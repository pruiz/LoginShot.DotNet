using LoginShot.Triggers;

namespace LoginShot.Storage;

public sealed record CaptureAppInfo(string Id, string Version, string Build);

public sealed record CaptureFrameStats(
	int Width,
	int Height,
	int Channels,
	double MeanLuma,
	double MinLuma,
	double MaxLuma,
	double DarkPixelRatio,
	bool IsBlackFrame);

public sealed record CaptureAttemptDiagnostics(
	int CameraIndex,
	string Backend,
	int Attempt,
	long DurationMs,
	string Outcome,
	CaptureFrameStats? FrameStats,
	string? Message);

public sealed record CaptureDiagnostics(
	int? SelectedCameraIndex,
	int UsedCameraIndex,
	string Backend,
	int Attempts,
	long TotalDurationMs,
	CaptureFrameStats? FinalFrameStats,
	IReadOnlyList<CaptureAttemptDiagnostics> AttemptDetails,
	string? FailureCode);

public sealed record CaptureCameraInfo(string DeviceName, CaptureDiagnostics? Diagnostics);

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
