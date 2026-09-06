using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using URLShortener.Application.Common;
using URLShortener.Application.Interfaces;

namespace URLShortener.Infrastructure.Analytics;

/// <summary>
/// In-process bounded buffer between the redirect hot path and Redis.
/// Publishing is a non-blocking memory write; the flush worker drains the
/// channel and XADDs batches to the Redis stream. When the buffer is full the
/// oldest events are dropped — analytics is best-effort and never back-pressures redirects.
/// </summary>
internal sealed class ChannelClickEventPublisher : IClickEventPublisher
{
    private readonly Channel<ClickEvent> _channel;
    private readonly ILogger<ChannelClickEventPublisher> _logger;

    public ChannelClickEventPublisher(IOptions<AnalyticsOptions> options, ILogger<ChannelClickEventPublisher> logger)
    {
        _logger = logger;
        _channel = Channel.CreateBounded<ClickEvent>(new BoundedChannelOptions(options.Value.PublishBufferCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
    }

    /// <summary>Reader consumed by <see cref="ClickEventFlushWorker"/>.</summary>
    public ChannelReader<ClickEvent> Reader => _channel.Reader;

    public void Publish(ClickEvent clickEvent)
    {
        if (!_channel.Writer.TryWrite(clickEvent))
        {
            // Unreachable with DropOldest, kept as a safety net.
            _logger.LogWarning("Click event buffer rejected an event; analytics data lost.");
        }
    }
}
