using URLShortener.Application.DTOs;

namespace URLShortener.Application.Interfaces;

public interface IUrlService
{
    Task<UrlResponse> CreateUrlAsync(CreateUrlRequest request, Guid? userId, string? ipAddress);
    Task<UrlResponse?> GetUrlByIdAsync(Guid id, Guid userId);
    Task<PagedResult<UrlResponse>> GetUserUrlsAsync(Guid userId, int page = 1, int pageSize = 20, string? search = null, bool? active = null);
    Task<UrlResponse> UpdateUrlAsync(Guid id, Guid userId, UpdateUrlRequest request);
    Task DeleteUrlAsync(Guid id, Guid userId);
    Task<string?> ResolveShortCodeAsync(string shortCode);
}
