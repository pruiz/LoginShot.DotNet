using LoginShot.Triggers;

namespace LoginShot.AppLaunch;

public sealed class StartupLogonLaunchCoordinator
{
    private readonly ITriggerDispatcher triggerDispatcher;

    public StartupLogonLaunchCoordinator(ITriggerDispatcher triggerDispatcher)
    {
        this.triggerDispatcher = triggerDispatcher;
    }

    public async Task DispatchStartupLogonTriggerAsync(IEnumerable<string> args, CancellationToken cancellationToken = default)
    {
        if (!AppLaunchTriggerParser.IsStartupLogonLaunch(args))
        {
            return;
        }

        await triggerDispatcher.DispatchAsync(SessionEventType.Logon, cancellationToken);
    }
}
