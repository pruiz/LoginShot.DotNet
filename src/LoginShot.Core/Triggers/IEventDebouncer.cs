namespace LoginShot.Triggers;

public interface IEventDebouncer
{
	bool ShouldProcess(SessionEventType eventType, DateTimeOffset timestamp, TimeSpan debounceWindow);
}
