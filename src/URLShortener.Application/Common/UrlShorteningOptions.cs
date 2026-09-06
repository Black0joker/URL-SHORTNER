namespace URLShortener.Application.Common;

public class UrlShorteningOptions
{
    public const string SectionName = "UrlShortening";
    public string BaseUrl { get; init; } = "https://myshort.com";
}
