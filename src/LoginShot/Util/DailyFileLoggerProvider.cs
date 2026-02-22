using Microsoft.Extensions.Logging;

namespace LoginShot.Util;

internal sealed class DailyFileLoggerProvider : ILoggerProvider
{
    private readonly FileLoggingOptions options;
    private readonly object syncLock = new();
    private StreamWriter? writer;
    private DateOnly? writerDate;
    private bool disposed;

    public DailyFileLoggerProvider(FileLoggingOptions options)
    {
        this.options = options;
        Directory.CreateDirectory(options.DirectoryPath);
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new DailyFileLogger(categoryName, this);
    }

    public void Dispose()
    {
        lock (syncLock)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            writer?.Dispose();
            writer = null;
            writerDate = null;
        }
    }

    private void WriteLine(DateTimeOffset timestamp, string line)
    {
        lock (syncLock)
        {
            if (disposed)
            {
                return;
            }

            EnsureWriter(timestamp);
            writer!.WriteLine(line);
            writer.Flush();
        }
    }

    private void EnsureWriter(DateTimeOffset timestamp)
    {
        var currentDate = DateOnly.FromDateTime(timestamp.DateTime);
        if (writer is not null && writerDate == currentDate)
        {
            return;
        }

        writer?.Dispose();

        var filePath = Path.Combine(options.DirectoryPath, $"loginshot-{timestamp:yyyy-MM-dd}.log");
        var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read);
        writer = new StreamWriter(stream)
        {
            AutoFlush = true
        };
        writerDate = currentDate;
    }

    private sealed class DailyFileLogger : ILogger
    {
        private readonly string categoryName;
        private readonly DailyFileLoggerProvider provider;

        public DailyFileLogger(string categoryName, DailyFileLoggerProvider provider)
        {
            this.categoryName = categoryName;
            this.provider = provider;
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

            provider.WriteLine(timestamp, line);
        }
    }
}
