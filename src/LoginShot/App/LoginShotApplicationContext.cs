using System.Diagnostics;
using System.Drawing;
using LoginShot.Config;
using LoginShot.Startup;
using LoginShot.Storage;

namespace LoginShot.App;

internal sealed class LoginShotApplicationContext : ApplicationContext
{
    private readonly ContextMenuStrip menu;
    private readonly NotifyIcon trayIcon;
    private readonly ToolStripMenuItem startAfterLoginMenuItem;
    private readonly IStartupRegistrationService startupRegistrationService;

    public LoginShotApplicationContext()
    {
        startupRegistrationService = CreateStartupRegistrationService();

        startAfterLoginMenuItem = new ToolStripMenuItem("Start after login")
        {
            CheckOnClick = true
        };
        startAfterLoginMenuItem.Click += OnStartAfterLoginClicked;
        startAfterLoginMenuItem.Checked = startupRegistrationService.IsEnabled();

        menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem("Capture now", null, OnCaptureNowClicked));
        menu.Items.Add(new ToolStripMenuItem("Open output folder", null, OnOpenOutputFolderClicked));
        menu.Items.Add(startAfterLoginMenuItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Reload config", null, OnReloadConfigClicked));
        menu.Items.Add(new ToolStripMenuItem("Generate sample config", null, OnGenerateSampleConfigClicked));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Quit", null, OnQuitClicked));

        trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "LoginShot",
            ContextMenuStrip = menu,
            Visible = true
        };
    }

    private static void OnCaptureNowClicked(object? sender, EventArgs eventArgs)
    {
    }

    private static void OnReloadConfigClicked(object? sender, EventArgs eventArgs)
    {
    }

    private static void OnOpenOutputFolderClicked(object? sender, EventArgs eventArgs)
    {
        var outputDirectory = OutputPathProvider.GetDefaultOutputDirectory();
        Directory.CreateDirectory(outputDirectory);

        var processStartInfo = new ProcessStartInfo
        {
            FileName = outputDirectory,
            UseShellExecute = true
        };

        Process.Start(processStartInfo);
    }

    private static void OnGenerateSampleConfigClicked(object? sender, EventArgs eventArgs)
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var configDirectory = Path.Combine(appDataPath, "LoginShot");
        var configPath = Path.Combine(configDirectory, "config.yml");

        Directory.CreateDirectory(configDirectory);
        if (File.Exists(configPath))
        {
            return;
        }

        var sample = ConfigPaths.SampleConfigYaml;
        File.WriteAllText(configPath, sample);
    }

    private void OnStartAfterLoginClicked(object? sender, EventArgs eventArgs)
    {
        try
        {
            if (startAfterLoginMenuItem.Checked)
            {
                startupRegistrationService.Enable();
            }
            else
            {
                startupRegistrationService.Disable();
            }
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Failed to update startup setting: {exception.Message}",
                "LoginShot",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        startAfterLoginMenuItem.Checked = startupRegistrationService.IsEnabled();
    }

    private void OnQuitClicked(object? sender, EventArgs eventArgs)
    {
        trayIcon.Visible = false;
        trayIcon.Dispose();
        menu.Dispose();
        ExitThread();
    }

    private static IStartupRegistrationService CreateStartupRegistrationService()
    {
        var executablePath = Application.ExecutablePath;
        var startupDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        var legacyShortcutPath = Path.Combine(startupDirectory, "LoginShot.lnk");
        var schedulerClient = new SchtasksStartupTaskSchedulerClient();
        var fileSystem = new SystemFileSystem();

        return new TaskSchedulerStartupRegistrationService(
            "LoginShot\\StartAfterLogin",
            executablePath,
            "--startup-trigger=logon",
            legacyShortcutPath,
            schedulerClient,
            fileSystem);
    }

}
