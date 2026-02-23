namespace LoginShot.Triggers;

public interface ITriggerDispatcher
{
	Task DispatchAsync(SessionEventType eventType, CancellationToken cancellationToken = default);
}
