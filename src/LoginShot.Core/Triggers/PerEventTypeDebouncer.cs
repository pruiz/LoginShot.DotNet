namespace LoginShot.Triggers;

public sealed class PerEventTypeDebouncer : IEventDebouncer
{
    private readonly Dictionary<SessionEventType, DateTimeOffset> lastProcessedAtByEventType = new();

    public bool ShouldProcess(SessionEventType eventType, DateTimeOffset timestamp, TimeSpan debounceWindow)
    {
        if (debounceWindow <= TimeSpan.Zero)
        {
            lastProcessedAtByEventType[eventType] = timestamp;
            return true;
        }

        if (!lastProcessedAtByEventType.TryGetValue(eventType, out var lastProcessedAt))
        {
            lastProcessedAtByEventType[eventType] = timestamp;
            return true;
        }

        if (timestamp - lastProcessedAt >= debounceWindow)
        {
            lastProcessedAtByEventType[eventType] = timestamp;
            return true;
        }

        return false;
    }
}
