namespace LoginShot.Util;

internal static class LogFilePathProvider
{
	public static string GetDailyLogFilePath(string directoryPath, DateTimeOffset localTimestamp)
	{
		return Path.Combine(directoryPath, $"loginshot-{localTimestamp:yyyy-MM-dd}.log");
	}
}
