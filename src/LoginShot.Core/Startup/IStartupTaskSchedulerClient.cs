namespace LoginShot.Startup;

public interface IStartupTaskSchedulerClient
{
	bool TaskExists(string taskName);
	bool IsTaskEnabled(string taskName);
	void RegisterLogonTask(string taskName, string executablePath, string arguments, string description);
	void DeleteTask(string taskName);
}
