using System.ComponentModel.DataAnnotations;

namespace URLShortener.Application.DTOs;

public record LoginRequest
{
    [Required]
    [EmailAddress]
    public required string Email { get; init; }

    [Required]
    public required string Password { get; init; }
}
