using System.Diagnostics;
using System.Drawing;
using LoginShot.Config;
using LoginShot.Startup;
using LoginShot.Triggers;

namespace LoginShot.App;

internal sealed class LoginShotApplicationContext : ApplicationContext
{
    private readonly ContextMenuStrip menu;
    private readonly NotifyIcon trayIcon;
    private readonly ToolStripMenuItem startAfterLoginMenuItem;
    private readonly IStartupRegistrationService startupRegistrationService;
    private readonly IConfigLoader configLoader;
    private readonly ISessionEventSource sessionEventSource;
    private readonly SessionEventRouter sessionEventRouter;
    private LoginShotConfig currentConfig;

    public LoginShotApplicationContext(ITriggerDispatcher triggerDispatcher)
    {
        configLoader = CreateConfigLoader();
        currentConfig = LoadConfigOnStartup(configLoader);

        sessionEventRouter = CreateSessionEventRouter(triggerDispatcher, currentConfig);
        sessionEventSource = new WindowsSessionEventSource();
        sessionEventSource.SessionEventReceived += OnSessionEventReceived;

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

    private void OnReloadConfigClicked(object? sender, EventArgs eventArgs)
    {
        try
        {
            currentConfig = configLoader.Load();
            sessionEventRouter.UpdateOptions(CreateTriggerHandlingOptions(currentConfig));
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Failed to reload config: {exception.Message}",
                "LoginShot",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void OnOpenOutputFolderClicked(object? sender, EventArgs eventArgs)
    {
        var outputDirectory = currentConfig.Output.Directory;
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
        sessionEventSource.SessionEventReceived -= OnSessionEventReceived;
        sessionEventSource.Dispose();
        trayIcon.Visible = false;
        trayIcon.Dispose();
        menu.Dispose();
        ExitThread();
    }

    private void OnSessionEventReceived(object? sender, SessionEventType eventType)
    {
        _ = sessionEventRouter.HandleEventAsync(eventType);
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

    private static IConfigLoader CreateConfigLoader()
    {
        var userProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var fileProvider = new SystemConfigFileProvider();
        var pathResolver = new ConfigPathResolver(userProfilePath, appDataPath, fileProvider);
        return new LoginShotConfigLoader(pathResolver, fileProvider);
    }

    private static LoginShotConfig LoadConfigOnStartup(IConfigLoader loader)
    {
        return loader.Load();
    }

    private static SessionEventRouter CreateSessionEventRouter(ITriggerDispatcher triggerDispatcher, LoginShotConfig config)
    {
        var debouncer = new PerEventTypeDebouncer();
        var timeProvider = new SystemEventTimeProvider();
        var options = CreateTriggerHandlingOptions(config);
        return new SessionEventRouter(triggerDispatcher, debouncer, timeProvider, options);
    }

    private static TriggerHandlingOptions CreateTriggerHandlingOptions(LoginShotConfig config)
    {
        return new TriggerHandlingOptions(
            config.Triggers.OnUnlock,
            config.Triggers.OnLock,
            TimeSpan.FromSeconds(config.Capture.DebounceSeconds));
    }

}
