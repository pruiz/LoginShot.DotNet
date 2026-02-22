namespace LoginShot.Triggers;

internal interface ISessionEventSource
{
    event EventHandler<SessionEventType>? SessionEventReceived;
}
