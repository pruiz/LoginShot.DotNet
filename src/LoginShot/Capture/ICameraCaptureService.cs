using LoginShot.Triggers;

namespace LoginShot.Capture;

internal interface ICameraCaptureService
{
    Task<CaptureResult> CaptureOnceAsync(SessionEventType eventType, CancellationToken cancellationToken);
}

internal sealed record CaptureResult(bool Success, byte[]? ImageBytes, string? ErrorMessage, string? CameraDeviceName);
