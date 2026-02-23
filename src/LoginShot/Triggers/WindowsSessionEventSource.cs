using Microsoft.Win32;

namespace LoginShot.Triggers;

internal sealed class WindowsSessionEventSource : ISessionEventSource
{
	public event EventHandler<SessionEventType>? SessionEventReceived;

	public WindowsSessionEventSource()
	{
		SystemEvents.SessionSwitch += OnSessionSwitch;
	}

	public void Dispose()
	{
		SystemEvents.SessionSwitch -= OnSessionSwitch;
	}

	private void OnSessionSwitch(object sender, SessionSwitchEventArgs args)
	{
		var eventType = args.Reason switch
		{
			SessionSwitchReason.SessionLock => SessionEventType.Lock,
			SessionSwitchReason.SessionUnlock => SessionEventType.Unlock,
			_ => (SessionEventType?)null
		};

		if (eventType is null)
		{
			return;
		}

		SessionEventReceived?.Invoke(this, eventType.Value);
	}
}
