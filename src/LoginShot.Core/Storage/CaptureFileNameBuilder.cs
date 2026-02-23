using LoginShot.Triggers;

namespace LoginShot.Storage;

public static class CaptureFileNameBuilder
{
	public static string Build(DateTimeOffset timestamp, SessionEventType eventType, string extension)
	{
		var safeExtension = extension.Trim().TrimStart('.');
		var eventTag = eventType.ToString().ToLowerInvariant();
		return $"{timestamp:yyyy-MM-ddTHH-mm-ss}-{eventTag}.{safeExtension}";
	}
}
