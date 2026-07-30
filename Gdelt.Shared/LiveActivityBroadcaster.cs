using System.Collections.Concurrent;
using System.Threading.Channels;

namespace GdeltSearchUI;

public readonly record struct LiveActivityEvent(
    DateTime Timestamp,
    string Widget,
    string Level,
    string Message);

// In-memory pub/sub for the live activity dashboard. PostLogger publishes here
// after every file write; the web dashboard's SSE endpoint subscribes.
// Keeps a bounded ring buffer so new browser tabs can replay recent history.
public static class LiveActivityBroadcaster
{
    private const int RingBufferCapacity = 500;

    private static readonly ConcurrentQueue<LiveActivityEvent> RingBuffer = new();
    private static readonly ConcurrentDictionary<Guid, Channel<LiveActivityEvent>> Subscribers = new();

    public static void Publish(string widget, string level, string message)
    {
        var evt = new LiveActivityEvent(DateTime.Now, widget, level, message);

        RingBuffer.Enqueue(evt);
        while (RingBuffer.Count > RingBufferCapacity)
            RingBuffer.TryDequeue(out _);

        foreach (var channel in Subscribers.Values)
            channel.Writer.TryWrite(evt);
    }

    public static IReadOnlyList<LiveActivityEvent> Recent() => RingBuffer.ToArray();

    // Registers a new subscriber and returns an id to unsubscribe with plus the
    // channel reader to await new events from.
    public static (Guid Id, ChannelReader<LiveActivityEvent> Reader) Subscribe()
    {
        var channel = Channel.CreateUnbounded<LiveActivityEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });
        var id = Guid.NewGuid();
        Subscribers[id] = channel;
        return (id, channel.Reader);
    }

    public static void Unsubscribe(Guid id)
    {
        if (Subscribers.TryRemove(id, out var channel))
            channel.Writer.TryComplete();
    }
}
