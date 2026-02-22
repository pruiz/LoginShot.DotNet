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
        "  cameraIndex: null\n";

}
