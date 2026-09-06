using URLShortener.Application.Common;

namespace URLShortener.Application.Interfaces;

/// <summary>
/// Publishes click events for asynchronous analytics processing.
/// Implementations MUST NOT block the redirect hot path and MUST NOT throw;
/// analytics is eventually-consistent, best-effort data.
/// </summary>
public interface IClickEventPublisher
{
    /// <summary>Enqueues a click event. Safe to call on every redirect.</summary>
    void Publish(ClickEvent clickEvent);
}
