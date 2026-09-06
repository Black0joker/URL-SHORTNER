using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using URLShortener.Application.Common;
using URLShortener.Application.Interfaces;
using URLShortener.Infrastructure.RateLimiting;

namespace URLShortener.Api.Controllers;

[ApiController]
public class RedirectController : ControllerBase
{
    private readonly IRedirectService _redirectService;
    private readonly IClickEventPublisher _clickEventPublisher;

    public RedirectController(IRedirectService redirectService, IClickEventPublisher clickEventPublisher)
    {
        _redirectService = redirectService;
        _clickEventPublisher = clickEventPublisher;
    }

    [HttpGet("/{shortCode}")]
    [EnableRateLimiting(RateLimitPolicies.Redirect)]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RedirectToUrl(string shortCode, CancellationToken cancellationToken)
    {
        // Restrict route to reasonable short code shapes to avoid matching arbitrary paths
        if (string.IsNullOrWhiteSpace(shortCode) || shortCode.Length > 64)
            return NotFound();

        var resolved = await _redirectService.ResolveAsync(shortCode, cancellationToken);

        if (resolved == null)
            return NotFound();

        // Phase 7: fire-and-forget analytics. The publisher only enqueues into an
        // in-process buffer, so it never blocks or fails the redirect response.
        // No raw IP is captured; only coarse device/browser/referrer dimensions.
        var userAgent = Request.Headers.UserAgent.ToString();
        _clickEventPublisher.Publish(new ClickEvent
        {
            UrlId = resolved.UrlId,
            TimestampUtc = DateTime.UtcNow,
            DeviceType = UserAgentInfo.ParseDeviceType(userAgent),
            Browser = UserAgentInfo.ParseBrowser(userAgent),
            ReferrerHost = ParseReferrerHost(Request.Headers.Referer.ToString())
        });

        // 302 Found keeps the destination mutable; avoids aggressive caching of 301/308.
        return Redirect(resolved.Destination);
    }

    private static string? ParseReferrerHost(string? referer) =>
        !string.IsNullOrWhiteSpace(referer) && Uri.TryCreate(referer, UriKind.Absolute, out var uri)
            ? uri.Host
            : null;
}
