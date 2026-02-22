using LoginShot.AppLaunch;
using LoginShot.Storage;
using LoginShot.Startup;
using LoginShot.Triggers;

namespace LoginShot.Tests;

public class CaptureFileNameBuilderTests
{
    [Test]
    public void Build_UsesExpectedPattern()
    {
        var timestamp = new DateTimeOffset(2026, 2, 22, 14, 30, 0, TimeSpan.Zero);

        var fileName = CaptureFileNameBuilder.Build(timestamp, SessionEventType.Unlock, "jpg");

        Assert.That(fileName, Is.EqualTo("2026-02-22T14-30-00-unlock.jpg"));
    }

    [Test]
    public void Build_RemovesLeadingDotFromExtension()
    {
        var timestamp = new DateTimeOffset(2026, 2, 22, 14, 30, 0, TimeSpan.Zero);

        var fileName = CaptureFileNameBuilder.Build(timestamp, SessionEventType.Logon, ".jpg");

        Assert.That(fileName, Is.EqualTo("2026-02-22T14-30-00-logon.jpg"));
    }
}

public class TaskSchedulerStartupRegistrationServiceTests
{
    [Test]
    public void Enable_RegistersTaskAndMarksServiceEnabled()
    {
        var fileSystem = new FakeFileSystem();
        var schedulerClient = new FakeTaskSchedulerClient();
        var service = CreateService(fileSystem, schedulerClient);

        service.Enable();

        Assert.Multiple(() =>
        {
            Assert.That(service.IsEnabled(), Is.True);
            Assert.That(schedulerClient.RegisterCalls, Is.EqualTo(1));
            Assert.That(schedulerClient.LastArguments, Is.EqualTo("--startup-trigger=logon"));
        });
    }

    [Test]
    public void Disable_WhenEnabled_RemovesTaskAndMarksServiceDisabled()
    {
        var fileSystem = new FakeFileSystem();
        var schedulerClient = new FakeTaskSchedulerClient();
        var service = CreateService(fileSystem, schedulerClient);
        service.Enable();

        service.Disable();

        Assert.Multiple(() =>
        {
            Assert.That(service.IsEnabled(), Is.False);
            Assert.That(schedulerClient.DeleteCalls, Is.EqualTo(1));
        });
    }

    [Test]
    public void Disable_WhenAlreadyDisabled_IsNoOp()
    {
        var fileSystem = new FakeFileSystem();
        var schedulerClient = new FakeTaskSchedulerClient();
        var service = CreateService(fileSystem, schedulerClient);

        service.Disable();

        Assert.Multiple(() =>
        {
            Assert.That(service.IsEnabled(), Is.False);
            Assert.That(schedulerClient.DeleteCalls, Is.EqualTo(0));
        });
    }

    [Test]
    public void Enable_CleansUpLegacyShortcut()
    {
        var fileSystem = new FakeFileSystem();
        var schedulerClient = new FakeTaskSchedulerClient();
        var service = CreateService(fileSystem, schedulerClient);

        fileSystem.Files.Add("C:\\Users\\pablo\\AppData\\Roaming\\Microsoft\\Windows\\Start Menu\\Programs\\Startup\\LoginShot.lnk");

        service.Enable();

        Assert.That(fileSystem.Files, Is.Empty);
    }

    [Test]
    public void Disable_CleansUpLegacyShortcut()
    {
        var fileSystem = new FakeFileSystem();
        var schedulerClient = new FakeTaskSchedulerClient();
        var service = CreateService(fileSystem, schedulerClient);

        fileSystem.Files.Add("C:\\Users\\pablo\\AppData\\Roaming\\Microsoft\\Windows\\Start Menu\\Programs\\Startup\\LoginShot.lnk");
        service.Disable();

        Assert.Multiple(() =>
        {
            Assert.That(fileSystem.Files, Is.Empty);
            Assert.That(fileSystem.DeleteCalls, Is.EqualTo(1));
        });
    }

    private static TaskSchedulerStartupRegistrationService CreateService(
        FakeFileSystem fileSystem,
        FakeTaskSchedulerClient schedulerClient)
    {
        return new TaskSchedulerStartupRegistrationService(
            "LoginShot\\StartAfterLogin",
            "C:\\Tools\\LoginShot\\LoginShot.exe",
            "--startup-trigger=logon",
            "C:\\Users\\pablo\\AppData\\Roaming\\Microsoft\\Windows\\Start Menu\\Programs\\Startup\\LoginShot.lnk",
            schedulerClient,
            fileSystem);
    }

    private sealed class FakeTaskSchedulerClient : IStartupTaskSchedulerClient
    {
        private bool exists;
        private bool enabled;

        public int RegisterCalls { get; private set; }
        public int DeleteCalls { get; private set; }
        public string? LastArguments { get; private set; }

        public bool TaskExists(string taskName)
        {
            return exists;
        }

        public bool IsTaskEnabled(string taskName)
        {
            return enabled;
        }

        public void RegisterLogonTask(string taskName, string executablePath, string arguments, string description)
        {
            RegisterCalls++;
            exists = true;
            enabled = true;
            LastArguments = arguments;
        }

        public void DeleteTask(string taskName)
        {
            DeleteCalls++;
            exists = false;
            enabled = false;
        }
    }

    private sealed class FakeFileSystem : IFileSystem
    {
        public HashSet<string> Files { get; } = new(StringComparer.OrdinalIgnoreCase);
        public int DeleteCalls { get; private set; }

        public bool FileExists(string path)
        {
            return Files.Contains(path);
        }

        public void DeleteFile(string path)
        {
            DeleteCalls++;
            Files.Remove(path);
        }
    }
}

public class AppLaunchTriggerParserTests
{
    [Test]
    public void IsStartupLogonLaunch_WhenArgumentPresent_ReturnsTrue()
    {
        var args = new[] { "--startup-trigger=logon" };

        var result = AppLaunchTriggerParser.IsStartupLogonLaunch(args);

        Assert.That(result, Is.True);
    }

    [Test]
    public void IsStartupLogonLaunch_WhenArgumentMissing_ReturnsFalse()
    {
        var args = new[] { "--some-other-arg" };

        var result = AppLaunchTriggerParser.IsStartupLogonLaunch(args);

        Assert.That(result, Is.False);
    }
}

public class StartupLogonLaunchCoordinatorTests
{
    [Test]
    public async Task DispatchStartupLogonTriggerAsync_WhenStartupArgumentPresent_DispatchesLogonOnce()
    {
        var dispatcher = new FakeTriggerDispatcher();
        var coordinator = new StartupLogonLaunchCoordinator(dispatcher);

        await coordinator.DispatchStartupLogonTriggerAsync(new[] { "--startup-trigger=logon" });

        Assert.Multiple(() =>
        {
            Assert.That(dispatcher.DispatchCount, Is.EqualTo(1));
            Assert.That(dispatcher.LastEventType, Is.EqualTo(SessionEventType.Logon));
        });
    }

    [Test]
    public async Task DispatchStartupLogonTriggerAsync_WhenStartupArgumentMissing_DoesNotDispatch()
    {
        var dispatcher = new FakeTriggerDispatcher();
        var coordinator = new StartupLogonLaunchCoordinator(dispatcher);

        await coordinator.DispatchStartupLogonTriggerAsync(new[] { "--manual" });

        Assert.That(dispatcher.DispatchCount, Is.EqualTo(0));
    }

    private sealed class FakeTriggerDispatcher : ITriggerDispatcher
    {
        public int DispatchCount { get; private set; }
        public SessionEventType? LastEventType { get; private set; }

        public Task DispatchAsync(SessionEventType eventType, CancellationToken cancellationToken = default)
        {
            DispatchCount++;
            LastEventType = eventType;
            return Task.CompletedTask;
        }
    }
}
