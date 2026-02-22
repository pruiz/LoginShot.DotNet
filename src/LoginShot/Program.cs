using LoginShot.App;
using LoginShot.AppLaunch;

namespace LoginShot;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        var launchFromStartupLogon = AppLaunchTriggerParser.IsStartupLogonLaunch(args);
        Application.Run(new LoginShotApplicationContext(launchFromStartupLogon));
    }
}
