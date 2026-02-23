using LoginShot.Capture;
using LoginShot.Config;
using LoginShot.Storage;
using Microsoft.Extensions.Logging;

namespace LoginShot.Triggers;

internal sealed class StartupTriggerDispatcher : ITriggerDispatcher
{
	private readonly ICaptureStorageService captureStorageService;
	private readonly IConfigLoader configLoader;
	private readonly ILogger<StartupTriggerDispatcher> logger;

	public StartupTriggerDispatcher(IConfigLoader configLoader, ILogger<StartupTriggerDispatcher> logger)
	{
		captureStorageService = new CaptureStorageService(new AtomicFileWriter());
		this.configLoader = configLoader;
		this.logger = logger;
	}

	public Task DispatchAsync(SessionEventType eventType, CancellationToken cancellationToken = default)
	{
		return DispatchInternalAsync(eventType, cancellationToken);
	}

	private async Task DispatchInternalAsync(SessionEventType eventType, CancellationToken cancellationToken)
	{
		var config = LoadConfigSafe();
		if (!IsEventEnabled(config, eventType))
		{
			return;
		}

		var cameraCaptureService = CaptureBackendFactory.Create(config.Capture.Backend, message => logger.LogWarning("{Message}", message));
		var captureRequest = new CaptureRequest(
			EventType: eventType,
			MaxWidth: config.Output.MaxWidth,
			JpegQuality: config.Output.JpegQuality,
			CameraIndex: config.Capture.CameraIndex,
			WatermarkEnabled: config.Watermark.Enabled,
			WatermarkFormat: config.Watermark.Format,
			Hostname: Environment.MachineName);

		var captureResult = await cameraCaptureService.CaptureOnceAsync(captureRequest, cancellationToken);
		if (!captureResult.Success)
		{
			logger.LogWarning("Capture failed for {EventType}: {ErrorMessage}", eventType, captureResult.ErrorMessage);
		}
		else if (!string.IsNullOrWhiteSpace(captureResult.ErrorMessage))
		{
			logger.LogWarning("Capture fallback note for {EventType}: {Message}", eventType, captureResult.ErrorMessage);
		}

		var request = new CapturePersistenceRequest(
			TimestampUtc: DateTimeOffset.UtcNow,
			EventType: eventType,
			OutputDirectory: config.Output.Directory,
			Extension: config.Output.Format,
			ImageBytes: captureResult.ImageBytes,
			Failure: captureResult.Success
				? null
				: new CaptureFailureInfo("camera_capture_failed", captureResult.ErrorMessage),
			Hostname: Environment.MachineName,
			Username: Environment.UserName,
			App: GetAppInfo(),
			Camera: new CaptureCameraInfo(captureResult.CameraDeviceName ?? "unknown"),
			WriteSidecar: config.Metadata.WriteSidecar || !captureResult.Success);

		try
		{
			var persistenceResult = await captureStorageService.PersistAsync(request, cancellationToken);
			logger.LogInformation(
				"Capture persisted for {EventType}. sidecar={SidecarPath}, image={ImagePath}",
				eventType,
				persistenceResult.SidecarPath,
				persistenceResult.ImagePath);
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Failed to persist capture attempt for {EventType}", eventType);
		}
	}

	private LoginShotConfig LoadConfigSafe()
	{
		try
		{
			return configLoader.Load();
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Failed to load config for dispatch, using defaults");
			var userProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			var localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			return LoginShotConfigDefaults.Create(userProfilePath, localAppDataPath);
		}
	}

	private static bool IsEventEnabled(LoginShotConfig config, SessionEventType eventType)
	{
		return eventType switch
		{
			SessionEventType.Logon => config.Triggers.OnLogon,
			SessionEventType.Unlock => config.Triggers.OnUnlock,
			SessionEventType.Lock => config.Triggers.OnLock,
			SessionEventType.Manual => true,
			_ => true
		};
	}

	private static CaptureAppInfo GetAppInfo()
	{
		var entryAssembly = typeof(StartupTriggerDispatcher).Assembly;
		var version = entryAssembly.GetName().Version?.ToString() ?? "0.0.0";

		return new CaptureAppInfo(
			Id: "LoginShot",
			Version: version,
			Build: "dev");
	}
}
