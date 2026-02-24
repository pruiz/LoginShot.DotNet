using System.Text.Json;
using LoginShot.Storage;
using LoginShot.Triggers;

namespace LoginShot.Core.Tests;

public class CaptureStorageServiceTests
{
	[Test]
	public async Task PersistAsync_WhenCaptureSucceeds_WritesImageAndSidecar()
	{
		var tempRoot = CreateTempDirectory();
		try
		{
			var service = new CaptureStorageService(new AtomicFileWriter());
			var request = CreateRequest(tempRoot, SessionEventType.Unlock, new byte[] { 1, 2, 3 }, failure: null, writeSidecar: true);

			var result = await service.PersistAsync(request);

			Assert.Multiple(() =>
			{
				Assert.That(result.ImageWritten, Is.True);
				Assert.That(result.ImagePath, Is.Not.Null);
				Assert.That(result.SidecarPath, Is.Not.Null);
				Assert.That(File.Exists(result.ImagePath!), Is.True);
				Assert.That(File.Exists(result.SidecarPath!), Is.True);
			});

			var sidecarText = File.ReadAllText(result.SidecarPath!);
			using var document = JsonDocument.Parse(sidecarText);
			Assert.Multiple(() =>
			{
				Assert.That(document.RootElement.GetProperty("status").GetString(), Is.EqualTo("success"));
				Assert.That(document.RootElement.GetProperty("event").GetString(), Is.EqualTo("unlock"));
				Assert.That(document.RootElement.GetProperty("outputPath").GetString(), Is.EqualTo(result.ImagePath));
			});
		}
		finally
		{
			Directory.Delete(tempRoot, recursive: true);
		}
	}

	[Test]
	public async Task PersistAsync_WhenCaptureFails_WritesFailureSidecarOnly()
	{
		var tempRoot = CreateTempDirectory();
		try
		{
			var service = new CaptureStorageService(new AtomicFileWriter());
			var request = CreateRequest(
				tempRoot,
				SessionEventType.Lock,
				imageBytes: null,
				failure: new CaptureFailureInfo("camera_capture_failed", "Device unavailable"),
				writeSidecar: true);

			var result = await service.PersistAsync(request);

			Assert.Multiple(() =>
			{
				Assert.That(result.ImageWritten, Is.False);
				Assert.That(result.ImagePath, Is.Null);
				Assert.That(result.SidecarPath, Is.Not.Null);
				Assert.That(File.Exists(result.SidecarPath!), Is.True);
			});

			var sidecarText = File.ReadAllText(result.SidecarPath!);
			using var document = JsonDocument.Parse(sidecarText);
			Assert.Multiple(() =>
			{
				Assert.That(document.RootElement.GetProperty("status").GetString(), Is.EqualTo("failure"));
				Assert.That(document.RootElement.GetProperty("event").GetString(), Is.EqualTo("lock"));
				Assert.That(document.RootElement.GetProperty("outputPath").ValueKind, Is.EqualTo(JsonValueKind.Null));
				Assert.That(document.RootElement.GetProperty("failure").GetProperty("reason").GetString(), Is.EqualTo("camera_capture_failed"));
			});
		}
		finally
		{
			Directory.Delete(tempRoot, recursive: true);
		}
	}

	[Test]
	public async Task PersistAsync_WhenCameraDiagnosticsProvided_WritesDiagnosticsToSidecar()
	{
		var tempRoot = CreateTempDirectory();
		try
		{
			var service = new CaptureStorageService(new AtomicFileWriter());
			var diagnostics = new CaptureDiagnostics(
				SelectedCameraIndex: 1,
				UsedCameraIndex: 1,
				Backend: "dshow",
				Attempts: 2,
				TotalDurationMs: 220,
				FinalFrameStats: new CaptureFrameStats(1280, 720, 3, 35.2, 0.0, 240.0, 0.72, false),
				AttemptDetails: new[]
				{
					new CaptureAttemptDiagnostics(1, "dshow", "MJPG", "1280x720", "auto", 1, 100, "read_failed", "MJPG", 1280, 720, null, "Unable to read frame from camera."),
					new CaptureAttemptDiagnostics(1, "dshow", "MJPG", "1280x720", "auto", 2, 120, "success", "MJPG", 1280, 720, new CaptureFrameStats(1280, 720, 3, 35.2, 0.0, 240.0, 0.72, false), "Captured frame via dshow (attempt 2).")
				},
				FailureCode: null);

			var request = CreateRequest(tempRoot, SessionEventType.Manual, new byte[] { 9, 8, 7 }, failure: null, writeSidecar: true, diagnostics: diagnostics);

			var result = await service.PersistAsync(request);

			var sidecarText = File.ReadAllText(result.SidecarPath!);
			using var document = JsonDocument.Parse(sidecarText);
			var diagnosticsElement = document.RootElement
				.GetProperty("camera")
				.GetProperty("diagnostics");

			Assert.Multiple(() =>
			{
				Assert.That(diagnosticsElement.GetProperty("backend").GetString(), Is.EqualTo("dshow"));
				Assert.That(diagnosticsElement.GetProperty("attempts").GetInt32(), Is.EqualTo(2));
				Assert.That(diagnosticsElement.GetProperty("attemptDetails").GetArrayLength(), Is.EqualTo(2));
			});
		}
		finally
		{
			Directory.Delete(tempRoot, recursive: true);
		}
	}

	[Test]
	public void AtomicFileWriter_DoesNotLeaveTempFilesAfterWrite()
	{
		var tempRoot = CreateTempDirectory();
		try
		{
			var writer = new AtomicFileWriter();
			var filePath = Path.Combine(tempRoot, "output.json");

			writer.WriteAllTextAtomic(filePath, "{}");

			var files = Directory.GetFiles(tempRoot);
			Assert.Multiple(() =>
			{
				Assert.That(files.Length, Is.EqualTo(1));
				Assert.That(files[0], Is.EqualTo(filePath));
			});
		}
		finally
		{
			Directory.Delete(tempRoot, recursive: true);
		}
	}

	private static CapturePersistenceRequest CreateRequest(
		string outputDirectory,
		SessionEventType eventType,
		byte[]? imageBytes,
		CaptureFailureInfo? failure,
		bool writeSidecar,
		CaptureDiagnostics? diagnostics = null)
	{
		return new CapturePersistenceRequest(
			TimestampUtc: new DateTimeOffset(2026, 2, 22, 14, 30, 0, TimeSpan.Zero),
			EventType: eventType,
			OutputDirectory: outputDirectory,
			Extension: "jpg",
			ImageBytes: imageBytes,
			Failure: failure,
			Hostname: "WORKSTATION-01",
			Username: "pablo",
			App: new CaptureAppInfo("LoginShot", "0.1.0", "1"),
			Camera: new CaptureCameraInfo("Integrated Camera", Diagnostics: diagnostics),
			WriteSidecar: writeSidecar);
	}

	private static string CreateTempDirectory()
	{
		var directoryPath = Path.Combine(Path.GetTempPath(), "LoginShot.Core.Tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(directoryPath);
		return directoryPath;
	}
}
