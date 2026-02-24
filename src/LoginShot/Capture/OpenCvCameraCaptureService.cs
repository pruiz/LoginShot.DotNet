using LoginShot.Config;
using OpenCvSharp;
using LoginShot.Storage;
using Microsoft.Extensions.Logging;

namespace LoginShot.Capture;

internal sealed class OpenCvCameraCaptureService : ICameraCaptureService
{
	// These conservative defaults intentionally classify near-black captures as failures
	// so users do not get false-positive "success" images with only watermark text.
	private const double BlackFrameMaxLumaThreshold = 25.0;
	private const double BlackFrameMeanLumaThreshold = 18.0;
	private const double BlackFrameDarkPixelRatioThreshold = 0.98;
	private const double BrightPixelLumaThreshold = 20.0;

	private readonly ILogger logger;

	public OpenCvCameraCaptureService(ILogger logger)
	{
		this.logger = logger;
	}

	public async Task<CaptureResult> CaptureOnceAsync(CaptureRequest request, CancellationToken cancellationToken)
	{
		var selectedCameraIndex = request.CameraIndex ?? 0;
		logger.LogInformation(
			"Camera capture start. event={EventType}, selectedCameraIndex={CameraIndex}, watermark={WatermarkEnabled}",
			request.EventType,
			selectedCameraIndex,
			request.WatermarkEnabled);

		var diagnostics = new List<CaptureAttemptDiagnostics>(capacity: 24);
		var overallStopwatch = System.Diagnostics.Stopwatch.StartNew();
		var combinations = BuildCaptureCombinations(request.Negotiation);
		var attemptsPerCombination = request.Negotiation.AttemptsPerCombination;

		try
		{
			foreach (var combination in combinations)
			{
				for (var attempt = 1; attempt <= attemptsPerCombination; attempt++)
				{
					cancellationToken.ThrowIfCancellationRequested();
					var attemptResult = TryCaptureFromIndex(selectedCameraIndex, combination, attempt, request);
					diagnostics.Add(new CaptureAttemptDiagnostics(
						CameraIndex: selectedCameraIndex,
						Backend: combination.BackendName,
						RequestedPixelFormat: combination.PixelFormat,
						RequestedResolution: combination.Resolution,
						RequestedConvertRgbMode: combination.ConvertRgbMode,
						Attempt: attempt,
						DurationMs: attemptResult.DurationMs,
						Outcome: attemptResult.Outcome,
						ActualPixelFormat: attemptResult.ActualPixelFormat,
						ActualWidth: attemptResult.ActualWidth,
						ActualHeight: attemptResult.ActualHeight,
						FrameStats: attemptResult.FrameStats,
						Message: attemptResult.Message));

					logger.LogInformation(
						"Camera capture attempt outcome. index={CameraIndex}, backend={Backend}, attempt={Attempt}, outcome={Outcome}, durationMs={DurationMs}, message={Message}",
						selectedCameraIndex,
						combination.BackendName,
						attempt,
						attemptResult.Outcome,
						attemptResult.DurationMs,
						attemptResult.Message ?? "none");

					logger.LogInformation(
						"Camera negotiation request. index={CameraIndex}, backend={Backend}, pixelFormat={PixelFormat}, resolution={Resolution}, convertRgbMode={ConvertRgbMode}, actualPixelFormat={ActualPixelFormat}, actualWidth={ActualWidth}, actualHeight={ActualHeight}",
						selectedCameraIndex,
						combination.BackendName,
						combination.PixelFormat,
						combination.Resolution,
						combination.ConvertRgbMode,
						attemptResult.ActualPixelFormat ?? "unknown",
						attemptResult.ActualWidth,
						attemptResult.ActualHeight);

					if (attemptResult.FrameStats is not null)
					{
						logger.LogInformation(
							"Camera frame stats. index={CameraIndex}, backend={Backend}, attempt={Attempt}, width={Width}, height={Height}, channels={Channels}, mean={MeanLuma:0.0}, min={MinLuma:0.0}, max={MaxLuma:0.0}, darkRatio={DarkPixelRatio:0.000}, black={IsBlackFrame}",
							selectedCameraIndex,
							combination.BackendName,
							attempt,
							attemptResult.FrameStats.Width,
							attemptResult.FrameStats.Height,
							attemptResult.FrameStats.Channels,
							attemptResult.FrameStats.MeanLuma,
							attemptResult.FrameStats.MinLuma,
							attemptResult.FrameStats.MaxLuma,
							attemptResult.FrameStats.DarkPixelRatio,
							attemptResult.FrameStats.IsBlackFrame);
					}

					if (attemptResult.Success)
					{
						overallStopwatch.Stop();
						logger.LogInformation(
							"Camera capture succeeded. index={CameraIndex}, backend={Backend}, attempts={Attempts}, totalDurationMs={TotalDurationMs}",
							selectedCameraIndex,
							combination.BackendName,
							diagnostics.Count,
							overallStopwatch.ElapsedMilliseconds);

						return new CaptureResult(
							Success: true,
							ImageBytes: attemptResult.ImageBytes,
							ErrorMessage: null,
							CameraDeviceName: $"camera-index-{selectedCameraIndex}",
							Diagnostics: new CaptureDiagnostics(
								SelectedCameraIndex: request.CameraIndex,
								UsedCameraIndex: selectedCameraIndex,
								Backend: combination.BackendName,
								Attempts: diagnostics.Count,
								TotalDurationMs: overallStopwatch.ElapsedMilliseconds,
								FinalFrameStats: attemptResult.FrameStats,
								AttemptDetails: diagnostics,
								FailureCode: null));
					}

					if (attempt < attemptsPerCombination)
					{
						await Task.Delay(TimeSpan.FromMilliseconds(120), cancellationToken);
					}
				}
			}

			overallStopwatch.Stop();
			var lastAttempt = diagnostics.LastOrDefault();
			var failureCode = lastAttempt?.Outcome ?? "capture_failed";
			var failureMessage = lastAttempt?.Message ?? "Unable to capture a usable frame from selected camera.";

			logger.LogWarning(
				"Camera capture failed. index={CameraIndex}, attempts={Attempts}, totalDurationMs={TotalDurationMs}, failureCode={FailureCode}, message={Message}",
				selectedCameraIndex,
				diagnostics.Count,
				overallStopwatch.ElapsedMilliseconds,
				failureCode,
				failureMessage);

			return new CaptureResult(
				Success: false,
				ImageBytes: null,
				ErrorMessage: failureMessage,
				CameraDeviceName: $"camera-index-{selectedCameraIndex}",
				Diagnostics: new CaptureDiagnostics(
					SelectedCameraIndex: request.CameraIndex,
					UsedCameraIndex: selectedCameraIndex,
					Backend: lastAttempt?.Backend ?? "unknown",
					Attempts: diagnostics.Count,
					TotalDurationMs: overallStopwatch.ElapsedMilliseconds,
					FinalFrameStats: lastAttempt?.FrameStats,
					AttemptDetails: diagnostics,
					FailureCode: failureCode));
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception exception)
		{
			overallStopwatch.Stop();
			logger.LogWarning(exception, "Camera capture raised an unhandled exception");
			return new CaptureResult(
				Success: false,
				ImageBytes: null,
				ErrorMessage: exception.Message,
				CameraDeviceName: $"camera-index-{selectedCameraIndex}",
				Diagnostics: new CaptureDiagnostics(
					SelectedCameraIndex: request.CameraIndex,
					UsedCameraIndex: selectedCameraIndex,
					Backend: "unknown",
					Attempts: diagnostics.Count,
					TotalDurationMs: overallStopwatch.ElapsedMilliseconds,
					FinalFrameStats: null,
					AttemptDetails: diagnostics,
					FailureCode: "exception"));
		}
	}

	private static AttemptCaptureResult TryCaptureFromIndex(int cameraIndex, CaptureCombination combination, int attempt, CaptureRequest request)
	{
		var stopwatch = System.Diagnostics.Stopwatch.StartNew();
		try
		{
			using var capture = new VideoCapture(cameraIndex, combination.BackendApi);
			if (!capture.IsOpened())
			{
				return AttemptCaptureResult.Failure("open_failed", "Unable to open selected camera device.", stopwatch.ElapsedMilliseconds, null, null, null, null);
			}

			ApplyCaptureNegotiation(capture, combination);
			var openProperties = ReadOpenProperties(capture);

			using var frame = ReadStabilizedFrame(capture, request.Negotiation.WarmupFrames);
			if (frame.Empty())
			{
				return AttemptCaptureResult.Failure("read_failed", "Unable to read frame from camera.", stopwatch.ElapsedMilliseconds, null, openProperties.PixelFormat, openProperties.Width, openProperties.Height);
			}

			var frameStats = ComputeFrameStats(frame);
			if (frameStats.IsBlackFrame)
			{
				return AttemptCaptureResult.Failure("black_frame", BuildBlackFrameMessage(frameStats), stopwatch.ElapsedMilliseconds, frameStats, openProperties.PixelFormat, openProperties.Width, openProperties.Height);
			}

			using var processed = ResizeIfNeeded(frame, request.MaxWidth);
			if (request.WatermarkEnabled)
			{
				DrawWatermark(processed, request.Hostname, request.WatermarkFormat);
			}

			var quality = (int)Math.Round(Math.Clamp(request.JpegQuality, 0.0, 1.0) * 100.0);
			var imageParameters = new[]
			{
				new ImageEncodingParam(ImwriteFlags.JpegQuality, quality)
			};

			Cv2.ImEncode(".jpg", processed, out var imageBytes, imageParameters);
			if (imageBytes.Length == 0)
			{
				return AttemptCaptureResult.Failure("encode_failed", "Unable to encode captured frame as JPEG.", stopwatch.ElapsedMilliseconds, frameStats, openProperties.PixelFormat, openProperties.Width, openProperties.Height);
			}

			var message = $"Captured frame via {combination.BackendName} (attempt {attempt}).";
			return AttemptCaptureResult.Successful(imageBytes, stopwatch.ElapsedMilliseconds, frameStats, message, openProperties.PixelFormat, openProperties.Width, openProperties.Height);
		}
		catch (Exception exception)
		{
			return AttemptCaptureResult.Failure("exception", exception.Message, stopwatch.ElapsedMilliseconds, null, null, null, null);
		}
	}

	private static Mat ReadStabilizedFrame(VideoCapture capture, int warmupFrames)
	{
		var frameReadCount = Math.Max(1, warmupFrames);
		Mat? latestFrame = null;
		for (var i = 0; i < frameReadCount; i++)
		{
			using var frame = new Mat();
			if (!capture.Read(frame) || frame.Empty())
			{
				continue;
			}

			latestFrame?.Dispose();
			latestFrame = frame.Clone();
		}

		return latestFrame ?? new Mat();
	}

	private static CaptureFrameStats ComputeFrameStats(Mat frame)
	{
		using var grayscale = new Mat();
		if (frame.Channels() == 1)
		{
			frame.CopyTo(grayscale);
		}
		else
		{
			Cv2.CvtColor(frame, grayscale, ColorConversionCodes.BGR2GRAY);
		}

		Cv2.MinMaxLoc(grayscale, out double minLuma, out double maxLuma, out _, out _);
		var meanLuma = Cv2.Mean(grayscale).Val0;

		using var brightMask = new Mat();
		Cv2.Threshold(grayscale, brightMask, BrightPixelLumaThreshold, 255, ThresholdTypes.Binary);
		var totalPixels = grayscale.Rows * grayscale.Cols;
		var brightPixels = Cv2.CountNonZero(brightMask);
		var darkPixelRatio = totalPixels <= 0 ? 1.0 : (totalPixels - brightPixels) / (double)totalPixels;

		var blackFrame =
			maxLuma <= BlackFrameMaxLumaThreshold ||
			meanLuma < BlackFrameMeanLumaThreshold ||
			darkPixelRatio >= BlackFrameDarkPixelRatioThreshold;

		return new CaptureFrameStats(
			Width: frame.Width,
			Height: frame.Height,
			Channels: frame.Channels(),
			MeanLuma: meanLuma,
			MinLuma: minLuma,
			MaxLuma: maxLuma,
			DarkPixelRatio: darkPixelRatio,
			IsBlackFrame: blackFrame);
	}

	private static string BuildBlackFrameMessage(CaptureFrameStats stats)
	{
		return $"Captured frame appears black or near-black. " +
			$"mean={stats.MeanLuma:0.0} (threshold<{BlackFrameMeanLumaThreshold:0.0}), " +
			$"max={stats.MaxLuma:0.0} (threshold<={BlackFrameMaxLumaThreshold:0.0}), " +
			$"darkRatio={stats.DarkPixelRatio:0.000} (threshold>={BlackFrameDarkPixelRatioThreshold:0.000}).";
	}

	private static IReadOnlyList<CaptureCombination> BuildCaptureCombinations(CaptureNegotiationConfig negotiation)
	{
		var result = new List<CaptureCombination>();
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		foreach (var backend in negotiation.BackendOrder)
		{
			if (!TryParseBackend(backend, out var backendApi, out var backendName))
			{
				continue;
			}

			foreach (var pixelFormat in negotiation.PixelFormats)
			{
				var normalizedPixelFormat = string.IsNullOrWhiteSpace(pixelFormat) ? "auto" : pixelFormat.Trim();
				var fourCc = string.Equals(normalizedPixelFormat, "auto", StringComparison.OrdinalIgnoreCase)
					? null
					: normalizedPixelFormat.ToUpperInvariant();

				foreach (var resolution in negotiation.Resolutions)
				{
					var normalizedResolution = string.IsNullOrWhiteSpace(resolution) ? "auto" : resolution.Trim();
					var parsedResolution = ParseResolution(normalizedResolution);

					var convertMode = string.IsNullOrWhiteSpace(negotiation.ConvertRgbMode)
						? "auto"
						: negotiation.ConvertRgbMode.Trim();
					bool? forceConvertRgb = string.Equals(convertMode, "true", StringComparison.OrdinalIgnoreCase)
						? true
						: string.Equals(convertMode, "false", StringComparison.OrdinalIgnoreCase)
							? false
							: null;

					var combination = new CaptureCombination(
						BackendApi: backendApi,
						BackendName: backendName,
						PixelFormat: normalizedPixelFormat,
						Resolution: normalizedResolution,
						ConvertRgbMode: convertMode,
						Width: parsedResolution.Width,
						Height: parsedResolution.Height,
						ForceConvertRgb: forceConvertRgb,
						FourCc: fourCc);

					var key = $"{combination.BackendName}|{combination.PixelFormat}|{combination.Resolution}|{combination.ConvertRgbMode}";
					if (seen.Add(key))
					{
						result.Add(combination);
					}
				}
			}
		}

		if (result.Count == 0)
		{
			result.Add(new CaptureCombination(
				BackendApi: VideoCaptureAPIs.ANY,
				BackendName: "any",
				PixelFormat: "auto",
				Resolution: "auto",
				ConvertRgbMode: "auto",
				Width: null,
				Height: null,
				ForceConvertRgb: null,
				FourCc: null));
		}

		return result;
	}

	private static void ApplyCaptureNegotiation(VideoCapture capture, CaptureCombination combination)
	{
		if (combination.Width.HasValue && combination.Height.HasValue)
		{
			capture.Set(VideoCaptureProperties.FrameWidth, combination.Width.Value);
			capture.Set(VideoCaptureProperties.FrameHeight, combination.Height.Value);
		}

		if (!string.IsNullOrWhiteSpace(combination.FourCc) && combination.FourCc.Length == 4)
		{
			capture.Set(
				VideoCaptureProperties.FourCC,
				VideoWriter.FourCC(
					combination.FourCc[0],
					combination.FourCc[1],
					combination.FourCc[2],
					combination.FourCc[3]));
		}

		if (combination.ForceConvertRgb.HasValue)
		{
			capture.Set(VideoCaptureProperties.ConvertRgb, combination.ForceConvertRgb.Value ? 1 : 0);
		}
	}

	private static CaptureOpenProperties ReadOpenProperties(VideoCapture capture)
	{
		var width = (int)Math.Round(capture.Get(VideoCaptureProperties.FrameWidth));
		var height = (int)Math.Round(capture.Get(VideoCaptureProperties.FrameHeight));
		var fourCcValue = capture.Get(VideoCaptureProperties.FourCC);

		return new CaptureOpenProperties(
			PixelFormat: DecodeFourCc(fourCcValue),
			Width: width > 0 ? width : null,
			Height: height > 0 ? height : null);
	}

	private static string? DecodeFourCc(double fourCcValue)
	{
		if (double.IsNaN(fourCcValue) || Math.Abs(fourCcValue) < double.Epsilon)
		{
			return null;
		}

		var intValue = (int)Math.Round(fourCcValue);
		var chars = new[]
		{
			(char)(intValue & 0xFF),
			(char)((intValue >> 8) & 0xFF),
			(char)((intValue >> 16) & 0xFF),
			(char)((intValue >> 24) & 0xFF)
		};

		return new string(chars).TrimEnd('\0').Trim();
	}

	private static bool TryParseBackend(string backend, out VideoCaptureAPIs backendApi, out string backendName)
	{
		if (string.Equals(backend, "dshow", StringComparison.OrdinalIgnoreCase))
		{
			backendApi = VideoCaptureAPIs.DSHOW;
			backendName = "dshow";
			return true;
		}

		if (string.Equals(backend, "msmf", StringComparison.OrdinalIgnoreCase))
		{
			backendApi = VideoCaptureAPIs.MSMF;
			backendName = "msmf";
			return true;
		}

		if (string.Equals(backend, "any", StringComparison.OrdinalIgnoreCase))
		{
			backendApi = VideoCaptureAPIs.ANY;
			backendName = "any";
			return true;
		}

		backendApi = VideoCaptureAPIs.ANY;
		backendName = "any";
		return false;
	}

	private static (int? Width, int? Height) ParseResolution(string resolution)
	{
		if (string.Equals(resolution, "auto", StringComparison.OrdinalIgnoreCase))
		{
			return (null, null);
		}

		var separatorIndex = resolution.IndexOfAny(['x', 'X']);
		if (separatorIndex <= 0 || separatorIndex >= resolution.Length - 1)
		{
			return (null, null);
		}

		var widthText = resolution[..separatorIndex];
		var heightText = resolution[(separatorIndex + 1)..];
		if (!int.TryParse(widthText, out var width) || !int.TryParse(heightText, out var height) || width <= 0 || height <= 0)
		{
			return (null, null);
		}

		return (width, height);
	}

	private static void DrawWatermark(Mat image, string hostname, string format)
	{
		var timestamp = DateTimeOffset.Now;
		string formattedTimestamp;
		try
		{
			formattedTimestamp = timestamp.ToString(format);
		}
		catch (FormatException)
		{
			formattedTimestamp = timestamp.ToString("yyyy-MM-dd HH:mm:ss zzz");
		}

		var text = $"{hostname} {formattedTimestamp}";

		var fontFace = HersheyFonts.HersheySimplex;
		var thickness = Math.Max(1, image.Width / 600);
		var fontScale = Math.Clamp(image.Width / 1200.0, 0.45, 1.2);
		var baseline = 0;
		var textSize = Cv2.GetTextSize(text, fontFace, fontScale, thickness, out baseline);

		var margin = Math.Max(8, image.Width / 100);
		var x = Math.Max(margin, image.Width - textSize.Width - margin);
		var y = Math.Max(textSize.Height + margin, image.Height - margin);

		var outlineThickness = thickness + 2;
		Cv2.PutText(
			image,
			text,
			new OpenCvSharp.Point(x, y),
			fontFace,
			fontScale,
			new Scalar(0, 0, 0),
			outlineThickness,
			LineTypes.AntiAlias);

		Cv2.PutText(
			image,
			text,
			new OpenCvSharp.Point(x, y),
			fontFace,
			fontScale,
			new Scalar(240, 240, 240),
			thickness,
			LineTypes.AntiAlias);
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

	private sealed record AttemptCaptureResult(
		bool Success,
		byte[]? ImageBytes,
		string Outcome,
		string? Message,
		long DurationMs,
		CaptureFrameStats? FrameStats,
		string? ActualPixelFormat,
		int? ActualWidth,
		int? ActualHeight)
	{
		public static AttemptCaptureResult Failure(
			string outcome,
			string message,
			long durationMs,
			CaptureFrameStats? frameStats,
			string? actualPixelFormat,
			int? actualWidth,
			int? actualHeight)
		{
			return new AttemptCaptureResult(false, null, outcome, message, durationMs, frameStats, actualPixelFormat, actualWidth, actualHeight);
		}

		public static AttemptCaptureResult Successful(
			byte[] imageBytes,
			long durationMs,
			CaptureFrameStats frameStats,
			string message,
			string? actualPixelFormat,
			int? actualWidth,
			int? actualHeight)
		{
			return new AttemptCaptureResult(true, imageBytes, "success", message, durationMs, frameStats, actualPixelFormat, actualWidth, actualHeight);
		}
	}

	private sealed record CaptureCombination(
		VideoCaptureAPIs BackendApi,
		string BackendName,
		string PixelFormat,
		string Resolution,
		string ConvertRgbMode,
		int? Width,
		int? Height,
		bool? ForceConvertRgb,
		string? FourCc);

	private sealed record CaptureOpenProperties(string? PixelFormat, int? Width, int? Height);
}
