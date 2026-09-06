namespace URLShortener.Application.Common;

/// <summary>Result of resolving a short code on the redirect hot path.</summary>
/// <param name="UrlId">Database identity of the URL, used for analytics attribution.</param>
/// <param name="Destination">The original URL to redirect to.</param>
public sealed record ResolvedRedirect(Guid UrlId, string Destination);
