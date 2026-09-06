using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using URLShortener.Application.Interfaces;
using URLShortener.Infrastructure.RateLimiting;

namespace URLShortener.Api.Controllers;

[ApiController]
public class RedirectController : ControllerBase
{
    private readonly IRedirectService _redirectService;

    public RedirectController(IRedirectService redirectService)
    {
        _redirectService = redirectService;
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

        var destination = await _redirectService.ResolveAsync(shortCode, cancellationToken);

        if (destination == null)
            return NotFound();

        // 302 Found keeps the destination mutable; avoids aggressive caching of 301/308.
        return Redirect(destination);
    }
}
