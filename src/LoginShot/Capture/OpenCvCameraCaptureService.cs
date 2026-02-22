using OpenCvSharp;

namespace LoginShot.Capture;

internal sealed class OpenCvCameraCaptureService : ICameraCaptureService
{
    public Task<CaptureResult> CaptureOnceAsync(CaptureRequest request, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var cameraIndex = request.CameraIndex ?? 0;
            using var capture = new VideoCapture(cameraIndex);
            if (!capture.IsOpened())
            {
                return Task.FromResult(new CaptureResult(false, null, "Unable to open selected camera device.", $"camera-index-{cameraIndex}"));
            }

            using var frame = new Mat();
            if (!capture.Read(frame) || frame.Empty())
            {
                return Task.FromResult(new CaptureResult(false, null, "Unable to read frame from camera.", $"camera-index-{cameraIndex}"));
            }

            using var processed = ResizeIfNeeded(frame, request.MaxWidth);
            var quality = (int)Math.Round(Math.Clamp(request.JpegQuality, 0.0, 1.0) * 100.0);
            var imageParameters = new[]
            {
                new ImageEncodingParam(ImwriteFlags.JpegQuality, quality)
            };

            Cv2.ImEncode(".jpg", processed, out var imageBytes, imageParameters);
            return Task.FromResult(new CaptureResult(true, imageBytes, null, $"camera-index-{cameraIndex}"));
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
