namespace LoginShot.Startup;

public interface IStartupShortcutWriter
{
    void WriteShortcut(string shortcutPath, string targetPath, string workingDirectory, string description);
}
