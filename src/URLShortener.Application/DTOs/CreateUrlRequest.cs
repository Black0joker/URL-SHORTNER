namespace URLShortener.Application.DTOs;

public record CreateUrlRequest
{
    public required string OriginalUrl { get; init; }
    public string? CustomAlias { get; init; }
    public DateTime? ExpiresAt { get; init; }
}
