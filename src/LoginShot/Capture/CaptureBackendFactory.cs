using Microsoft.Extensions.Logging;

namespace LoginShot.Capture;

internal static class CaptureBackendFactory
{
	public static ICameraCaptureService Create(string backend, ILogger logger)
	{
		if (string.Equals(backend, "opencv", StringComparison.OrdinalIgnoreCase))
		{
			return new OpenCvCameraCaptureService(logger);
		}

		if (string.Equals(backend, "winrt-mediacapture", StringComparison.OrdinalIgnoreCase))
		{
			// TODO: Implement WinRT MediaCapture backend and select it via capture.backend.
			logger.LogWarning("TODO: implement WinRT MediaCapture backend. Falling back to OpenCV.");
			return new OpenCvCameraCaptureService(logger);
		}

		logger.LogWarning("Unknown capture backend '{Backend}'. Falling back to OpenCV.", backend);
		return new OpenCvCameraCaptureService(logger);
	}
}
