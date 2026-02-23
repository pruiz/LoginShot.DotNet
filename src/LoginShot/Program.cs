using LoginShot.App;
using LoginShot.AppLaunch;
using LoginShot.Capture;
using LoginShot.Config;
using LoginShot.Triggers;
using LoginShot.Util;
using Microsoft.Extensions.Logging;

namespace LoginShot;

internal static class Program
{
	[STAThread]
	private static void Main(string[] args)
	{
		ApplicationConfiguration.Initialize();

		var configLoader = CreateConfigLoader();
		LoginShotConfig config;
		try
		{
			config = configLoader.Load();
		}
		catch (ConfigValidationException exception)
		{
			MessageBox.Show(
				exception.Message,
				"LoginShot configuration error",
				MessageBoxButtons.OK,
				MessageBoxIcon.Error);
			return;
		}

		var fileLoggingOptions = new FileLoggingOptions(
			config.Logging.Directory,
			config.Logging.RetentionDays,
			config.Logging.CleanupIntervalHours);

		using var loggerFactory = LoggerFactory.Create(builder =>
		{
			builder.ClearProviders();
			// TODO: Evaluate replacing this custom provider with a battle-tested file sink (for example Serilog),
			// while keeping setup fully programmatic and without requiring a separate logging config file.
			builder.AddProvider(new DailyFileLoggerProvider(fileLoggingOptions));
		});

		using var logRetentionService = new LogRetentionService(fileLoggingOptions, loggerFactory.CreateLogger<LogRetentionService>());
		logRetentionService.Start();

		var triggerDispatcher = new StartupTriggerDispatcher(configLoader, loggerFactory.CreateLogger<StartupTriggerDispatcher>());
		var startupCoordinator = new StartupLogonLaunchCoordinator(triggerDispatcher);
		startupCoordinator.DispatchStartupLogonTriggerAsync(args).GetAwaiter().GetResult();

		try
		{
			Application.Run(new LoginShotApplicationContext(
				triggerDispatcher,
				configLoader,
				new YamlConfigWriter(),
				new OpenCvCameraDeviceEnumerator(),
				loggerFactory.CreateLogger<LoginShotApplicationContext>(),
				loggerFactory.CreateLogger<SessionEventRouter>()));
		}
		catch (ConfigValidationException exception)
		{
			MessageBox.Show(
				exception.Message,
				"LoginShot configuration error",
				MessageBoxButtons.OK,
				MessageBoxIcon.Error);
		}
	}

	private static IConfigLoader CreateConfigLoader()
	{
		var userProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
		var localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		var fileProvider = new SystemConfigFileProvider();
		var pathResolver = new ConfigPathResolver(userProfilePath, appDataPath, localAppDataPath, fileProvider);
		return new LoginShotConfigLoader(pathResolver, fileProvider);
	}
}
