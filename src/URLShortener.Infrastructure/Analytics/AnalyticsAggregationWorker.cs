using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace URLShortener.Infrastructure.Analytics;

/// <summary>
/// Consumes click events from the Redis stream via a consumer group (shared across
/// API instances) and upserts batched aggregates into SQL Server.
///
/// Reliability model: at-least-once. Entries are only acknowledged after the SQL
/// transaction commits, so a crash before commit re-delivers the batch on restart
/// (pending entries are drained first).
/// </summary>
internal sealed class AnalyticsAggregationWorker : BackgroundService
{
    private const string MergeDailySql = """
        MERGE [UrlDailyStats] WITH (HOLDLOCK) AS t
        USING (SELECT @p0 AS UrlId, @p1 AS [Date]) AS s
        ON t.UrlId = s.UrlId AND t.[Date] = s.[Date]
        WHEN MATCHED THEN UPDATE SET ClickCount = t.ClickCount + @p2
        WHEN NOT MATCHED THEN INSERT (UrlId, [Date], ClickCount) VALUES (s.UrlId, s.[Date], @p2);
        """;

    private const string MergeCountrySql = """
        MERGE [UrlCountryDailyStats] WITH (HOLDLOCK) AS t
        USING (SELECT @p0 AS UrlId, @p1 AS [Date], @p2 AS CountryCode) AS s
        ON t.UrlId = s.UrlId AND t.[Date] = s.[Date] AND t.CountryCode = s.CountryCode
        WHEN MATCHED THEN UPDATE SET ClickCount = t.ClickCount + @p3
        WHEN NOT MATCHED THEN INSERT (UrlId, [Date], CountryCode, ClickCount) VALUES (s.UrlId, s.[Date], s.CountryCode, @p3);
        """;

    // Stream read positions: ">" = new (undelivered) messages, "0" = this consumer's pending
    // messages, "$" = start the group at the newest existing message.
    private static readonly RedisValue NewMessagesPosition = ">";
    private static readonly RedisValue PendingPosition = "0";
    private static readonly RedisValue GroupStartPosition = "$";

    private const string MergeDeviceSql = """
        MERGE [UrlDeviceDailyStats] WITH (HOLDLOCK) AS t
        USING (SELECT @p0 AS UrlId, @p1 AS [Date], @p2 AS DeviceType) AS s
        ON t.UrlId = s.UrlId AND t.[Date] = s.[Date] AND t.DeviceType = s.DeviceType
        WHEN MATCHED THEN UPDATE SET ClickCount = t.ClickCount + @p3
        WHEN NOT MATCHED THEN INSERT (UrlId, [Date], DeviceType, ClickCount) VALUES (s.UrlId, s.[Date], s.DeviceType, @p3);
        """;

    private readonly IConnectionMultiplexer _redis;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AnalyticsOptions _options;
    private readonly ILogger<AnalyticsAggregationWorker> _logger;
    private readonly string _consumerName;

    public AnalyticsAggregationWorker(
        IConnectionMultiplexer redis,
        IServiceScopeFactory scopeFactory,
        IOptions<AnalyticsOptions> options,
        ILogger<AnalyticsAggregationWorker> logger)
    {
        _redis = redis;
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;

        // Unique per process so multiple API instances share the group but not the consumer.
        _consumerName = $"{Environment.MachineName}-{Guid.NewGuid():N}";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Analytics aggregation worker started. Group: {Group}, Consumer: {Consumer}.",
            _options.ConsumerGroup, _consumerName);

        var db = _redis.GetDatabase();
        await EnsureConsumerGroupAsync(db, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 1) Drain this consumer's pending entries (unacked from a previous crash).
                var pending = await db.StreamReadGroupAsync(
                    _options.StreamKey, _options.ConsumerGroup, _consumerName,
                    PendingPosition, count: _options.BatchSize);

                if (pending.Length > 0)
                    await ProcessBatchAsync(db, pending);

                // 2) Read new messages delivered to the group.
                var entries = await db.StreamReadGroupAsync(
                    _options.StreamKey, _options.ConsumerGroup, _consumerName,
                    NewMessagesPosition, count: _options.BatchSize);

                if (entries.Length > 0)
                {
                    await ProcessBatchAsync(db, entries);
                }
                else
                {
                    await Task.Delay(_options.PollDelayMilliseconds, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Analytics worker iteration failed; retrying.");

                try
                {
                    await Task.Delay(_options.FlushFailureDelayMilliseconds, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        _logger.LogInformation("Analytics aggregation worker stopped.");
    }

    private async Task EnsureConsumerGroupAsync(IDatabase db, CancellationToken cancellationToken)
    {
        // Retry until Redis is reachable: the worker must not crash the host on startup.
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await db.StreamCreateConsumerGroupAsync(
                    _options.StreamKey, _options.ConsumerGroup, GroupStartPosition, createStream: true);
                return;
            }
            catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP", StringComparison.Ordinal))
            {
                return; // Group already exists.
            }
            catch (Exception ex) when (ex is RedisException or TimeoutException or IOException)
            {
                _logger.LogWarning(ex, "Redis unavailable while creating consumer group; retrying.");
                await Task.Delay(_options.FlushFailureDelayMilliseconds, cancellationToken);
            }
        }
    }

    private async Task ProcessBatchAsync(IDatabase redisDb, StreamEntry[] entries)
    {
        var totals = new Dictionary<(Guid UrlId, DateOnly Date), long>();
        var countries = new Dictionary<(Guid UrlId, DateOnly Date, string Country), long>();
        var devices = new Dictionary<(Guid UrlId, DateOnly Date, string Device), long>();

        var ackIds = new List<RedisValue>(entries.Length);

        foreach (var entry in entries)
        {
            var click = ClickEventCodec.Decode(entry.Values);

            if (click is not null)
            {
                var date = DateOnly.FromDateTime(click.TimestampUtc.Date);

                Increment(totals, (click.UrlId, date));

                if (click.Country is not null)
                    Increment(countries, (click.UrlId, date, NormalizeCountry(click.Country)));

                Increment(devices, (click.UrlId, date, NormalizeDevice(click.DeviceType)));
            }

            // Malformed entries are acknowledged too, so they cannot poison the queue.
            ackIds.Add(entry.Id);
        }

        await PersistAsync(totals, countries, devices);

        await redisDb.StreamAcknowledgeAsync(_options.StreamKey, _options.ConsumerGroup, ackIds.ToArray());
    }

    private async Task PersistAsync(
        Dictionary<(Guid, DateOnly), long> totals,
        Dictionary<(Guid, DateOnly, string), long> countries,
        Dictionary<(Guid, DateOnly, string), long> devices)
    {
        if (totals.Count == 0 && countries.Count == 0 && devices.Count == 0)
            return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DbContext>();

        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        foreach (var ((urlId, date), count) in totals)
        {
            await dbContext.Database.ExecuteSqlRawAsync(MergeDailySql, urlId, date, count);
        }

        foreach (var ((urlId, date, country), count) in countries)
        {
            await dbContext.Database.ExecuteSqlRawAsync(MergeCountrySql, urlId, date, country, count);
        }

        foreach (var ((urlId, date, device), count) in devices)
        {
            await dbContext.Database.ExecuteSqlRawAsync(MergeDeviceSql, urlId, date, device, count);
        }

        await transaction.CommitAsync();

        _logger.LogDebug(
            "Aggregated {Totals} daily, {Countries} country, {Devices} device buckets.",
            totals.Count, countries.Count, devices.Count);
    }

    private static void Increment<T>(Dictionary<T, long> map, T key)
        where T : notnull
    {
        map.TryGetValue(key, out var current);
        map[key] = current + 1;
    }

    /// <summary>Uppercased ISO alpha-2, truncated to the column width.</summary>
    private static string NormalizeCountry(string country)
    {
        var trimmed = country.Trim().ToUpperInvariant();
        return trimmed.Length <= 2 ? trimmed : trimmed[..2];
    }

    /// <summary>Clamps free-form device strings to the column width.</summary>
    private static string NormalizeDevice(string device)
    {
        var trimmed = device.Trim().ToLowerInvariant();
        return trimmed.Length <= 16 ? trimmed : trimmed[..16];
    }
}
