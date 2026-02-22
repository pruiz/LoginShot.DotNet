using Microsoft.Extensions.Logging;

namespace LoginShot.Util;

internal sealed class LogRetentionService : IDisposable
{
    private readonly FileLoggingOptions options;
    private readonly ILogger logger;
    private System.Threading.Timer? timer;

    public LogRetentionService(FileLoggingOptions options, ILogger logger)
    {
        this.options = options;
        this.logger = logger;
    }

    public void Start()
    {
        CleanupOldLogs();

        var interval = TimeSpan.FromHours(options.CleanupIntervalHours);
        timer = new System.Threading.Timer(_ => CleanupOldLogs(), null, interval, interval);
    }

    public void Dispose()
    {
        timer?.Dispose();
        timer = null;
    }

    private void CleanupOldLogs()
    {
        try
        {
            Directory.CreateDirectory(options.DirectoryPath);

            var cutoffDate = DateTime.UtcNow.Date.AddDays(-options.RetentionDays);
            var files = Directory.GetFiles(options.DirectoryPath, "loginshot-*.log");
            foreach (var filePath in files)
            {
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.LastWriteTimeUtc.Date < cutoffDate)
                {
                    File.Delete(filePath);
                }
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed while cleaning up old log files in {LogDirectory}", options.DirectoryPath);
        }
    }
}
