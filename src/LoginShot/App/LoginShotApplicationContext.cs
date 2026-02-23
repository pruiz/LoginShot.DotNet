using System.Diagnostics;
using System.Drawing;
using System.IO;
using LoginShot.Capture;
using LoginShot.Config;
using LoginShot.Startup;
using LoginShot.Triggers;
using LoginShot.Util;
using Microsoft.Extensions.Logging;

namespace LoginShot.App;

internal sealed class LoginShotApplicationContext : ApplicationContext
{
	private const int CAMERA_INDEX_PROBE_COUNT = 10;
	private static readonly TimeSpan CameraRefreshInterval = TimeSpan.FromSeconds(15);

	private readonly ContextMenuStrip menu;
	private readonly NotifyIcon trayIcon;
	private readonly ToolStripMenuItem cameraMenuItem;
	private readonly ToolStripMenuItem startAfterLoginMenuItem;
	private readonly ITriggerDispatcher triggerDispatcher;
	private readonly IStartupRegistrationService startupRegistrationService;
	private readonly IConfigLoader configLoader;
	private readonly IConfigWriter configWriter;
	private readonly ISessionEventSource sessionEventSource;
	private readonly SessionEventRouter sessionEventRouter;
	private readonly ILogger<LoginShotApplicationContext> logger;
	private readonly SynchronizationContext uiContext;
	private readonly CameraIndexCacheService cameraIndexCacheService;
	private readonly CameraSelectionService cameraSelectionService;
	private readonly ConfigReloadCoordinator configReloadCoordinator;
	private LoginShotConfig currentConfig;

	public LoginShotApplicationContext(
		ITriggerDispatcher triggerDispatcher,
		IConfigLoader configLoader,
		IConfigWriter configWriter,
		ICameraDeviceEnumerator cameraDeviceEnumerator,
		ILogger<LoginShotApplicationContext> logger,
		ILogger<SessionEventRouter> sessionEventRouterLogger)
	{
		uiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
		this.triggerDispatcher = triggerDispatcher;
		this.configLoader = configLoader;
		this.configWriter = configWriter;
		this.logger = logger;
		currentConfig = LoadConfigOnStartup(this.configLoader);

		cameraIndexCacheService = new CameraIndexCacheService(
			cameraDeviceEnumerator,
			logger,
			CAMERA_INDEX_PROBE_COUNT,
			CameraRefreshInterval);
		cameraSelectionService = new CameraSelectionService(configWriter, logger);
		configReloadCoordinator = new ConfigReloadCoordinator(
			uiContext,
			this.configLoader.Load,
			OnConfigReloadSucceeded,
			OnConfigReloadFailed,
			OnConfigWatcherError,
			logger);

		sessionEventRouter = CreateSessionEventRouter(triggerDispatcher, currentConfig, sessionEventRouterLogger);
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
		menu.Items.Add(new ToolStripMenuItem("Open log", null, OnOpenLogClicked));
		menu.Items.Add(cameraMenuItem);
		menu.Items.Add(startAfterLoginMenuItem);
		menu.Items.Add(new ToolStripSeparator());
		menu.Items.Add(new ToolStripMenuItem("Edit config", null, OnEditConfigClicked));
		menu.Items.Add(new ToolStripMenuItem("Reload config", null, OnReloadConfigClicked));
		menu.Items.Add(new ToolStripMenuItem("Generate sample config", null, OnGenerateSampleConfigClicked));
		menu.Items.Add(new ToolStripSeparator());
		menu.Items.Add(new ToolStripMenuItem("Quit", null, OnQuitClicked));

		trayIcon = new NotifyIcon
		{
			Icon = LoadTrayIcon(),
			Text = "LoginShot",
			ContextMenuStrip = menu,
			Visible = true
		};

		RefreshCameraMenuItems();
		configReloadCoordinator.Bind(currentConfig.SourcePath);
	}

	private async void OnCaptureNowClicked(object? sender, EventArgs eventArgs)
	{
		try
		{
			await triggerDispatcher.DispatchAsync(SessionEventType.Manual, CancellationToken.None);
			logger.LogInformation("Manual capture requested from tray menu");
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Manual capture dispatch failed");
			MessageBox.Show(
				$"Manual capture failed: {exception.Message}",
				"LoginShot",
				MessageBoxButtons.OK,
				MessageBoxIcon.Error);
		}
	}

	private void OnCameraMenuOpening(object? sender, EventArgs eventArgs)
	{
		RefreshCameraMenuItems();
	}

	private void OnReloadConfigClicked(object? sender, EventArgs eventArgs)
	{
		ReloadConfiguration(notifyOnSuccess: true, autoReload: false);
	}

	private void OnEditConfigClicked(object? sender, EventArgs eventArgs)
	{
		try
		{
			var configPath = EnsureConfigFileExists();

			Process.Start(new ProcessStartInfo
			{
				FileName = configPath,
				UseShellExecute = true
			});

			logger.LogInformation("Opened config file in editor at {ConfigPath}", configPath);
			ShowBalloon("Config", "Opened configuration file in your default editor.", ToolTipIcon.Info);
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Failed to open configuration file in editor");
			ShowBalloon("Config error", $"Failed to open config: {exception.Message}", ToolTipIcon.Warning);
		}
	}

	private async void OnVerifySelectedCameraClicked(object? sender, EventArgs eventArgs)
	{
		try
		{
			var captureService = CaptureBackendFactory.Create(currentConfig.Capture.Backend, message => logger.LogWarning("{Message}", message));
			var request = new CaptureRequest(
				EventType: SessionEventType.Manual,
				MaxWidth: currentConfig.Output.MaxWidth,
				JpegQuality: currentConfig.Output.JpegQuality,
				CameraIndex: currentConfig.Capture.CameraIndex,
				WatermarkEnabled: currentConfig.Watermark.Enabled,
				WatermarkFormat: currentConfig.Watermark.Format,
				Hostname: Environment.MachineName);

			var result = await captureService.CaptureOnceAsync(request, CancellationToken.None);
			if (result.Success)
			{
				logger.LogInformation(
					"Camera verification succeeded for backend {Backend} and cameraIndex {CameraIndex}",
					currentConfig.Capture.Backend,
					currentConfig.Capture.CameraIndex);
				MessageBox.Show(
					"Camera verification succeeded.",
					"LoginShot",
					MessageBoxButtons.OK,
					MessageBoxIcon.Information);
				return;
			}

			logger.LogWarning("Camera verification failed: {ErrorMessage}", result.ErrorMessage);
			MessageBox.Show(
				$"Camera verification failed: {result.ErrorMessage}",
				"LoginShot",
				MessageBoxButtons.OK,
				MessageBoxIcon.Warning);
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Camera verification raised an exception");
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
		logger.LogInformation("Opening output folder {OutputDirectory}", outputDirectory);

		var processStartInfo = new ProcessStartInfo
		{
			FileName = outputDirectory,
			UseShellExecute = true
		};

		Process.Start(processStartInfo);
	}

	private void OnOpenLogClicked(object? sender, EventArgs eventArgs)
	{
		try
		{
			Directory.CreateDirectory(currentConfig.Logging.Directory);
			var logPath = LogFilePathProvider.GetDailyLogFilePath(currentConfig.Logging.Directory, DateTimeOffset.Now);

			using (new FileStream(logPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
			{
			}

			logger.LogInformation("Opening log file {LogPath}", logPath);
			Process.Start(new ProcessStartInfo
			{
				FileName = logPath,
				UseShellExecute = true
			});
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Failed to open current log file");
			ShowBalloon("Log error", $"Failed to open log file: {exception.Message}", ToolTipIcon.Warning);
		}
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
			logger.LogWarning(exception, "Failed to update startup registration setting");
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
		configReloadCoordinator.Dispose();
		sessionEventSource.Dispose();
		trayIcon.Visible = false;
		trayIcon.Dispose();
		menu.Dispose();
		ExitThread();
	}

	private void OnSessionEventReceived(object? sender, SessionEventType eventType)
	{
		logger.LogInformation("Received session event {EventType}", eventType);
		_ = sessionEventRouter.HandleEventAsync(eventType);
	}

	private static IStartupRegistrationService CreateStartupRegistrationService()
	{
		var executablePath = Application.ExecutablePath;
		var startupDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
		var legacyShortcutPath = Path.Combine(startupDirectory, "LoginShot.lnk");
		var schedulerClient = new TaskSchedulerStartupTaskSchedulerClient();
		var fileSystem = new SystemFileSystem();

		return new TaskSchedulerStartupRegistrationService(
			"LoginShot.StartAfterLogin",
			executablePath,
			"--startup-trigger=logon",
			legacyShortcutPath,
			schedulerClient,
			fileSystem);
	}

	private void RefreshCameraMenuItems()
	{
		var snapshot = cameraIndexCacheService.GetSnapshotAndRefreshIfNeeded(
			() => uiContext.Post(_ => RefreshCameraMenuItems(), null));
		var detectedDevices = snapshot.Devices;
		var refreshing = snapshot.IsRefreshing;

		cameraMenuItem.DropDownItems.Clear();

		var autoItem = new ToolStripMenuItem("Auto (default)")
		{
			Checked = currentConfig.Capture.CameraIndex is null
		};
		autoItem.Click += (_, _) => ApplyCameraSelection(null);
		cameraMenuItem.DropDownItems.Add(autoItem);

		if (refreshing)
		{
			cameraMenuItem.DropDownItems.Add(new ToolStripSeparator());
			cameraMenuItem.DropDownItems.Add(new ToolStripMenuItem("Detecting cameras...")
			{
				Enabled = false
			});
		}

		if (detectedDevices.Count > 0)
		{
			cameraMenuItem.DropDownItems.Add(new ToolStripSeparator());
		}

		foreach (var device in detectedDevices)
		{
			var item = new ToolStripMenuItem(BuildCameraMenuLabel(device))
			{
				Checked = currentConfig.Capture.CameraIndex == device.Index,
				Tag = device.Index
			};
			item.Click += (_, _) => ApplyCameraSelection(device.Index);
			cameraMenuItem.DropDownItems.Add(item);
		}

		cameraMenuItem.DropDownItems.Add(new ToolStripSeparator());
		cameraMenuItem.DropDownItems.Add(new ToolStripMenuItem("Verify selected camera", null, OnVerifySelectedCameraClicked));
	}

	private static string BuildCameraMenuLabel(CameraDeviceDescriptor device)
	{
		if (string.IsNullOrWhiteSpace(device.Name))
		{
			return $"Camera {device.Index}";
		}

		return $"Camera {device.Index} - {device.Name}";
	}

	private void ApplyCameraSelection(int? cameraIndex)
	{
		if (!cameraSelectionService.TryApplySelection(currentConfig, cameraIndex, out var updatedConfig, out var errorMessage))
		{
			MessageBox.Show(
				$"Failed to save camera selection: {errorMessage}",
				"LoginShot",
				MessageBoxButtons.OK,
				MessageBoxIcon.Error);
			return;
		}

		currentConfig = updatedConfig;
		configReloadCoordinator.Bind(currentConfig.SourcePath);

		RefreshCameraMenuItems();
	}

	private static LoginShotConfig LoadConfigOnStartup(IConfigLoader loader)
	{
		return loader.Load();
	}

	private string EnsureConfigFileExists()
	{
		if (!string.IsNullOrWhiteSpace(currentConfig.SourcePath))
		{
			return currentConfig.SourcePath;
		}

		var savedPath = configWriter.Save(currentConfig, null);
		currentConfig = currentConfig with { SourcePath = savedPath };
		configReloadCoordinator.Bind(savedPath);
		logger.LogInformation("Created configuration file at {ConfigPath}", savedPath);
		return savedPath;
	}

	private void ReloadConfiguration(bool notifyOnSuccess, bool autoReload)
	{
		configReloadCoordinator.RequestReload(notifyOnSuccess, autoReload);
	}

	private void OnConfigReloadSucceeded(LoginShotConfig reloadedConfig, bool notifyOnSuccess, bool autoReload)
	{
		currentConfig = reloadedConfig;
		sessionEventRouter.UpdateOptions(CreateTriggerHandlingOptions(currentConfig));

		logger.LogInformation("Configuration reloaded from {ConfigPath}", currentConfig.SourcePath ?? "defaults");
		if (notifyOnSuccess)
		{
			ShowBalloon(
				"Config",
				autoReload
					? "Configuration changes were detected and reloaded."
					: "Configuration reloaded successfully.",
				ToolTipIcon.Info);
		}
	}

	private void OnConfigReloadFailed(Exception exception, bool _notifyOnSuccess, bool _autoReload)
	{
		logger.LogWarning(exception, "Failed to reload configuration");
		ShowBalloon(
			"Config error",
			$"Config reload failed: {exception.Message}. Keeping previous valid configuration.",
			ToolTipIcon.Warning);
	}

	private void OnConfigWatcherError(Exception exception)
	{
		logger.LogWarning(exception, "Config file watcher encountered an error");
		ShowBalloon(
			"Config watcher",
			"Config watcher encountered an error; automatic reload may be delayed.",
			ToolTipIcon.Warning);
	}

	private void ShowBalloon(string title, string text, ToolTipIcon icon)
	{
		var clipped = text.Length > 220 ? text[..220] + "..." : text;
		trayIcon.BalloonTipTitle = title;
		trayIcon.BalloonTipText = clipped;
		trayIcon.BalloonTipIcon = icon;
		trayIcon.ShowBalloonTip(5000);
	}

	private static SessionEventRouter CreateSessionEventRouter(ITriggerDispatcher triggerDispatcher, LoginShotConfig config, ILogger<SessionEventRouter> logger)
	{
		var debouncer = new PerEventTypeDebouncer();
		var timeProvider = new SystemEventTimeProvider();
		var options = CreateTriggerHandlingOptions(config);
		return new SessionEventRouter(triggerDispatcher, debouncer, timeProvider, logger, options);
	}

	private static TriggerHandlingOptions CreateTriggerHandlingOptions(LoginShotConfig config)
	{
		return new TriggerHandlingOptions(
			config.Triggers.OnUnlock,
			config.Triggers.OnLock,
			TimeSpan.FromSeconds(config.Capture.DebounceSeconds));
	}

	private Icon LoadTrayIcon()
	{
		try
		{
			var icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
			if (icon is not null)
			{
				logger.LogInformation("Loaded tray icon from executable resources");
				return icon;
			}
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Failed to load tray icon from executable resources; using fallback icon");
		}

		return SystemIcons.Application;
	}

}
