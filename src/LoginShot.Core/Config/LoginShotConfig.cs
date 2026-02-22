namespace LoginShot.Config;

public sealed record LoginShotConfig(
    OutputConfig Output,
    TriggerConfig Triggers,
    MetadataConfig Metadata,
    UiConfig Ui,
    CaptureConfig Capture,
    string? SourcePath);

public sealed record OutputConfig(string Directory, string Format, int? MaxWidth, double JpegQuality);

public sealed record TriggerConfig(bool OnLogon, bool OnUnlock, bool OnLock);

public sealed record MetadataConfig(bool WriteSidecar);

public sealed record UiConfig(bool TrayIcon, bool StartAfterLogin);

public sealed record CaptureConfig(int DebounceSeconds);
