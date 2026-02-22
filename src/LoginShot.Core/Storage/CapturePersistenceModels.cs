using LoginShot.Triggers;

namespace LoginShot.Storage;

public sealed record CaptureAppInfo(string Id, string Version, string Build);

public sealed record CaptureCameraInfo(string DeviceName);

public sealed record CaptureFailureInfo(string Reason, string? Message);

public sealed record CapturePersistenceRequest(
    DateTimeOffset TimestampUtc,
    SessionEventType EventType,
    string OutputDirectory,
    string Extension,
    byte[]? ImageBytes,
    CaptureFailureInfo? Failure,
    string Hostname,
    string Username,
    CaptureAppInfo App,
    CaptureCameraInfo Camera,
    bool WriteSidecar);

public sealed record CapturePersistenceResult(bool ImageWritten, string? ImagePath, string? SidecarPath);
