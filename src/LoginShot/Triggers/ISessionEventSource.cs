namespace LoginShot.Triggers;

internal interface ISessionEventSource : IDisposable
{
    event EventHandler<SessionEventType>? SessionEventReceived;
}
