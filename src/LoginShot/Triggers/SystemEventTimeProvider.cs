namespace LoginShot.Triggers;

internal sealed class SystemEventTimeProvider : IEventTimeProvider
{
	public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
