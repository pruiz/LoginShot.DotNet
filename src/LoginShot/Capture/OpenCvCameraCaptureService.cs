using OpenCvSharp;

namespace LoginShot.Capture;

internal sealed class OpenCvCameraCaptureService : ICameraCaptureService
{
    public Task<CaptureResult> CaptureOnceAsync(CaptureRequest request, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var capture = new VideoCapture(0);
            if (!capture.IsOpened())
            {
                return Task.FromResult(new CaptureResult(false, null, "Unable to open default camera device.", "camera-index-0"));
            }

            using var frame = new Mat();
            if (!capture.Read(frame) || frame.Empty())
            {
                return Task.FromResult(new CaptureResult(false, null, "Unable to read frame from camera.", "camera-index-0"));
            }

            using var processed = ResizeIfNeeded(frame, request.MaxWidth);
            var quality = (int)Math.Round(Math.Clamp(request.JpegQuality, 0.0, 1.0) * 100.0);
            var imageParameters = new[]
            {
                new ImageEncodingParam(ImwriteFlags.JpegQuality, quality)
            };

            Cv2.ImEncode(".jpg", processed, out var imageBytes, imageParameters);
            return Task.FromResult(new CaptureResult(true, imageBytes, null, "camera-index-0"));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Task.FromResult(new CaptureResult(false, null, exception.Message, "camera-index-0"));
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
