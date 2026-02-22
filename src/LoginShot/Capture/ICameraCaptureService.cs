namespace LoginShot.Capture;

internal interface ICameraCaptureService
{
    Task<CaptureResult> CaptureOnceAsync(string eventName, CancellationToken cancellationToken);
}

internal sealed record CaptureResult(bool Success, string? ImagePath, string? ErrorMessage);
