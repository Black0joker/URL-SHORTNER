namespace URLShortener.Application.DTOs;

public record UrlResponse
{
    public Guid Id { get; init; }
    public required string ShortCode { get; init; }
    public required string ShortUrl { get; init; }
    public required string OriginalUrl { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public bool IsActive { get; init; }
}
