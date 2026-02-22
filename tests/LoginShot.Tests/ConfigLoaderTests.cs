using LoginShot.Config;

namespace LoginShot.Tests;

public class ConfigPathResolverTests
{
    [Test]
    public void ResolveFirstExistingPath_UsesFirstMatchInPriorityOrder()
    {
        var provider = new FakeConfigFileProvider();
        var resolver = new ConfigPathResolver("C:\\Users\\pablo", "C:\\Users\\pablo\\AppData\\Roaming", provider);
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
        var resolver = new ConfigPathResolver("C:\\Users\\pablo", "C:\\Users\\pablo\\AppData\\Roaming", provider);

        var expandedPath = resolver.ExpandKnownVariables("%USERPROFILE%\\Pictures\\LoginShot;%APPDATA%\\LoginShot");

        Assert.That(expandedPath, Is.EqualTo("C:\\Users\\pablo\\Pictures\\LoginShot;C:\\Users\\pablo\\AppData\\Roaming\\LoginShot"));
    }
}

public class LoginShotConfigLoaderTests
{
    [Test]
    public void Load_WhenNoConfigFileExists_ReturnsDefaults()
    {
        var provider = new FakeConfigFileProvider();
        var resolver = new ConfigPathResolver("C:\\Users\\pablo", "C:\\Users\\pablo\\AppData\\Roaming", provider);
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
        });
    }

    [Test]
    public void Load_WhenYamlOverridesSubset_MergesWithDefaults()
    {
        var provider = new FakeConfigFileProvider();
        var resolver = new ConfigPathResolver("C:\\Users\\pablo", "C:\\Users\\pablo\\AppData\\Roaming", provider);
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
    public void Load_WhenFormatInvalid_ThrowsValidationException()
    {
        var provider = new FakeConfigFileProvider();
        var resolver = new ConfigPathResolver("C:\\Users\\pablo", "C:\\Users\\pablo\\AppData\\Roaming", provider);
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
        var resolver = new ConfigPathResolver("C:\\Users\\pablo", "C:\\Users\\pablo\\AppData\\Roaming", provider);
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
        var resolver = new ConfigPathResolver("C:\\Users\\pablo", "C:\\Users\\pablo\\AppData\\Roaming", provider);
        provider.Files[resolver.GetSearchPaths()[0]] = "output: [bad";
        var loader = new LoginShotConfigLoader(resolver, provider);

        var exception = Assert.Throws<ConfigValidationException>(() => loader.Load());

        Assert.That(exception!.Message, Does.Contain("Failed to parse config"));
    }

    [Test]
    public void Load_WhenCaptureBackendInvalid_ThrowsValidationException()
    {
        var provider = new FakeConfigFileProvider();
        var resolver = new ConfigPathResolver("C:\\Users\\pablo", "C:\\Users\\pablo\\AppData\\Roaming", provider);
        provider.Files[resolver.GetSearchPaths()[0]] =
            "capture:\n" +
            "  backend: \"not-a-backend\"\n";
        var loader = new LoginShotConfigLoader(resolver, provider);

        var exception = Assert.Throws<ConfigValidationException>(() => loader.Load());

        Assert.That(exception!.Message, Does.Contain("capture.backend must be either 'opencv' or 'winrt-mediacapture'"));
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
