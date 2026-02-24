using System.Globalization;
using LoginShot.Config;

namespace LoginShot.Tests;

public class YamlConfigWriterTests
{
	[Test]
	public void Save_WhenCurrentCultureUsesCommaDecimal_WritesInvariantJpegQuality()
	{
		var originalCulture = CultureInfo.CurrentCulture;
		var originalUiCulture = CultureInfo.CurrentUICulture;
		var tempDirectory = Path.Combine(Path.GetTempPath(), "LoginShot.Tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempDirectory);

		try
		{
			CultureInfo.CurrentCulture = new CultureInfo("de-DE");
			CultureInfo.CurrentUICulture = new CultureInfo("de-DE");

			var config = new LoginShotConfig(
				new OutputConfig("C:\\Users\\pablo\\Pictures\\LoginShot", "jpg", 1280, 0.85),
				new TriggerConfig(true, true, true),
				new MetadataConfig(true),
				new UiConfig(true, false),
				new CaptureConfig(
					3,
					"opencv",
					null,
					new CaptureNegotiationConfig(
						BackendOrder: ["dshow", "msmf", "any"],
						PixelFormats: ["auto", "MJPG"],
						ConvertRgbMode: "auto",
						Resolutions: ["auto", "1280x720"],
						AttemptsPerCombination: 2,
						WarmupFrames: 6)),
				new LoggingConfig("C:\\Users\\pablo\\AppData\\Local\\LoginShot\\logs", 14, 24, "Information"),
				new WatermarkConfig(true, "yyyy-MM-dd HH:mm:ss zzz"),
				SourcePath: null);

			var outputPath = Path.Combine(tempDirectory, "config.yml");
			var writer = new YamlConfigWriter();
			writer.Save(config, outputPath);

			var yaml = File.ReadAllText(outputPath);
			Assert.That(yaml, Does.Contain("jpegQuality: 0.85"));
			Assert.That(yaml, Does.Not.Contain("jpegQuality: 0,85"));
		}
		finally
		{
			CultureInfo.CurrentCulture = originalCulture;
			CultureInfo.CurrentUICulture = originalUiCulture;
			Directory.Delete(tempDirectory, recursive: true);
		}
	}
}
