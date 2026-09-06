using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using URLShortener.Application.Common;

namespace URLShortener.Infrastructure.Analytics;

/// <summary>
/// Drains the in-process click buffer and appends events to the Redis stream.
/// This is the "queue" half of the asynchronous analytics pipeline: the redirect
/// path never talks to Redis for analytics directly.
/// </summary>
internal sealed class ClickEventFlushWorker : BackgroundService
{
    private readonly ChannelClickEventPublisher _publisher;
    private readonly IConnectionMultiplexer _redis;
    private readonly AnalyticsOptions _options;
    private readonly ILogger<ClickEventFlushWorker> _logger;

    public ClickEventFlushWorker(
        ChannelClickEventPublisher publisher,
        IConnectionMultiplexer redis,
        IOptions<AnalyticsOptions> options,
        ILogger<ClickEventFlushWorker> logger)
    {
        _publisher = publisher;
        _redis = redis;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Click event flush worker started. Stream: {StreamKey}.", _options.StreamKey);

        var db = _redis.GetDatabase();
        var batch = new List<ClickEvent>(_options.BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            batch.Clear();

            try
            {
                // Block until at least one event arrives, then drain up to a full batch.
                while (batch.Count < _options.BatchSize && await _publisher.Reader.WaitToReadAsync(stoppingToken))
                {
                    while (batch.Count < _options.BatchSize && _publisher.Reader.TryRead(out var clickEvent))
                        batch.Add(clickEvent);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (batch.Count == 0)
                continue;

            try
            {
                foreach (var clickEvent in batch)
                {
                    await db.StreamAddAsync(
                        _options.StreamKey,
                        ClickEventCodec.Encode(clickEvent),
                        maxLength: _options.MaxStreamLength,
                        useApproximateMaxLength: true);
                }
            }
            catch (Exception ex) when (ex is RedisException or TimeoutException or IOException)
            {
                // Analytics is best-effort: drop the batch rather than stall the buffer.
                _logger.LogWarning(ex, "Redis unavailable; dropped {Count} click events.", batch.Count);

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

        _logger.LogInformation("Click event flush worker stopped.");
    }
}
