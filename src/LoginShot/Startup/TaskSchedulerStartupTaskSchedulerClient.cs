using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.TaskScheduler;

namespace LoginShot.Startup;

internal sealed class TaskSchedulerStartupTaskSchedulerClient : IStartupTaskSchedulerClient
{
    public bool TaskExists(string taskName)
    {
        using var taskService = new TaskService();
        return taskService.GetTask(taskName) is not null;
    }

    public bool IsTaskEnabled(string taskName)
    {
        using var taskService = new TaskService();
        var task = taskService.GetTask(taskName);
        return task?.Enabled == true;
    }

    public void RegisterLogonTask(string taskName, string executablePath, string arguments, string description)
    {
        try
        {
            using var taskService = new TaskService();
            var taskDefinition = taskService.NewTask();

            taskDefinition.RegistrationInfo.Description = description;
            taskDefinition.Principal.RunLevel = TaskRunLevel.LUA;
            taskDefinition.Principal.LogonType = TaskLogonType.InteractiveToken;
            taskDefinition.Settings.MultipleInstances = TaskInstancesPolicy.IgnoreNew;
            taskDefinition.Settings.StartWhenAvailable = true;
            taskDefinition.Settings.DisallowStartIfOnBatteries = false;
            taskDefinition.Settings.StopIfGoingOnBatteries = false;

            var currentUser = WindowsIdentity.GetCurrent().Name;
            taskDefinition.Triggers.Add(new LogonTrigger
            {
                Enabled = true,
                UserId = currentUser
            });

            taskDefinition.Actions.Add(new ExecAction(executablePath, arguments, Path.GetDirectoryName(executablePath)));

            taskService.RootFolder.RegisterTaskDefinition(
                taskName,
                taskDefinition,
                TaskCreation.CreateOrUpdate,
                userId: null,
                password: null,
                logonType: TaskLogonType.InteractiveToken);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw CreateAccessDeniedException("register", taskName, exception);
        }
        catch (COMException exception) when (IsAccessDenied(exception))
        {
            throw CreateAccessDeniedException("register", taskName, exception);
        }
    }

    public void DeleteTask(string taskName)
    {
        try
        {
            using var taskService = new TaskService();
            if (taskService.GetTask(taskName) is null)
            {
                return;
            }

            taskService.RootFolder.DeleteTask(taskName, false);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw CreateAccessDeniedException("delete", taskName, exception);
        }
        catch (COMException exception) when (IsAccessDenied(exception))
        {
            throw CreateAccessDeniedException("delete", taskName, exception);
        }
    }

    private static bool IsAccessDenied(COMException exception)
    {
        const int EAccessDenied = unchecked((int)0x80070005);
        return exception.HResult == EAccessDenied;
    }

    private static InvalidOperationException CreateAccessDeniedException(string operation, string taskName, Exception innerException)
    {
        return new InvalidOperationException(
            $"Failed to {operation} startup task '{taskName}': Access denied. " +
            "Per-user startup tasks should not require elevation, but local policy may block task registration.",
            innerException);
    }
}
