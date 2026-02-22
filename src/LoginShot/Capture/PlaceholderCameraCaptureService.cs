using LoginShot.Triggers;

namespace LoginShot.Capture;

internal sealed class PlaceholderCameraCaptureService : ICameraCaptureService
{
    public Task<CaptureResult> CaptureOnceAsync(CaptureRequest request, CancellationToken cancellationToken)
    {
        var result = new CaptureResult(
            Success: false,
            ImageBytes: null,
            ErrorMessage: "Camera capture is not implemented yet.",
            CameraDeviceName: "unknown");

        return Task.FromResult(result);
    }
}
