using System.Globalization;
using System.Text;

namespace LoginShot.Config;

internal sealed class YamlConfigWriter : IConfigWriter
{
	public string Save(LoginShotConfig config, string? preferredPath)
	{
		var outputPath = string.IsNullOrWhiteSpace(preferredPath)
			? GetDefaultConfigPath()
			: preferredPath;

		var directory = Path.GetDirectoryName(outputPath)
			?? throw new InvalidOperationException("Config path does not have a directory.");
		Directory.CreateDirectory(directory);

		var yamlText = Serialize(config);
		WriteAllTextAtomic(outputPath, yamlText);
		return outputPath;
	}

	private static string GetDefaultConfigPath()
	{
		var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
		return Path.Combine(appDataPath, "LoginShot", "config.yml");
	}

	private static string Serialize(LoginShotConfig config)
	{
		var builder = new StringBuilder();
		builder.AppendLine("output:");
		builder.AppendLine($"  directory: \"{Escape(config.Output.Directory)}\"");
		builder.AppendLine($"  format: \"{Escape(config.Output.Format)}\"");
		builder.AppendLine($"  maxWidth: {(config.Output.MaxWidth is null ? "null" : config.Output.MaxWidth.Value)}");
		builder.AppendLine($"  jpegQuality: {config.Output.JpegQuality.ToString("0.##", CultureInfo.InvariantCulture)}");
		builder.AppendLine();

		builder.AppendLine("triggers:");
		builder.AppendLine($"  onLogon: {ToYamlBoolean(config.Triggers.OnLogon)}");
		builder.AppendLine($"  onUnlock: {ToYamlBoolean(config.Triggers.OnUnlock)}");
		builder.AppendLine($"  onLock: {ToYamlBoolean(config.Triggers.OnLock)}");
		builder.AppendLine();

		builder.AppendLine("metadata:");
		builder.AppendLine($"  writeSidecar: {ToYamlBoolean(config.Metadata.WriteSidecar)}");
		builder.AppendLine();

		builder.AppendLine("ui:");
		builder.AppendLine($"  trayIcon: {ToYamlBoolean(config.Ui.TrayIcon)}");
		builder.AppendLine($"  startAfterLogin: {ToYamlBoolean(config.Ui.StartAfterLogin)}");
		builder.AppendLine();

		builder.AppendLine("capture:");
		builder.AppendLine($"  debounceSeconds: {config.Capture.DebounceSeconds}");
		builder.AppendLine($"  backend: \"{Escape(config.Capture.Backend)}\"");
		builder.AppendLine($"  cameraIndex: {(config.Capture.CameraIndex is null ? "null" : config.Capture.CameraIndex.Value)}");
		builder.AppendLine("  negotiation:");
		builder.AppendLine("    backendOrder:");
		foreach (var backend in config.Capture.Negotiation.BackendOrder)
		{
			builder.AppendLine($"      - \"{Escape(backend)}\"");
		}
		builder.AppendLine("    pixelFormats:");
		foreach (var pixelFormat in config.Capture.Negotiation.PixelFormats)
		{
			builder.AppendLine($"      - \"{Escape(pixelFormat)}\"");
		}
		builder.AppendLine($"    convertRgbMode: \"{Escape(config.Capture.Negotiation.ConvertRgbMode)}\"");
		builder.AppendLine("    resolutions:");
		foreach (var resolution in config.Capture.Negotiation.Resolutions)
		{
			builder.AppendLine($"      - \"{Escape(resolution)}\"");
		}
		builder.AppendLine($"    attemptsPerCombination: {config.Capture.Negotiation.AttemptsPerCombination}");
		builder.AppendLine($"    warmupFrames: {config.Capture.Negotiation.WarmupFrames}");
		builder.AppendLine();

		builder.AppendLine("logging:");
		builder.AppendLine($"  directory: \"{Escape(config.Logging.Directory)}\"");
		builder.AppendLine($"  retentionDays: {config.Logging.RetentionDays}");
		builder.AppendLine($"  cleanupIntervalHours: {config.Logging.CleanupIntervalHours}");
		builder.AppendLine($"  level: \"{Escape(config.Logging.Level)}\"");
		builder.AppendLine();

		builder.AppendLine("watermark:");
		builder.AppendLine($"  enabled: {ToYamlBoolean(config.Watermark.Enabled)}");
		builder.AppendLine($"  format: \"{Escape(config.Watermark.Format)}\"");

		return builder.ToString();
	}

	private static void WriteAllTextAtomic(string path, string content)
	{
		var directory = Path.GetDirectoryName(path)
			?? throw new InvalidOperationException("Path does not have a directory.");
		var tempPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");

		File.WriteAllText(tempPath, content);
		try
		{
			File.Move(tempPath, path, overwrite: true);
		}
		finally
		{
			if (File.Exists(tempPath))
			{
				File.Delete(tempPath);
			}
		}
	}

	private static string Escape(string value)
	{
		var builder = new StringBuilder(value.Length + 8);
		foreach (var character in value)
		{
			switch (character)
			{
				case '\\':
					builder.Append("\\\\");
					break;
				case '\"':
					builder.Append("\\\"");
					break;
				case '\n':
					builder.Append("\\n");
					break;
				case '\r':
					builder.Append("\\r");
					break;
				case '\t':
					builder.Append("\\t");
					break;
				default:
					if (char.IsControl(character))
					{
						builder.Append("\\u");
						builder.Append(((int)character).ToString("x4"));
					}
					else
					{
						builder.Append(character);
					}
					break;
			}
		}

		return builder.ToString();
	}

	private static string ToYamlBoolean(bool value)
	{
		return value ? "true" : "false";
	}
}
