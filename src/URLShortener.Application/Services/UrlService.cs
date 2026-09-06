using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using URLShortener.Application.Common;
using URLShortener.Application.DTOs;
using URLShortener.Application.Interfaces;
using URLShortener.Domain.Entities;
using URLShortener.Domain.Exceptions;

namespace URLShortener.Application.Services;

public class UrlService : IUrlService
{
    private readonly DbContext _dbContext;
    private readonly IShortCodeGenerator _shortCodeGenerator;
    private readonly IRedirectCache _redirectCache;
    private readonly string _baseUrl;

    private static readonly HashSet<string> ReservedAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        "api", "admin", "login", "register", "health", "swagger",
        "favicon.ico", "robots.txt", "sitemap.xml", ".well-known"
    };

    public UrlService(
        DbContext dbContext,
        IShortCodeGenerator shortCodeGenerator,
        IRedirectCache redirectCache,
        IOptions<UrlShorteningOptions> options)
    {
        _dbContext = dbContext;
        _shortCodeGenerator = shortCodeGenerator;
        _redirectCache = redirectCache;
        _baseUrl = options.Value.BaseUrl;
    }

    public async Task<UrlResponse> CreateUrlAsync(CreateUrlRequest request, Guid? userId, string? ipAddress)
    {
        ValidateUrl(request.OriginalUrl);

        string shortCode;
        if (!string.IsNullOrWhiteSpace(request.CustomAlias))
        {
            ValidateCustomAlias(request.CustomAlias);
            shortCode = request.CustomAlias;
        }
        else
        {
            shortCode = _shortCodeGenerator.Generate();
        }

        var url = new Url
        {
            ShortCode = shortCode,
            OriginalUrl = request.OriginalUrl,
            UserId = userId,
            ExpiresAt = request.ExpiresAt,
            CreatedByIp = ipAddress,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Set<Url>().Add(url);

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new ShortCodeConflictException(shortCode);
        }

        return MapToResponse(url);
    }

    public async Task<UrlResponse?> GetUrlByIdAsync(Guid id, Guid userId)
    {
        var url = await _dbContext.Set<Url>()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id && u.UserId == userId && u.DeletedAt == null);

        return url == null ? null : MapToResponse(url);
    }

    public async Task<PagedResult<UrlResponse>> GetUserUrlsAsync(
        Guid userId, int page = 1, int pageSize = 20, string? search = null, bool? active = null)
    {
        pageSize = Math.Min(pageSize, 100);
        page = Math.Max(page, 1);

        var query = _dbContext.Set<Url>()
            .AsNoTracking()
            .Where(u => u.UserId == userId && u.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(u =>
                u.OriginalUrl.Contains(search) || u.ShortCode.Contains(search));
        }

        if (active.HasValue)
        {
            query = query.Where(u => u.IsActive == active.Value);
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<UrlResponse>
        {
            Items = items.Select(MapToResponse).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<UrlResponse> UpdateUrlAsync(Guid id, Guid userId, UpdateUrlRequest request)
    {
        var url = await _dbContext.Set<Url>()
            .FirstOrDefaultAsync(u => u.Id == id && u.UserId == userId && u.DeletedAt == null)
            ?? throw new UrlNotFoundException();

        if (request.ExpiresAt.HasValue)
            url.ExpiresAt = request.ExpiresAt;

        if (request.IsActive.HasValue)
            url.IsActive = request.IsActive.Value;

        await _dbContext.SaveChangesAsync();

        // Cache invalidation: SQL is updated first, then the cache entry is dropped.
        // The next redirect repopulates a fresh value from SQL.
        await _redirectCache.RemoveAsync(url.ShortCode);

        return MapToResponse(url);
    }

    public async Task DeleteUrlAsync(Guid id, Guid userId)
    {
        var url = await _dbContext.Set<Url>()
            .FirstOrDefaultAsync(u => u.Id == id && u.UserId == userId && u.DeletedAt == null)
            ?? throw new UrlNotFoundException();

        url.IsActive = false;
        url.DeletedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        // Soft-deleted URLs must stop resolving immediately; drop the cache entry.
        await _redirectCache.RemoveAsync(url.ShortCode);
    }

    public async Task<string?> ResolveShortCodeAsync(string shortCode)
    {
        var url = await _dbContext.Set<Url>()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.ShortCode == shortCode && u.IsActive && u.DeletedAt == null);

        if (url == null)
            return null;

        if (url.IsExpired)
            return null;

        return url.OriginalUrl;
    }

    private void ValidateUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidUrlException("URL is required.");

        if (url.Length > 2048)
            throw new InvalidUrlException("URL exceeds maximum length of 2048 characters.");

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new InvalidUrlException("URL must be a valid absolute URI.");

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidUrlException("URL must use HTTP or HTTPS scheme.");
    }

    private void ValidateCustomAlias(string alias)
    {
        if (alias.Length < 3 || alias.Length > 64)
            throw new InvalidUrlException("Custom alias must be between 3 and 64 characters.");

        if (!alias.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'))
            throw new InvalidUrlException("Custom alias can only contain letters, numbers, hyphens, and underscores.");

        if (ReservedAliases.Contains(alias))
            throw new InvalidUrlException($"The alias '{alias}' is reserved.");
    }

    private UrlResponse MapToResponse(Url url)
    {
        return new UrlResponse
        {
            Id = url.Id,
            ShortCode = url.ShortCode,
            ShortUrl = $"{_baseUrl}/{url.ShortCode}",
            OriginalUrl = url.OriginalUrl,
            ExpiresAt = url.ExpiresAt,
            CreatedAt = url.CreatedAt,
            IsActive = url.IsActive
        };
    }
}
