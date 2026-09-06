using System.ComponentModel.DataAnnotations;

namespace URLShortener.Application.DTOs;

public record RegisterRequest
{
    [Required]
    [EmailAddress]
    public required string Email { get; init; }

    [Required]
    [MinLength(8)]
    public required string Password { get; init; }
}
