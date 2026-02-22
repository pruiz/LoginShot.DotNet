using LoginShot.App;
using LoginShot.AppLaunch;
using LoginShot.Config;
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

        try
        {
            Application.Run(new LoginShotApplicationContext(triggerDispatcher));
        }
        catch (ConfigValidationException exception)
        {
            MessageBox.Show(
                exception.Message,
                "LoginShot configuration error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
