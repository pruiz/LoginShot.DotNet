using LoginShot.Config;
using Microsoft.Extensions.Logging;

namespace LoginShot.App;

internal sealed class ConfigReloadCoordinator : IDisposable
{
	private readonly SynchronizationContext uiContext;
	private readonly Func<LoginShotConfig> loadConfig;
	private readonly Action<LoginShotConfig, bool, bool> reloadSucceeded;
	private readonly Action<Exception, bool, bool> reloadFailed;
	private readonly Action<Exception> watcherErrored;
	private readonly ILogger logger;
	private readonly object timerLock = new();
	private readonly TimeSpan debounceDelay;

	private FileSystemWatcher? configFileWatcher;
	private System.Threading.Timer? configReloadTimer;

	public ConfigReloadCoordinator(
		SynchronizationContext uiContext,
		Func<LoginShotConfig> loadConfig,
		Action<LoginShotConfig, bool, bool> reloadSucceeded,
		Action<Exception, bool, bool> reloadFailed,
		Action<Exception> watcherErrored,
		ILogger logger,
		TimeSpan? debounceDelay = null)
	{
		this.uiContext = uiContext;
		this.loadConfig = loadConfig;
		this.reloadSucceeded = reloadSucceeded;
		this.reloadFailed = reloadFailed;
		this.watcherErrored = watcherErrored;
		this.logger = logger;
		this.debounceDelay = debounceDelay ?? TimeSpan.FromMilliseconds(1200);
	}

	public void Bind(string? configPath)
	{
		if (string.IsNullOrWhiteSpace(configPath))
		{
			DisposeWatcher();
			return;
		}

		var directory = Path.GetDirectoryName(configPath)
			?? throw new InvalidOperationException("Config path has no directory.");
		var fileName = Path.GetFileName(configPath);

		if (configFileWatcher is not null &&
			string.Equals(configFileWatcher.Path, directory, StringComparison.OrdinalIgnoreCase) &&
			string.Equals(configFileWatcher.Filter, fileName, StringComparison.OrdinalIgnoreCase))
		{
			return;
		}

		DisposeWatcher();

		var watcher = new FileSystemWatcher(directory, fileName)
		{
			NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.Size,
			EnableRaisingEvents = true,
			IncludeSubdirectories = false
		};

		watcher.Changed += OnConfigFileChanged;
		watcher.Created += OnConfigFileChanged;
		watcher.Renamed += OnConfigFileChanged;
		watcher.Error += OnConfigWatcherError;

		configFileWatcher = watcher;
		logger.LogInformation("Watching config file changes at {ConfigPath}", configPath);
	}

	public void RequestReload(bool notifyOnSuccess, bool autoReload)
	{
		try
		{
			var reloadedConfig = loadConfig();
			Bind(reloadedConfig.SourcePath);
			reloadSucceeded(reloadedConfig, notifyOnSuccess, autoReload);
		}
		catch (Exception exception)
		{
			reloadFailed(exception, notifyOnSuccess, autoReload);
		}
	}

	public void Dispose()
	{
		DisposeWatcher();
		lock (timerLock)
		{
			configReloadTimer?.Dispose();
			configReloadTimer = null;
		}
	}

	private void OnConfigFileChanged(object sender, FileSystemEventArgs eventArgs)
	{
		ScheduleAutoReload();
	}

	private void OnConfigWatcherError(object sender, ErrorEventArgs eventArgs)
	{
		var exception = eventArgs.GetException();
		watcherErrored(exception);
		ScheduleAutoReload();
	}

	private void ScheduleAutoReload()
	{
		lock (timerLock)
		{
			configReloadTimer?.Dispose();
			configReloadTimer = new System.Threading.Timer(_ =>
			{
				uiContext.Post(_ => RequestReload(notifyOnSuccess: true, autoReload: true), null);
			}, null, debounceDelay, Timeout.InfiniteTimeSpan);
		}
	}

	private void DisposeWatcher()
	{
		if (configFileWatcher is null)
		{
			return;
		}

		configFileWatcher.EnableRaisingEvents = false;
		configFileWatcher.Changed -= OnConfigFileChanged;
		configFileWatcher.Created -= OnConfigFileChanged;
		configFileWatcher.Renamed -= OnConfigFileChanged;
		configFileWatcher.Error -= OnConfigWatcherError;
		configFileWatcher.Dispose();
		configFileWatcher = null;
	}
}
