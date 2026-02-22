using OpenCvSharp;

namespace LoginShot.Capture;

internal sealed class OpenCvCameraCaptureService : ICameraCaptureService
{
    public Task<CaptureResult> CaptureOnceAsync(CaptureRequest request, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var preferredIndex = request.CameraIndex ?? 0;
            var preferredAttempt = TryCaptureFromIndex(preferredIndex, request.MaxWidth, request.JpegQuality);
            if (preferredAttempt.Success)
            {
                return Task.FromResult(preferredAttempt);
            }

            if (request.CameraIndex is > 0)
            {
                var fallbackAttempt = TryCaptureFromIndex(0, request.MaxWidth, request.JpegQuality);
                if (fallbackAttempt.Success)
                {
                    return Task.FromResult(new CaptureResult(
                        Success: true,
                        ImageBytes: fallbackAttempt.ImageBytes,
                        ErrorMessage: $"Preferred camera index {request.CameraIndex} failed; fell back to camera-index-0",
                        CameraDeviceName: fallbackAttempt.CameraDeviceName));
                }

                return Task.FromResult(new CaptureResult(
                    false,
                    null,
                    $"Failed preferred camera index {request.CameraIndex}: {preferredAttempt.ErrorMessage}. Fallback camera-index-0 also failed: {fallbackAttempt.ErrorMessage}",
                    $"camera-index-{request.CameraIndex}"));
            }

            return Task.FromResult(preferredAttempt);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            var cameraIndex = request.CameraIndex ?? 0;
            return Task.FromResult(new CaptureResult(false, null, exception.Message, $"camera-index-{cameraIndex}"));
        }
    }

    private static CaptureResult TryCaptureFromIndex(int cameraIndex, int? maxWidth, double jpegQuality)
    {
        using var capture = new VideoCapture(cameraIndex);
        if (!capture.IsOpened())
        {
            return new CaptureResult(false, null, "Unable to open selected camera device.", $"camera-index-{cameraIndex}");
        }

        using var frame = new Mat();
        if (!capture.Read(frame) || frame.Empty())
        {
            return new CaptureResult(false, null, "Unable to read frame from camera.", $"camera-index-{cameraIndex}");
        }

        using var processed = ResizeIfNeeded(frame, maxWidth);
        var quality = (int)Math.Round(Math.Clamp(jpegQuality, 0.0, 1.0) * 100.0);
        var imageParameters = new[]
        {
            new ImageEncodingParam(ImwriteFlags.JpegQuality, quality)
        };

        Cv2.ImEncode(".jpg", processed, out var imageBytes, imageParameters);
        return new CaptureResult(true, imageBytes, null, $"camera-index-{cameraIndex}");
    }

    private static Mat ResizeIfNeeded(Mat source, int? maxWidth)
    {
        if (maxWidth is null || maxWidth <= 0 || source.Width <= maxWidth)
        {
            return source.Clone();
        }

        var width = maxWidth.Value;
        var height = (int)Math.Round(source.Height * (width / (double)source.Width));
        var resized = new Mat();
        Cv2.Resize(source, resized, new OpenCvSharp.Size(width, height));
        return resized;
    }
}
