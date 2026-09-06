using System.ComponentModel.DataAnnotations;

namespace URLShortener.Application.DTOs;

public record RefreshRequest
{
    [Required]
    public required string RefreshToken { get; init; }
}
