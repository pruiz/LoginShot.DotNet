namespace LoginShot.Startup;

public sealed class TaskSchedulerStartupRegistrationService : IStartupRegistrationService
{
    private readonly string taskName;
    private readonly string executablePath;
    private readonly string arguments;
    private readonly string legacyShortcutPath;
    private readonly IStartupTaskSchedulerClient schedulerClient;
    private readonly IFileSystem fileSystem;

    public TaskSchedulerStartupRegistrationService(
        string taskName,
        string executablePath,
        string arguments,
        string legacyShortcutPath,
        IStartupTaskSchedulerClient schedulerClient,
        IFileSystem fileSystem)
    {
        this.taskName = taskName;
        this.executablePath = executablePath;
        this.arguments = arguments;
        this.legacyShortcutPath = legacyShortcutPath;
        this.schedulerClient = schedulerClient;
        this.fileSystem = fileSystem;
    }

    public bool IsEnabled()
    {
        return schedulerClient.TaskExists(taskName) && schedulerClient.IsTaskEnabled(taskName);
    }

    public void Enable()
    {
        schedulerClient.RegisterLogonTask(
            taskName,
            executablePath,
            arguments,
            "LoginShot start after login");

        CleanupLegacyShortcut();
    }

    public void Disable()
    {
        if (schedulerClient.TaskExists(taskName))
        {
            schedulerClient.DeleteTask(taskName);
        }

        CleanupLegacyShortcut();
    }

    private void CleanupLegacyShortcut()
    {
        if (fileSystem.FileExists(legacyShortcutPath))
        {
            fileSystem.DeleteFile(legacyShortcutPath);
        }
    }
}
