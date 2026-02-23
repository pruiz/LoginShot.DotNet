using LoginShot.Config;

namespace LoginShot.Core.Tests;

public class ConfigPathResolverTests
{
	[Test]
	public void ResolveFirstExistingPath_UsesFirstMatchInPriorityOrder()
	{
		var provider = new FakeConfigFileProvider();
		var resolver = new ConfigPathResolver("C:\\Users\\pablo", "C:\\Users\\pablo\\AppData\\Roaming", "C:\\Users\\pablo\\AppData\\Local", provider);
		var searchPaths = resolver.GetSearchPaths();
		provider.Files[searchPaths[1]] = "ui: {}";
		provider.Files[searchPaths[0]] = "ui: {}";

		var resolvedPath = resolver.ResolveFirstExistingPath();

		Assert.That(resolvedPath, Is.EqualTo(searchPaths[0]));
	}

	[Test]
	public void ExpandKnownVariables_ReplacesUserProfileAndAppDataTokens()
	{
		var provider = new FakeConfigFileProvider();
		var resolver = new ConfigPathResolver("C:\\Users\\pablo", "C:\\Users\\pablo\\AppData\\Roaming", "C:\\Users\\pablo\\AppData\\Local", provider);

		var expandedPath = resolver.ExpandKnownVariables("%USERPROFILE%\\Pictures\\LoginShot;%APPDATA%\\LoginShot;%LOCALAPPDATA%\\LoginShot\\logs");

		Assert.That(expandedPath, Is.EqualTo("C:\\Users\\pablo\\Pictures\\LoginShot;C:\\Users\\pablo\\AppData\\Roaming\\LoginShot;C:\\Users\\pablo\\AppData\\Local\\LoginShot\\logs"));
	}
}

public class LoginShotConfigLoaderTests
{
	[Test]
	public void Load_WhenNoConfigFileExists_ReturnsDefaults()
	{
		var provider = new FakeConfigFileProvider();
		var resolver = new ConfigPathResolver("C:\\Users\\pablo", "C:\\Users\\pablo\\AppData\\Roaming", "C:\\Users\\pablo\\AppData\\Local", provider);
		var loader = new LoginShotConfigLoader(resolver, provider);

		var config = loader.Load();

		Assert.Multiple(() =>
		{
			Assert.That(config.SourcePath, Is.Null);
			Assert.That(config.Output.Directory, Is.EqualTo(Path.Combine("C:\\Users\\pablo", "Pictures", "LoginShot")));
			Assert.That(config.Output.Format, Is.EqualTo("jpg"));
			Assert.That(config.Triggers.OnLogon, Is.True);
			Assert.That(config.Capture.DebounceSeconds, Is.EqualTo(3));
			Assert.That(config.Capture.Backend, Is.EqualTo("opencv"));
			Assert.That(config.Capture.CameraIndex, Is.Null);
			Assert.That(config.Logging.Directory, Is.EqualTo(Path.Combine("C:\\Users\\pablo\\AppData\\Local", "LoginShot", "logs")));
			Assert.That(config.Logging.RetentionDays, Is.EqualTo(14));
			Assert.That(config.Logging.CleanupIntervalHours, Is.EqualTo(24));
			Assert.That(config.Watermark.Enabled, Is.True);
			Assert.That(config.Watermark.Format, Is.EqualTo("yyyy-MM-dd HH:mm:ss zzz"));
		});
	}

	[Test]
	public void Load_WhenYamlOverridesSubset_MergesWithDefaults()
	{
		var provider = new FakeConfigFileProvider();
		var resolver = new ConfigPathResolver("C:\\Users\\pablo", "C:\\Users\\pablo\\AppData\\Roaming", "C:\\Users\\pablo\\AppData\\Local", provider);
		provider.Files[resolver.GetSearchPaths()[0]] =
			"output:\n" +
			"  directory: \"%APPDATA%\\\\Custom\\\\Shots\"\n" +
			"capture:\n" +
			"  debounceSeconds: 7\n";
		var loader = new LoginShotConfigLoader(resolver, provider);

		var config = loader.Load();

		Assert.Multiple(() =>
		{
			Assert.That(config.SourcePath, Is.EqualTo(resolver.GetSearchPaths()[0]));
			Assert.That(TestPathUtil.NormalizePath(config.Output.Directory), Is.EqualTo(TestPathUtil.NormalizePath("C:\\Users\\pablo\\AppData\\Roaming\\Custom\\Shots")));
			Assert.That(config.Capture.DebounceSeconds, Is.EqualTo(7));
			Assert.That(config.Output.Format, Is.EqualTo("jpg"));
			Assert.That(config.Triggers.OnUnlock, Is.True);
		});
	}

	[Test]
	public void Load_WhenDirectoriesUseForwardSlashes_NormalizesToWindowsSeparators()
	{
		var provider = new FakeConfigFileProvider();
		var resolver = new ConfigPathResolver("C:\\Users\\pablo", "C:\\Users\\pablo\\AppData\\Roaming", "C:\\Users\\pablo\\AppData\\Local", provider);
		provider.Files[resolver.GetSearchPaths()[0]] =
			"output:\n" +
			"  directory: \"C:/Users/pablo/Pictures/LoginShot\"\n" +
			"logging:\n" +
			"  directory: \"%LOCALAPPDATA%/LoginShot/logs\"\n";
		var loader = new LoginShotConfigLoader(resolver, provider);

		var config = loader.Load();

		Assert.Multiple(() =>
		{
			Assert.That(config.Output.Directory, Is.EqualTo("C:\\Users\\pablo\\Pictures\\LoginShot"));
			Assert.That(config.Logging.Directory, Is.EqualTo("C:\\Users\\pablo\\AppData\\Local\\LoginShot\\logs"));
		});
	}

	[Test]
	public void Load_WhenWatermarkOverridesSpecified_UsesConfiguredValues()
	{
		var provider = new FakeConfigFileProvider();
		var resolver = new ConfigPathResolver("C:\\Users\\pablo", "C:\\Users\\pablo\\AppData\\Roaming", "C:\\Users\\pablo\\AppData\\Local", provider);
		provider.Files[resolver.GetSearchPaths()[0]] =
			"watermark:\n" +
			"  enabled: false\n" +
			"  format: \"yyyy/MM/dd HH:mm:ss zzz\"\n";
		var loader = new LoginShotConfigLoader(resolver, provider);

		var config = loader.Load();

		Assert.Multiple(() =>
		{
			Assert.That(config.Watermark.Enabled, Is.False);
			Assert.That(config.Watermark.Format, Is.EqualTo("yyyy/MM/dd HH:mm:ss zzz"));
		});
	}

	[Test]
	public void Load_WhenFormatInvalid_ThrowsValidationException()
	{
		var provider = new FakeConfigFileProvider();
		var resolver = new ConfigPathResolver("C:\\Users\\pablo", "C:\\Users\\pablo\\AppData\\Roaming", "C:\\Users\\pablo\\AppData\\Local", provider);
		provider.Files[resolver.GetSearchPaths()[0]] =
			"output:\n" +
			"  format: \"png\"\n";
		var loader = new LoginShotConfigLoader(resolver, provider);

		var exception = Assert.Throws<ConfigValidationException>(() => loader.Load());

		Assert.That(exception!.Message, Does.Contain("output.format must be 'jpg'"));
	}

	[Test]
	public void Load_WhenJpegQualityInvalid_ThrowsValidationException()
	{
		var provider = new FakeConfigFileProvider();
		var resolver = new ConfigPathResolver("C:\\Users\\pablo", "C:\\Users\\pablo\\AppData\\Roaming", "C:\\Users\\pablo\\AppData\\Local", provider);
		provider.Files[resolver.GetSearchPaths()[0]] =
			"output:\n" +
			"  jpegQuality: 1.5\n";
		var loader = new LoginShotConfigLoader(resolver, provider);

		var exception = Assert.Throws<ConfigValidationException>(() => loader.Load());

		Assert.That(exception!.Message, Does.Contain("output.jpegQuality must be between 0.0 and 1.0"));
	}

	[Test]
	public void Load_WhenYamlInvalid_ThrowsValidationException()
	{
		var provider = new FakeConfigFileProvider();
		var resolver = new ConfigPathResolver("C:\\Users\\pablo", "C:\\Users\\pablo\\AppData\\Roaming", "C:\\Users\\pablo\\AppData\\Local", provider);
		provider.Files[resolver.GetSearchPaths()[0]] = "output: [bad";
		var loader = new LoginShotConfigLoader(resolver, provider);

		var exception = Assert.Throws<ConfigValidationException>(() => loader.Load());

		Assert.That(exception!.Message, Does.Contain("Failed to parse config"));
	}

	[Test]
	public void Load_WhenCaptureBackendInvalid_ThrowsValidationException()
	{
		var provider = new FakeConfigFileProvider();
		var resolver = new ConfigPathResolver("C:\\Users\\pablo", "C:\\Users\\pablo\\AppData\\Roaming", "C:\\Users\\pablo\\AppData\\Local", provider);
		provider.Files[resolver.GetSearchPaths()[0]] =
			"capture:\n" +
			"  backend: \"not-a-backend\"\n";
		var loader = new LoginShotConfigLoader(resolver, provider);

		var exception = Assert.Throws<ConfigValidationException>(() => loader.Load());

		Assert.That(exception!.Message, Does.Contain("capture.backend must be either 'opencv' or 'winrt-mediacapture'"));
	}

	[Test]
	public void Load_WhenCaptureCameraIndexNegative_ThrowsValidationException()
	{
		var provider = new FakeConfigFileProvider();
		var resolver = new ConfigPathResolver("C:\\Users\\pablo", "C:\\Users\\pablo\\AppData\\Roaming", "C:\\Users\\pablo\\AppData\\Local", provider);
		provider.Files[resolver.GetSearchPaths()[0]] =
			"capture:\n" +
			"  cameraIndex: -1\n";
		var loader = new LoginShotConfigLoader(resolver, provider);

		var exception = Assert.Throws<ConfigValidationException>(() => loader.Load());

		Assert.That(exception!.Message, Does.Contain("capture.cameraIndex must be 0 or greater when provided."));
	}

	[Test]
	public void Load_WhenLoggingRetentionDaysInvalid_ThrowsValidationException()
	{
		var provider = new FakeConfigFileProvider();
		var resolver = new ConfigPathResolver("C:\\Users\\pablo", "C:\\Users\\pablo\\AppData\\Roaming", "C:\\Users\\pablo\\AppData\\Local", provider);
		provider.Files[resolver.GetSearchPaths()[0]] =
			"logging:\n" +
			"  retentionDays: 0\n";
		var loader = new LoginShotConfigLoader(resolver, provider);

		var exception = Assert.Throws<ConfigValidationException>(() => loader.Load());

		Assert.That(exception!.Message, Does.Contain("logging.retentionDays must be 1 or greater."));
	}

	[Test]
	public void Load_WhenLoggingCleanupIntervalInvalid_ThrowsValidationException()
	{
		var provider = new FakeConfigFileProvider();
		var resolver = new ConfigPathResolver("C:\\Users\\pablo", "C:\\Users\\pablo\\AppData\\Roaming", "C:\\Users\\pablo\\AppData\\Local", provider);
		provider.Files[resolver.GetSearchPaths()[0]] =
			"logging:\n" +
			"  cleanupIntervalHours: 0\n";
		var loader = new LoginShotConfigLoader(resolver, provider);

		var exception = Assert.Throws<ConfigValidationException>(() => loader.Load());

		Assert.That(exception!.Message, Does.Contain("logging.cleanupIntervalHours must be 1 or greater."));
	}

	[Test]
	public void Load_WhenWatermarkFormatEmpty_ThrowsValidationException()
	{
		var provider = new FakeConfigFileProvider();
		var resolver = new ConfigPathResolver("C:\\Users\\pablo", "C:\\Users\\pablo\\AppData\\Roaming", "C:\\Users\\pablo\\AppData\\Local", provider);
		provider.Files[resolver.GetSearchPaths()[0]] =
			"watermark:\n" +
			"  format: \"\"\n";
		var loader = new LoginShotConfigLoader(resolver, provider);

		var exception = Assert.Throws<ConfigValidationException>(() => loader.Load());

		Assert.That(exception!.Message, Does.Contain("watermark.format must not be empty."));
	}

}

internal static class TestPathUtil
{
	public static string NormalizePath(string path)
	{
		return path.Replace('\\', '/');
	}
}

internal sealed class FakeConfigFileProvider : IConfigFileProvider
{
	public Dictionary<string, string> Files { get; } = new(StringComparer.OrdinalIgnoreCase);

	public bool FileExists(string path)
	{
		return Files.ContainsKey(path);
	}

	public string ReadAllText(string path)
	{
		return Files[path];
	}
}
