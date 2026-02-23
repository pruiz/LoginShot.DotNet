namespace LoginShot.Triggers;

public interface IEventTimeProvider
{
	DateTimeOffset UtcNow { get; }
}
