using System.Reflection;
using LoginShot.App;
using LoginShot.Config;
using Microsoft.Extensions.Logging.Abstractions;

namespace LoginShot.Tests;

public class ConfigReloadCoordinatorTests
{
    [Test]
    public void RequestReload_WhenLoadSucceeds_InvokesSuccessCallbackWithFlags()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var expectedConfig = CreateConfig(Path.Combine(tempDirectory, "config.yml"));
            LoginShotConfig? succeededConfig = null;
            bool? notifyFlag = null;
            bool? autoFlag = null;
            Exception? failure = null;

            using var coordinator = new ConfigReloadCoordinator(
                new ImmediateSynchronizationContext(),
                () => expectedConfig,
                (config, notifyOnSuccess, autoReload) =>
                {
                    succeededConfig = config;
                    notifyFlag = notifyOnSuccess;
                    autoFlag = autoReload;
                },
                (exception, _, _) => failure = exception,
                _ => { },
                NullLogger.Instance,
                TimeSpan.FromMilliseconds(25));

            coordinator.RequestReload(notifyOnSuccess: true, autoReload: false);

            Assert.Multiple(() =>
            {
                Assert.That(failure, Is.Null);
                Assert.That(succeededConfig, Is.EqualTo(expectedConfig));
                Assert.That(notifyFlag, Is.True);
                Assert.That(autoFlag, Is.False);
            });
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void RequestReload_WhenLoadFails_InvokesFailureCallbackWithFlags()
    {
        Exception? failure = null;
        bool? notifyFlag = null;
        bool? autoFlag = null;
        var expectedException = new InvalidOperationException("invalid yaml");

        using var coordinator = new ConfigReloadCoordinator(
            new ImmediateSynchronizationContext(),
            () => throw expectedException,
            (_, _, _) => Assert.Fail("Expected failure callback."),
            (exception, notifyOnSuccess, autoReload) =>
            {
                failure = exception;
                notifyFlag = notifyOnSuccess;
                autoFlag = autoReload;
            },
            _ => { },
            NullLogger.Instance,
            TimeSpan.FromMilliseconds(25));

        coordinator.RequestReload(notifyOnSuccess: false, autoReload: true);

        Assert.Multiple(() =>
        {
            Assert.That(failure, Is.SameAs(expectedException));
            Assert.That(notifyFlag, Is.False);
            Assert.That(autoFlag, Is.True);
        });
    }

    [Test]
    public void Bind_WithNullPath_DoesNotThrowAfterBeingBound()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var path = Path.Combine(tempDirectory, "config.yml");
            File.WriteAllText(path, "ui: {}\n");

            using var coordinator = new ConfigReloadCoordinator(
                new ImmediateSynchronizationContext(),
                () => CreateConfig(path),
                (_, _, _) => { },
                (_, _, _) => { },
                _ => { },
                NullLogger.Instance,
                TimeSpan.FromMilliseconds(25));

            coordinator.Bind(path);

            Assert.DoesNotThrow(() => coordinator.Bind(null));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void ConfigChange_DebouncesRapidSignalsToSingleReload()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var loadCount = 0;
            Exception? failure = null;
            using var reloadObserved = new ManualResetEventSlim(false);

            using var coordinator = new ConfigReloadCoordinator(
                new ImmediateSynchronizationContext(),
                () =>
                {
                    Interlocked.Increment(ref loadCount);
                    return CreateConfig(Path.Combine(tempDirectory, "config.yml"));
                },
                (_, _, _) => reloadObserved.Set(),
                (exception, _, _) =>
                {
                    failure = exception;
                    reloadObserved.Set();
                },
                _ => { },
                NullLogger.Instance,
                TimeSpan.FromMilliseconds(40));

            var method = typeof(ConfigReloadCoordinator).GetMethod("OnConfigFileChanged", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Could not access OnConfigFileChanged.");
            var args = new FileSystemEventArgs(WatcherChangeTypes.Changed, tempDirectory, "config.yml");

            method.Invoke(coordinator, new object[] { this, args });
            method.Invoke(coordinator, new object[] { this, args });
            method.Invoke(coordinator, new object[] { this, args });

            Assert.That(reloadObserved.Wait(TimeSpan.FromSeconds(2)), Is.True, "Debounced reload was not observed.");
            Thread.Sleep(150);

            Assert.Multiple(() =>
            {
                Assert.That(failure, Is.Null);
                Assert.That(loadCount, Is.EqualTo(1));
            });
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static LoginShotConfig CreateConfig(string sourcePath)
    {
        return new LoginShotConfig(
            new OutputConfig("C:\\Users\\pablo\\Pictures\\LoginShot", "jpg", 1280, 0.85),
            new TriggerConfig(true, true, true),
            new MetadataConfig(true),
            new UiConfig(true, false),
            new CaptureConfig(3, "opencv", null),
            new LoggingConfig("C:\\Users\\pablo\\AppData\\Local\\LoginShot\\logs", 14, 24),
            new WatermarkConfig(true, "yyyy-MM-dd HH:mm:ss zzz"),
            sourcePath);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "LoginShot.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class ImmediateSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback callback, object? state)
        {
            callback(state);
        }
    }
}
