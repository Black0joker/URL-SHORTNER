namespace URLShortener.Application.Interfaces;

/// <summary>
/// Deactivates URLs whose expiration time has passed.
/// SQL Server is the authoritative source; this keeps IsActive in sync with ExpiresAt
/// so expired links stop resolving without hard-deleting rows.
/// </summary>
public interface IExpiredUrlCleanupService
{
    /// <summary>
    /// Marks expired-but-still-active URLs as inactive.
    /// </summary>
    /// <returns>The number of URLs deactivated.</returns>
    Task<int> DeactivateExpiredUrlsAsync(CancellationToken cancellationToken = default);
}
