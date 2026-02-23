using LoginShot.Util;
using Microsoft.Extensions.Logging;

namespace LoginShot.Tests;

public class LogFilePathProviderTests
{
	[Test]
	public void GetDailyLogFilePath_UsesExpectedDailyFileName()
	{
		var timestamp = new DateTimeOffset(2026, 2, 23, 8, 30, 15, TimeSpan.FromHours(-5));

		var path = LogFilePathProvider.GetDailyLogFilePath("C:\\Users\\pablo\\AppData\\Local\\LoginShot\\logs", timestamp);

		Assert.That(path, Is.EqualTo("C:\\Users\\pablo\\AppData\\Local\\LoginShot\\logs\\loginshot-2026-02-23.log"));
	}

	[Test]
	public void DailyFileLoggerProvider_AllowsOpeningCurrentLogWhileLoggingContinues()
	{
		var tempDirectory = Path.Combine(Path.GetTempPath(), "LoginShot.Tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempDirectory);

		try
		{
			var options = new FileLoggingOptions(tempDirectory, 14, 24);
			using var provider = new DailyFileLoggerProvider(options);
			var logger = provider.CreateLogger("LoginShot.Tests.Log");

			logger.LogInformation("first line");

			var logPath = LogFilePathProvider.GetDailyLogFilePath(tempDirectory, DateTimeOffset.Now);
			using (new FileStream(logPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
			{
				logger.LogInformation("second line");
			}

			provider.Dispose();

			var content = File.ReadAllText(logPath);
			Assert.Multiple(() =>
			{
				Assert.That(content, Does.Contain("first line"));
				Assert.That(content, Does.Contain("second line"));
			});
		}
		finally
		{
			Directory.Delete(tempDirectory, recursive: true);
		}
	}
}
