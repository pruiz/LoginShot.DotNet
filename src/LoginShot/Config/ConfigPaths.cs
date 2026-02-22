namespace LoginShot.Config;

internal static class ConfigPaths
{
    public static readonly string SampleConfigYaml =
        "output:\n" +
        "  directory: \"%USERPROFILE%\\\\Pictures\\\\LoginShot\"\n" +
        "  format: \"jpg\"\n" +
        "  maxWidth: 1280\n" +
        "  jpegQuality: 0.85\n" +
        "\n" +
        "triggers:\n" +
        "  onLogon: true\n" +
        "  onUnlock: true\n" +
        "  onLock: true\n" +
        "\n" +
        "metadata:\n" +
        "  writeSidecar: true\n" +
        "\n" +
        "ui:\n" +
        "  trayIcon: true\n" +
        "  startAfterLogin: false\n" +
        "\n" +
        "capture:\n" +
        "  debounceSeconds: 3\n" +
        "  backend: \"opencv\"\n" +
        "  cameraIndex: null\n" +
        "\n" +
        "logging:\n" +
        "  directory: \"%LOCALAPPDATA%\\\\LoginShot\\\\logs\"\n" +
        "  retentionDays: 14\n" +
        "  cleanupIntervalHours: 24\n" +
        "\n" +
        "watermark:\n" +
        "  enabled: true\n" +
        "  format: \"yyyy-MM-dd HH:mm:ss zzz\"\n";

}
