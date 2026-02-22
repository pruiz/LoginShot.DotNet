using Microsoft.Extensions.Logging;

namespace LoginShot.Util;

internal sealed class DailyFileLoggerProvider : ILoggerProvider
{
    private readonly FileLoggingOptions options;
    private readonly object writeLock = new();

    public DailyFileLoggerProvider(FileLoggingOptions options)
    {
        this.options = options;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new DailyFileLogger(categoryName, options, writeLock);
    }

    public void Dispose()
    {
    }

    private sealed class DailyFileLogger : ILogger
    {
        private readonly string categoryName;
        private readonly FileLoggingOptions options;
        private readonly object writeLock;

        public DailyFileLogger(string categoryName, FileLoggingOptions options, object writeLock)
        {
            this.categoryName = categoryName;
            this.options = options;
            this.writeLock = writeLock;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= LogLevel.Information;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            var timestamp = DateTimeOffset.Now;
            var line = $"{timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{logLevel}] {categoryName}: {message}";
            if (exception is not null)
            {
                line += $"{Environment.NewLine}{exception}";
            }

            Directory.CreateDirectory(options.DirectoryPath);
            var filePath = Path.Combine(options.DirectoryPath, $"loginshot-{timestamp:yyyy-MM-dd}.log");

            lock (writeLock)
            {
                File.AppendAllText(filePath, line + Environment.NewLine);
            }
        }
    }
}
