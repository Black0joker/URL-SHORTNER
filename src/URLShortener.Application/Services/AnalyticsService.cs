using Microsoft.EntityFrameworkCore;
using URLShortener.Application.DTOs;
using URLShortener.Application.Interfaces;
using URLShortener.Domain.Entities;

namespace URLShortener.Application.Services;

/// <summary>
/// Reads the aggregated analytics tables populated by the background worker.
/// Ownership is enforced in every query (URL must belong to the requesting user).
/// </summary>
public class AnalyticsService : IAnalyticsService
{
    private const int MaxDays = 365;
    private const int TopDimensions = 10;

    private readonly DbContext _dbContext;

    public AnalyticsService(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<UrlStatsResponse?> GetUrlStatsAsync(Guid urlId, Guid userId, int days, CancellationToken cancellationToken = default)
    {
        days = Math.Clamp(days, 1, MaxDays);

        // Ownership check prevents IDOR/BOLA: stats are only visible to the owner.
        var url = await _dbContext.Set<Url>()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == urlId && u.UserId == userId && u.DeletedAt == null, cancellationToken);

        if (url is null)
            return null;

        var fromDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-(days - 1)));

        var daily = await _dbContext.Set<UrlDailyStat>()
            .AsNoTracking()
            .Where(s => s.UrlId == urlId && s.Date >= fromDate)
            .OrderBy(s => s.Date)
            .Select(s => new DailyClickCount { Date = s.Date, ClickCount = s.ClickCount })
            .ToListAsync(cancellationToken);

        var countries = await _dbContext.Set<UrlCountryDailyStat>()
            .AsNoTracking()
            .Where(s => s.UrlId == urlId && s.Date >= fromDate)
            .GroupBy(s => s.CountryCode)
            .Select(g => new DimensionClickCount { Value = g.Key, ClickCount = g.Sum(x => x.ClickCount) })
            .OrderByDescending(x => x.ClickCount)
            .Take(TopDimensions)
            .ToListAsync(cancellationToken);

        var devices = await _dbContext.Set<UrlDeviceDailyStat>()
            .AsNoTracking()
            .Where(s => s.UrlId == urlId && s.Date >= fromDate)
            .GroupBy(s => s.DeviceType)
            .Select(g => new DimensionClickCount { Value = g.Key, ClickCount = g.Sum(x => x.ClickCount) })
            .OrderByDescending(x => x.ClickCount)
            .Take(TopDimensions)
            .ToListAsync(cancellationToken);

        return new UrlStatsResponse
        {
            UrlId = urlId,
            ShortCode = url.ShortCode,
            FromDate = fromDate,
            TotalClicks = daily.Sum(d => d.ClickCount),
            Daily = daily,
            Countries = countries,
            Devices = devices
        };
    }
}
