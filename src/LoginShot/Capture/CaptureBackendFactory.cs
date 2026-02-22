namespace LoginShot.Capture;

internal static class CaptureBackendFactory
{
    public static ICameraCaptureService Create(string backend, Action<string>? log)
    {
        if (string.Equals(backend, "opencv", StringComparison.OrdinalIgnoreCase))
        {
            return new OpenCvCameraCaptureService();
        }

        if (string.Equals(backend, "winrt-mediacapture", StringComparison.OrdinalIgnoreCase))
        {
            // TODO: Implement WinRT MediaCapture backend and select it via capture.backend.
            log?.Invoke("TODO: implement WinRT MediaCapture backend. Falling back to OpenCV.");
            return new OpenCvCameraCaptureService();
        }

        log?.Invoke($"Unknown capture backend '{backend}'. Falling back to OpenCV.");
        return new OpenCvCameraCaptureService();
    }
}
