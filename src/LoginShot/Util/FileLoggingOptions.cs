namespace LoginShot.Util;

internal sealed record FileLoggingOptions(string DirectoryPath, int RetentionDays, int CleanupIntervalHours);
