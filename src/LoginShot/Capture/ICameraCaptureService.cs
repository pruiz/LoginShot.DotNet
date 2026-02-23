using LoginShot.Triggers;
using LoginShot.Storage;

namespace LoginShot.Capture;

internal sealed record CaptureRequest(
	SessionEventType EventType,
	int? MaxWidth,
	double JpegQuality,
	int? CameraIndex,
	bool WatermarkEnabled,
	string WatermarkFormat,
	string Hostname);

internal interface ICameraCaptureService
{
	Task<CaptureResult> CaptureOnceAsync(CaptureRequest request, CancellationToken cancellationToken);
}

internal sealed record CaptureResult(
	bool Success,
	byte[]? ImageBytes,
	string? ErrorMessage,
	string? CameraDeviceName,
	CaptureDiagnostics? Diagnostics);
