using URLShortener.Application.DTOs;

namespace URLShortener.Application.Interfaces;

/// <summary>Reads aggregated click statistics (owner-scoped).</summary>
public interface IAnalyticsService
{
    /// <summary>
    /// Returns aggregated stats for the URL over the trailing <paramref name="days"/> window,
    /// or null when the URL does not exist or is not owned by <paramref name="userId"/>.
    /// </summary>
    Task<UrlStatsResponse?> GetUrlStatsAsync(Guid urlId, Guid userId, int days, CancellationToken cancellationToken = default);
}
