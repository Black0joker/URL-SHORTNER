namespace URLShortener.Application.DTOs;

public record UpdateUrlRequest
{
    public DateTime? ExpiresAt { get; init; }
    public bool? IsActive { get; init; }
}
