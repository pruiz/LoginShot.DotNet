using System.Diagnostics;
using LoginShot.Capture;
using LoginShot.Config;
using LoginShot.Storage;

namespace LoginShot.Triggers;

internal sealed class StartupTriggerDispatcher : ITriggerDispatcher
{
    private readonly ICaptureStorageService captureStorageService;
    private readonly IConfigLoader configLoader;

    public StartupTriggerDispatcher()
    {
        captureStorageService = new CaptureStorageService(new AtomicFileWriter());
        configLoader = CreateConfigLoader();
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

        var cameraCaptureService = CaptureBackendFactory.Create(config.Capture.Backend, message => Debug.WriteLine(message));
        var captureRequest = new CaptureRequest(
            EventType: eventType,
            MaxWidth: config.Output.MaxWidth,
            JpegQuality: config.Output.JpegQuality,
            CameraIndex: config.Capture.CameraIndex);

        var captureResult = await cameraCaptureService.CaptureOnceAsync(captureRequest, cancellationToken);
        if (!captureResult.Success)
        {
            Debug.WriteLine($"Capture failed for {eventType}: {captureResult.ErrorMessage}");
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
            Debug.WriteLine($"Capture persisted for {eventType}: sidecar={persistenceResult.SidecarPath}, image={persistenceResult.ImagePath}");
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Failed to persist capture attempt for {eventType}: {exception.Message}");
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
            Debug.WriteLine($"Failed to load config for dispatch, using defaults: {exception.Message}");
            var userProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return LoginShotConfigDefaults.Create(userProfilePath);
        }
    }

    private static IConfigLoader CreateConfigLoader()
    {
        var userProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var fileProvider = new SystemConfigFileProvider();
        var pathResolver = new ConfigPathResolver(userProfilePath, appDataPath, fileProvider);
        return new LoginShotConfigLoader(pathResolver, fileProvider);
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
