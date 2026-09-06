using URLShortener.Application.Common;

namespace URLShortener.Application.Interfaces;

/// <summary>
/// Resolves a short code to its destination using the cache-aside pattern.
/// This is the performance-critical redirect hot path and should avoid hitting
/// SQL Server when the answer is already cached.
/// </summary>
public interface IRedirectService
{
    /// <summary>
    /// Returns the resolved redirect (URL id + destination) for the short code,
    /// or null when the code does not exist, is inactive, or has expired.
    /// </summary>
    Task<ResolvedRedirect?> ResolveAsync(string shortCode, CancellationToken cancellationToken = default);
}
