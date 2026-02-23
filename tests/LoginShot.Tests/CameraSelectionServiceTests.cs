using LoginShot.App;
using LoginShot.Config;
using Microsoft.Extensions.Logging.Abstractions;

namespace LoginShot.Tests;

public class CameraSelectionServiceTests
{
	[Test]
	public void TryApplySelection_WhenSaveSucceeds_UpdatesCameraIndexAndSourcePath()
	{
		var writer = new FakeConfigWriter
		{
			SaveResultPath = "C:\\Users\\pablo\\AppData\\Roaming\\LoginShot\\config.yml"
		};
		var service = new CameraSelectionService(writer, NullLogger.Instance);
		var currentConfig = CreateConfig(cameraIndex: null, sourcePath: "C:\\existing\\config.yml");

		var success = service.TryApplySelection(currentConfig, 2, out var updatedConfig, out var errorMessage);

		Assert.Multiple(() =>
		{
			Assert.That(success, Is.True);
			Assert.That(errorMessage, Is.Null);
			Assert.That(updatedConfig.Capture.CameraIndex, Is.EqualTo(2));
			Assert.That(updatedConfig.SourcePath, Is.EqualTo(writer.SaveResultPath));
			Assert.That(writer.LastSavedConfig?.Capture.CameraIndex, Is.EqualTo(2));
			Assert.That(writer.LastPreferredPath, Is.EqualTo(currentConfig.SourcePath));
		});
	}

	[Test]
	public void TryApplySelection_WhenSaveFails_ReturnsOriginalConfigAndError()
	{
		var writer = new FakeConfigWriter
		{
			ExceptionToThrow = new InvalidOperationException("disk full")
		};
		var service = new CameraSelectionService(writer, NullLogger.Instance);
		var currentConfig = CreateConfig(cameraIndex: 1, sourcePath: "C:\\existing\\config.yml");

		var success = service.TryApplySelection(currentConfig, 3, out var updatedConfig, out var errorMessage);

		Assert.Multiple(() =>
		{
			Assert.That(success, Is.False);
			Assert.That(errorMessage, Is.EqualTo("disk full"));
			Assert.That(updatedConfig, Is.EqualTo(currentConfig));
		});
	}

	private static LoginShotConfig CreateConfig(int? cameraIndex, string? sourcePath)
	{
		return new LoginShotConfig(
			new OutputConfig("C:\\Users\\pablo\\Pictures\\LoginShot", "jpg", 1280, 0.85),
			new TriggerConfig(true, true, true),
			new MetadataConfig(true),
			new UiConfig(true, false),
			new CaptureConfig(3, "opencv", cameraIndex),
			new LoggingConfig("C:\\Users\\pablo\\AppData\\Local\\LoginShot\\logs", 14, 24),
			new WatermarkConfig(true, "yyyy-MM-dd HH:mm:ss zzz"),
			sourcePath);
	}

	private sealed class FakeConfigWriter : IConfigWriter
	{
		public string SaveResultPath { get; init; } = "C:\\saved\\config.yml";
		public Exception? ExceptionToThrow { get; init; }
		public LoginShotConfig? LastSavedConfig { get; private set; }
		public string? LastPreferredPath { get; private set; }

		public string Save(LoginShotConfig config, string? preferredPath)
		{
			LastSavedConfig = config;
			LastPreferredPath = preferredPath;

			if (ExceptionToThrow is not null)
			{
				throw ExceptionToThrow;
			}

			return SaveResultPath;
		}
	}
}
