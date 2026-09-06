using URLShortener.Application.Common;
using URLShortener.Application.Interfaces;

namespace URLShortener.Infrastructure.Analytics;

/// <summary>Used when Redis is disabled: clicks are not tracked.</summary>
internal sealed class NullClickEventPublisher : IClickEventPublisher
{
    public void Publish(ClickEvent clickEvent)
    {
        // Analytics requires Redis; nothing to do.
    }
}
