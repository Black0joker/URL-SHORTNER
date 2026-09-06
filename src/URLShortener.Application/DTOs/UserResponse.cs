namespace URLShortener.Application.DTOs;

public record UserResponse
{
    public Guid Id { get; init; }
    public required string Email { get; init; }
    public DateTime CreatedAt { get; init; }
}
