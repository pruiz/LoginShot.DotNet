using System.Diagnostics;

namespace LoginShot.Triggers;

internal sealed class StartupTriggerDispatcher : ITriggerDispatcher
{
    public Task DispatchAsync(SessionEventType eventType, CancellationToken cancellationToken = default)
    {
        Debug.WriteLine($"Trigger dispatched: {eventType}");
        // TODO: Route dispatched triggers to the capture pipeline.
        return Task.CompletedTask;
    }
}
