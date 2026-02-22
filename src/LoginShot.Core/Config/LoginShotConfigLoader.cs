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
        var config = LoginShotConfigDefaults.Create(pathResolver.UserProfilePath);

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
                Directory = pathResolver.ExpandKnownVariables(config.Output.Directory)
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
            JpegQuality = document.Output?.JpegQuality ?? defaults.Output.JpegQuality
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
            DebounceSeconds = document.Capture?.DebounceSeconds ?? defaults.Capture.DebounceSeconds
        };

        return defaults with
        {
            Output = output,
            Triggers = triggers,
            Metadata = metadata,
            Ui = ui,
            Capture = capture
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
    }

    private sealed record OutputDocument
    {
        public string? Directory { get; init; }
        public string? Format { get; init; }
        public int? MaxWidth { get; init; }
        public double? JpegQuality { get; init; }
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
    }
}
