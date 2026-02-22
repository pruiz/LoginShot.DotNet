namespace LoginShot.Startup;

public sealed class StartupShortcutRegistrationService : IStartupRegistrationService
{
    private readonly string startupDirectory;
    private readonly string shortcutName;
    private readonly string executablePath;
    private readonly IStartupShortcutWriter shortcutWriter;
    private readonly IFileSystem fileSystem;

    public StartupShortcutRegistrationService(
        string startupDirectory,
        string shortcutName,
        string executablePath,
        IStartupShortcutWriter shortcutWriter,
        IFileSystem fileSystem)
    {
        this.startupDirectory = startupDirectory;
        this.shortcutName = shortcutName;
        this.executablePath = executablePath;
        this.shortcutWriter = shortcutWriter;
        this.fileSystem = fileSystem;
    }

    public bool IsEnabled()
    {
        return fileSystem.FileExists(GetShortcutPath());
    }

    public void Enable()
    {
        fileSystem.EnsureDirectory(startupDirectory);

        var shortcutPath = GetShortcutPath();
        var workingDirectory = Path.GetDirectoryName(executablePath) ?? startupDirectory;
        shortcutWriter.WriteShortcut(shortcutPath, executablePath, workingDirectory, "LoginShot start after login");
    }

    public void Disable()
    {
        var shortcutPath = GetShortcutPath();
        if (!fileSystem.FileExists(shortcutPath))
        {
            return;
        }

        fileSystem.DeleteFile(shortcutPath);
    }

    private string GetShortcutPath()
    {
        return Path.Combine(startupDirectory, shortcutName);
    }
}
