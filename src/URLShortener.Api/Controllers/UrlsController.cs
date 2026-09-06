using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using URLShortener.Application.DTOs;
using URLShortener.Application.Interfaces;
using URLShortener.Infrastructure.RateLimiting;

namespace URLShortener.Api.Controllers;

[ApiController]
[Route("api/v1/urls")]
[Authorize]
public class UrlsController : ControllerBase
{
    private readonly IUrlService _urlService;

    public UrlsController(IUrlService urlService)
    {
        _urlService = urlService;
    }

    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.UrlCreate)]
    [ProducesResponseType(typeof(UrlResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateUrl([FromBody] CreateUrlRequest request)
    {
        var userId = GetCurrentUserId();
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

        var response = await _urlService.CreateUrlAsync(request, userId, ipAddress);

        return CreatedAtAction(nameof(GetUrl), new { id = response.Id }, response);
    }

    [HttpGet("{id:guid}")]
    [EnableRateLimiting(RateLimitPolicies.UrlRead)]
    [ProducesResponseType(typeof(UrlResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUrl(Guid id)
    {
        var userId = GetCurrentUserId();
        var response = await _urlService.GetUrlByIdAsync(id, userId);

        if (response == null)
            return NotFound();

        return Ok(response);
    }

    [HttpGet]
    [EnableRateLimiting(RateLimitPolicies.UrlRead)]
    [ProducesResponseType(typeof(PagedResult<UrlResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListUrls(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] bool? active = null)
    {
        var userId = GetCurrentUserId();
        var result = await _urlService.GetUserUrlsAsync(userId, page, pageSize, search, active);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(UrlResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateUrl(Guid id, [FromBody] UpdateUrlRequest request)
    {
        var userId = GetCurrentUserId();
        var response = await _urlService.UpdateUrlAsync(id, userId, request);
        return Ok(response);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteUrl(Guid id)
    {
        var userId = GetCurrentUserId();
        await _urlService.DeleteUrlAsync(id, userId);
        return NoContent();
    }

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.Parse(sub!);
    }
}
