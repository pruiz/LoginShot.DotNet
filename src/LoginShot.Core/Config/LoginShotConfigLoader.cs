using System.Globalization;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace LoginShot.Config;

public sealed class LoginShotConfigLoader : IConfigLoader
{
	private readonly ConfigPathResolver pathResolver;
	private readonly IConfigFileProvider fileProvider;
	private readonly IDeserializer deserializer;

	public LoginShotConfigLoader(ConfigPathResolver pathResolver, IConfigFileProvider fileProvider)
	{
		this.pathResolver = pathResolver;
		this.fileProvider = fileProvider;
		deserializer = new DeserializerBuilder()
			.WithNamingConvention(CamelCaseNamingConvention.Instance)
			.IgnoreUnmatchedProperties()
			.Build();
	}

	public LoginShotConfig Load()
	{
		var resolvedPath = pathResolver.ResolveFirstExistingPath();
		var config = LoginShotConfigDefaults.Create(pathResolver.UserProfilePath, pathResolver.LocalAppDataPath);

		if (resolvedPath is null)
		{
			return config;
		}

		var yamlText = fileProvider.ReadAllText(resolvedPath);

		ConfigDocument? document;
		try
		{
			document = deserializer.Deserialize<ConfigDocument>(yamlText);
		}
		catch (Exception exception)
		{
			throw new ConfigValidationException($"Failed to parse config '{resolvedPath}': {exception.Message}", exception);
		}

		config = Merge(config, document ?? new ConfigDocument());
		config = config with
		{
			Output = config.Output with
			{
				Directory = pathResolver.NormalizeWindowsPathSeparators(
					pathResolver.ExpandKnownVariables(config.Output.Directory))
			},
			Logging = config.Logging with
			{
				Directory = pathResolver.NormalizeWindowsPathSeparators(
					pathResolver.ExpandKnownVariables(config.Logging.Directory))
			},
			SourcePath = resolvedPath
		};

		Validate(config);
		return config;
	}

	private static LoginShotConfig Merge(LoginShotConfig defaults, ConfigDocument document)
	{
		var output = defaults.Output with
		{
			Directory = document.Output?.Directory ?? defaults.Output.Directory,
			Format = document.Output?.Format ?? defaults.Output.Format,
			MaxWidth = document.Output?.MaxWidth ?? defaults.Output.MaxWidth,
			JpegQuality = ParseOptionalDouble(document.Output?.JpegQuality, defaults.Output.JpegQuality, "output.jpegQuality")
		};

		var triggers = defaults.Triggers with
		{
			OnLogon = document.Triggers?.OnLogon ?? defaults.Triggers.OnLogon,
			OnUnlock = document.Triggers?.OnUnlock ?? defaults.Triggers.OnUnlock,
			OnLock = document.Triggers?.OnLock ?? defaults.Triggers.OnLock
		};

		var metadata = defaults.Metadata with
		{
			WriteSidecar = document.Metadata?.WriteSidecar ?? defaults.Metadata.WriteSidecar
		};

		var ui = defaults.Ui with
		{
			TrayIcon = document.Ui?.TrayIcon ?? defaults.Ui.TrayIcon,
			StartAfterLogin = document.Ui?.StartAfterLogin ?? defaults.Ui.StartAfterLogin
		};

		var capture = defaults.Capture with
		{
			DebounceSeconds = document.Capture?.DebounceSeconds ?? defaults.Capture.DebounceSeconds,
			Backend = document.Capture?.Backend ?? defaults.Capture.Backend,
			CameraIndex = document.Capture?.CameraIndex ?? defaults.Capture.CameraIndex,
			Negotiation = defaults.Capture.Negotiation with
			{
				BackendOrder = document.Capture?.Negotiation?.BackendOrder ?? defaults.Capture.Negotiation.BackendOrder,
				PixelFormats = document.Capture?.Negotiation?.PixelFormats ?? defaults.Capture.Negotiation.PixelFormats,
				ConvertRgbMode = document.Capture?.Negotiation?.ConvertRgbMode ?? defaults.Capture.Negotiation.ConvertRgbMode,
				Resolutions = document.Capture?.Negotiation?.Resolutions ?? defaults.Capture.Negotiation.Resolutions,
				AttemptsPerCombination = document.Capture?.Negotiation?.AttemptsPerCombination ?? defaults.Capture.Negotiation.AttemptsPerCombination,
				WarmupFrames = document.Capture?.Negotiation?.WarmupFrames ?? defaults.Capture.Negotiation.WarmupFrames
			}
		};

		var logging = defaults.Logging with
		{
			Directory = document.Logging?.Directory ?? defaults.Logging.Directory,
			RetentionDays = document.Logging?.RetentionDays ?? defaults.Logging.RetentionDays,
			CleanupIntervalHours = document.Logging?.CleanupIntervalHours ?? defaults.Logging.CleanupIntervalHours,
			Level = document.Logging?.Level ?? defaults.Logging.Level
		};

		var watermark = defaults.Watermark with
		{
			Enabled = document.Watermark?.Enabled ?? defaults.Watermark.Enabled,
			Format = document.Watermark?.Format ?? defaults.Watermark.Format
		};

		return defaults with
		{
			Output = output,
			Triggers = triggers,
			Metadata = metadata,
			Ui = ui,
			Capture = capture,
			Logging = logging,
			Watermark = watermark
		};
	}

	private static void Validate(LoginShotConfig config)
	{
		var errors = new List<string>();

		if (string.IsNullOrWhiteSpace(config.Output.Directory))
		{
			errors.Add("output.directory must not be empty.");
		}

		if (!string.Equals(config.Output.Format, "jpg", StringComparison.OrdinalIgnoreCase))
		{
			errors.Add("output.format must be 'jpg' in v1.");
		}

		if (config.Output.MaxWidth is <= 0)
		{
			errors.Add("output.maxWidth must be greater than 0 when provided.");
		}

		if (config.Output.JpegQuality < 0.0 || config.Output.JpegQuality > 1.0)
		{
			errors.Add("output.jpegQuality must be between 0.0 and 1.0.");
		}

		if (config.Capture.DebounceSeconds < 0)
		{
			errors.Add("capture.debounceSeconds must be 0 or greater.");
		}

		if (!string.Equals(config.Capture.Backend, "opencv", StringComparison.OrdinalIgnoreCase)
			&& !string.Equals(config.Capture.Backend, "winrt-mediacapture", StringComparison.OrdinalIgnoreCase))
		{
			errors.Add("capture.backend must be either 'opencv' or 'winrt-mediacapture'.");
		}

		if (config.Capture.CameraIndex is < 0)
		{
			errors.Add("capture.cameraIndex must be 0 or greater when provided.");
		}

		if (config.Capture.Negotiation.AttemptsPerCombination < 1 || config.Capture.Negotiation.AttemptsPerCombination > 5)
		{
			errors.Add("capture.negotiation.attemptsPerCombination must be between 1 and 5.");
		}

		if (config.Capture.Negotiation.BackendOrder.Count == 0)
		{
			errors.Add("capture.negotiation.backendOrder must contain at least one backend.");
		}

		if (config.Capture.Negotiation.PixelFormats.Count == 0)
		{
			errors.Add("capture.negotiation.pixelFormats must contain at least one value.");
		}

		if (config.Capture.Negotiation.Resolutions.Count == 0)
		{
			errors.Add("capture.negotiation.resolutions must contain at least one value.");
		}

		if (config.Capture.Negotiation.WarmupFrames < 0 || config.Capture.Negotiation.WarmupFrames > 30)
		{
			errors.Add("capture.negotiation.warmupFrames must be between 0 and 30.");
		}

		if (!IsValidConvertRgbMode(config.Capture.Negotiation.ConvertRgbMode))
		{
			errors.Add("capture.negotiation.convertRgbMode must be one of auto, true, or false.");
		}

		foreach (var backend in config.Capture.Negotiation.BackendOrder)
		{
			if (!IsValidCaptureBackendAlias(backend))
			{
				errors.Add($"capture.negotiation.backendOrder contains unsupported backend '{backend}'. Supported values: dshow, msmf, any.");
			}
		}

		foreach (var pixelFormat in config.Capture.Negotiation.PixelFormats)
		{
			if (!IsValidPixelFormat(pixelFormat))
			{
				errors.Add($"capture.negotiation.pixelFormats contains unsupported value '{pixelFormat}'. Supported values: auto, MJPG, YUY2, NV12.");
			}
		}

		foreach (var resolution in config.Capture.Negotiation.Resolutions)
		{
			if (!IsValidResolution(resolution))
			{
				errors.Add($"capture.negotiation.resolutions contains invalid value '{resolution}'. Use auto or <width>x<height>.");
			}
		}

		if (string.IsNullOrWhiteSpace(config.Logging.Directory))
		{
			errors.Add("logging.directory must not be empty.");
		}

		if (config.Logging.RetentionDays < 1)
		{
			errors.Add("logging.retentionDays must be 1 or greater.");
		}

		if (config.Logging.CleanupIntervalHours < 1)
		{
			errors.Add("logging.cleanupIntervalHours must be 1 or greater.");
		}

		if (!Enum.TryParse<LogLevel>(config.Logging.Level, ignoreCase: true, out _))
		{
			errors.Add("logging.level must be one of Trace, Debug, Information, Warning, Error, Critical, or None.");
		}

		if (string.IsNullOrWhiteSpace(config.Watermark.Format))
		{
			errors.Add("watermark.format must not be empty.");
		}

		if (errors.Count > 0)
		{
			throw new ConfigValidationException(string.Join(" ", errors));
		}
	}

	private sealed record ConfigDocument
	{
		public OutputDocument? Output { get; init; }
		public TriggersDocument? Triggers { get; init; }
		public MetadataDocument? Metadata { get; init; }
		public UiDocument? Ui { get; init; }
		public CaptureDocument? Capture { get; init; }
		public LoggingDocument? Logging { get; init; }
		public WatermarkDocument? Watermark { get; init; }
	}

	private sealed record OutputDocument
	{
		public string? Directory { get; init; }
		public string? Format { get; init; }
		public int? MaxWidth { get; init; }
		public string? JpegQuality { get; init; }
	}

	private sealed record TriggersDocument
	{
		public bool? OnLogon { get; init; }
		public bool? OnUnlock { get; init; }
		public bool? OnLock { get; init; }
	}

	private sealed record MetadataDocument
	{
		public bool? WriteSidecar { get; init; }
	}

	private sealed record UiDocument
	{
		public bool? TrayIcon { get; init; }
		public bool? StartAfterLogin { get; init; }
	}

	private sealed record CaptureDocument
	{
		public int? DebounceSeconds { get; init; }
		public string? Backend { get; init; }
		public int? CameraIndex { get; init; }
		public CaptureNegotiationDocument? Negotiation { get; init; }
	}

	private sealed record CaptureNegotiationDocument
	{
		public string[]? BackendOrder { get; init; }
		public string[]? PixelFormats { get; init; }
		public string? ConvertRgbMode { get; init; }
		public string[]? Resolutions { get; init; }
		public int? AttemptsPerCombination { get; init; }
		public int? WarmupFrames { get; init; }
	}

	private sealed record LoggingDocument
	{
		public string? Directory { get; init; }
		public int? RetentionDays { get; init; }
		public int? CleanupIntervalHours { get; init; }
		public string? Level { get; init; }
	}

	private sealed record WatermarkDocument
	{
		public bool? Enabled { get; init; }
		public string? Format { get; init; }
	}

	private static double ParseOptionalDouble(string? value, double fallback, string settingName)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return fallback;
		}

		var trimmed = value.Trim();
		if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var invariantParsed))
		{
			return invariantParsed;
		}

		if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.CurrentCulture, out var currentParsed))
		{
			return currentParsed;
		}

		if (trimmed.Contains(',') && double.TryParse(trimmed.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var commaNormalized))
		{
			return commaNormalized;
		}

		throw new ConfigValidationException($"{settingName} must be a valid number.");
	}

	private static bool IsValidCaptureBackendAlias(string backend)
	{
		return string.Equals(backend, "dshow", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(backend, "msmf", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(backend, "any", StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsValidPixelFormat(string pixelFormat)
	{
		return string.Equals(pixelFormat, "auto", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(pixelFormat, "MJPG", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(pixelFormat, "YUY2", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(pixelFormat, "NV12", StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsValidConvertRgbMode(string convertRgbMode)
	{
		return string.Equals(convertRgbMode, "auto", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(convertRgbMode, "true", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(convertRgbMode, "false", StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsValidResolution(string resolution)
	{
		if (string.Equals(resolution, "auto", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		var separatorIndex = resolution.IndexOf('x');
		if (separatorIndex <= 0 || separatorIndex >= resolution.Length - 1)
		{
			return false;
		}

		var widthText = resolution[..separatorIndex];
		var heightText = resolution[(separatorIndex + 1)..];

		return int.TryParse(widthText, NumberStyles.None, CultureInfo.InvariantCulture, out var width)
			&& int.TryParse(heightText, NumberStyles.None, CultureInfo.InvariantCulture, out var height)
			&& width > 0
			&& height > 0;
	}
}
