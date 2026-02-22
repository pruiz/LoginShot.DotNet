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
        "  debounceSeconds: 3\n";

    public static IReadOnlyList<string> GetSearchPaths()
    {
        var userProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        return new[]
        {
            Path.Combine(userProfilePath, ".config", "LoginShot", "config.yml"),
            Path.Combine(appDataPath, "LoginShot", "config.yml")
        };
    }
}
