using System.Diagnostics;
using System.Xml.Linq;

namespace LoginShot.Startup;

internal sealed class SchtasksStartupTaskSchedulerClient : IStartupTaskSchedulerClient
{
    public bool TaskExists(string taskName)
    {
        var result = RunSchtasks("/Query", "/TN", taskName);
        return result.ExitCode == 0;
    }

    public bool IsTaskEnabled(string taskName)
    {
        var result = RunSchtasks("/Query", "/TN", taskName, "/XML");
        if (result.ExitCode != 0)
        {
            return false;
        }

        try
        {
            var xml = XDocument.Parse(result.StandardOutput);
            var enabledValue = xml
                .Descendants()
                .FirstOrDefault(node => string.Equals(node.Name.LocalName, "Enabled", StringComparison.OrdinalIgnoreCase))
                ?.Value;

            return string.Equals(enabledValue, "true", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public void RegisterLogonTask(string taskName, string executablePath, string arguments, string description)
    {
        var taskCommand = BuildTaskCommand(executablePath, arguments);

        var result = RunSchtasks(
            "/Create",
            "/F",
            "/SC",
            "ONLOGON",
            "/TN",
            taskName,
            "/TR",
            taskCommand,
            "/RL",
            "LIMITED");

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Failed to register startup task '{taskName}': {result.StandardError}");
        }
    }

    public void DeleteTask(string taskName)
    {
        var result = RunSchtasks("/Delete", "/F", "/TN", taskName);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Failed to delete startup task '{taskName}': {result.StandardError}");
        }
    }

    private static string BuildTaskCommand(string executablePath, string arguments)
    {
        return string.IsNullOrWhiteSpace(arguments)
            ? Quote(executablePath)
            : $"{Quote(executablePath)} {arguments}";
    }

    private static string Quote(string value)
    {
        return $"\"{value}\"";
    }

    private static CommandResult RunSchtasks(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start schtasks.exe process.");

        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return new CommandResult(process.ExitCode, standardOutput, standardError);
    }

    private sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError);
}
