using Microsoft.EntityFrameworkCore;
using URLShortener.Application.Interfaces;
using URLShortener.Domain.Entities;

namespace URLShortener.Application.Services;

public class ExpiredUrlCleanupService : IExpiredUrlCleanupService
{
    private readonly DbContext _dbContext;

    public ExpiredUrlCleanupService(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int> DeactivateExpiredUrlsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        // Bulk update avoids loading every expired row into memory.
        var affected = await _dbContext.Set<Url>()
            .Where(u => u.IsActive && u.ExpiresAt != null && u.ExpiresAt <= now)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false), cancellationToken);

        return affected;
    }
}
