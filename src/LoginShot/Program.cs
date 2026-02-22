using LoginShot.App;
using LoginShot.AppLaunch;
using LoginShot.Triggers;

namespace LoginShot;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        var triggerDispatcher = new StartupTriggerDispatcher();
        var startupCoordinator = new StartupLogonLaunchCoordinator(triggerDispatcher);
        startupCoordinator.DispatchStartupLogonTriggerAsync(args).GetAwaiter().GetResult();

        Application.Run(new LoginShotApplicationContext());
    }
}
