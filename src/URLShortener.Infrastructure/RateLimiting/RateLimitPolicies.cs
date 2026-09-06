namespace URLShortener.Infrastructure.RateLimiting;

/// <summary>Named rate-limiting policies applied via [EnableRateLimiting].</summary>
public static class RateLimitPolicies
{
    public const string AuthRegister = "auth-register";
    public const string AuthLogin = "auth-login";
    public const string UrlCreate = "url-create";
    public const string UrlRead = "url-read";
    public const string Redirect = "redirect";

    public static readonly string[] All =
        [AuthRegister, AuthLogin, UrlCreate, UrlRead, Redirect];
}
