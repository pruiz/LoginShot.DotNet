using System.Diagnostics;

namespace LoginShot.Triggers;

public sealed class SessionEventRouter
{
    private readonly ITriggerDispatcher triggerDispatcher;
    private readonly IEventDebouncer eventDebouncer;
    private readonly IEventTimeProvider timeProvider;
    private TriggerHandlingOptions options;

    public SessionEventRouter(
        ITriggerDispatcher triggerDispatcher,
        IEventDebouncer eventDebouncer,
        IEventTimeProvider timeProvider,
        TriggerHandlingOptions options)
    {
        this.triggerDispatcher = triggerDispatcher;
        this.eventDebouncer = eventDebouncer;
        this.timeProvider = timeProvider;
        this.options = options;
    }

    public void UpdateOptions(TriggerHandlingOptions updatedOptions)
    {
        options = updatedOptions;
    }

    public async Task HandleEventAsync(SessionEventType eventType, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled(eventType))
        {
            return;
        }

        if (RequiresDebounce(eventType))
        {
            var shouldProcess = eventDebouncer.ShouldProcess(eventType, timeProvider.UtcNow, options.DebounceWindow);
            if (!shouldProcess)
            {
                return;
            }
        }

        try
        {
            await triggerDispatcher.DispatchAsync(eventType, cancellationToken);
        }
        catch (Exception exception)
        {
            if (eventType == SessionEventType.Lock)
            {
                Debug.WriteLine($"Best-effort lock dispatch failed: {exception.Message}");
                return;
            }

            Debug.WriteLine($"Dispatch failed for {eventType}: {exception.Message}");
        }
    }

    private bool IsEnabled(SessionEventType eventType)
    {
        return eventType switch
        {
            SessionEventType.Unlock => options.EnableUnlock,
            SessionEventType.Lock => options.EnableLock,
            _ => true
        };
    }

    private static bool RequiresDebounce(SessionEventType eventType)
    {
        return eventType is SessionEventType.Unlock or SessionEventType.Lock;
    }
}
