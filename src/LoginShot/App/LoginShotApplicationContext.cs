using System.Diagnostics;
using System.Drawing;
using LoginShot.Capture;
using LoginShot.Config;
using LoginShot.Startup;
using LoginShot.Triggers;

namespace LoginShot.App;

internal sealed class LoginShotApplicationContext : ApplicationContext
{
    private const int CameraIndexProbeCount = 10;

    private readonly ContextMenuStrip menu;
    private readonly NotifyIcon trayIcon;
    private readonly ToolStripMenuItem cameraMenuItem;
    private readonly ToolStripMenuItem startAfterLoginMenuItem;
    private readonly IStartupRegistrationService startupRegistrationService;
    private readonly IConfigLoader configLoader;
    private readonly IConfigWriter configWriter;
    private readonly ICameraDeviceEnumerator cameraDeviceEnumerator;
    private readonly ISessionEventSource sessionEventSource;
    private readonly SessionEventRouter sessionEventRouter;
    private LoginShotConfig currentConfig;

    public LoginShotApplicationContext(ITriggerDispatcher triggerDispatcher)
    {
        configLoader = CreateConfigLoader();
        configWriter = new YamlConfigWriter();
        cameraDeviceEnumerator = new OpenCvCameraDeviceEnumerator();
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

        cameraMenuItem = new ToolStripMenuItem("Camera");
        cameraMenuItem.DropDownOpening += OnCameraMenuOpening;

        menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem("Capture now", null, OnCaptureNowClicked));
        menu.Items.Add(new ToolStripMenuItem("Open output folder", null, OnOpenOutputFolderClicked));
        menu.Items.Add(cameraMenuItem);
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

        RefreshCameraMenuItems();
    }

    private static void OnCaptureNowClicked(object? sender, EventArgs eventArgs)
    {
    }

    private void OnCameraMenuOpening(object? sender, EventArgs eventArgs)
    {
        RefreshCameraMenuItems();
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

    private async void OnVerifySelectedCameraClicked(object? sender, EventArgs eventArgs)
    {
        try
        {
            var captureService = CaptureBackendFactory.Create(currentConfig.Capture.Backend, message => Debug.WriteLine(message));
            var request = new CaptureRequest(
                EventType: SessionEventType.Manual,
                MaxWidth: currentConfig.Output.MaxWidth,
                JpegQuality: currentConfig.Output.JpegQuality,
                CameraIndex: currentConfig.Capture.CameraIndex);

            var result = await captureService.CaptureOnceAsync(request, CancellationToken.None);
            if (result.Success)
            {
                MessageBox.Show(
                    "Camera verification succeeded.",
                    "LoginShot",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            MessageBox.Show(
                $"Camera verification failed: {result.ErrorMessage}",
                "LoginShot",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Camera verification failed: {exception.Message}",
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
        cameraMenuItem.DropDownOpening -= OnCameraMenuOpening;
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

    private void RefreshCameraMenuItems()
    {
        var detectedIndexes = cameraDeviceEnumerator.EnumerateIndexes(CameraIndexProbeCount);
        cameraMenuItem.DropDownItems.Clear();

        var autoItem = new ToolStripMenuItem("Auto (default)")
        {
            Checked = currentConfig.Capture.CameraIndex is null
        };
        autoItem.Click += (_, _) => ApplyCameraSelection(null);
        cameraMenuItem.DropDownItems.Add(autoItem);

        if (detectedIndexes.Count > 0)
        {
            cameraMenuItem.DropDownItems.Add(new ToolStripSeparator());
        }

        foreach (var index in detectedIndexes)
        {
            var item = new ToolStripMenuItem($"Camera {index}")
            {
                Checked = currentConfig.Capture.CameraIndex == index,
                Tag = index
            };
            item.Click += (_, _) => ApplyCameraSelection(index);
            cameraMenuItem.DropDownItems.Add(item);
        }

        cameraMenuItem.DropDownItems.Add(new ToolStripSeparator());
        cameraMenuItem.DropDownItems.Add(new ToolStripMenuItem("Verify selected camera", null, OnVerifySelectedCameraClicked));
    }

    private void ApplyCameraSelection(int? cameraIndex)
    {
        var previousConfig = currentConfig;
        currentConfig = currentConfig with
        {
            Capture = currentConfig.Capture with
            {
                CameraIndex = cameraIndex
            }
        };

        try
        {
            var savedPath = configWriter.Save(currentConfig, currentConfig.SourcePath);
            currentConfig = currentConfig with { SourcePath = savedPath };
        }
        catch (Exception exception)
        {
            currentConfig = previousConfig;
            MessageBox.Show(
                $"Failed to save camera selection: {exception.Message}",
                "LoginShot",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        RefreshCameraMenuItems();
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
