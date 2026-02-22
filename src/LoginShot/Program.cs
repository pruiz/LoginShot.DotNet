using LoginShot.App;

namespace LoginShot;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new LoginShotApplicationContext());
    }
}
