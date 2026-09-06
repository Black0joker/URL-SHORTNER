namespace URLShortener.Infrastructure.Authentication;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public required string Issuer { get; init; }
    public required string Audience { get; init; }
    public required string SecretKey { get; init; }
    public int AccessTokenExpirationMinutes { get; init; } = 60;
}
