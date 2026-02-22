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

public class StartupShortcutRegistrationServiceTests
{
    [Test]
    public void Enable_CreatesShortcutAndMarksServiceEnabled()
    {
        var fileSystem = new FakeFileSystem();
        var shortcutWriter = new FakeStartupShortcutWriter(fileSystem);
        var service = CreateService(fileSystem, shortcutWriter);

        service.Enable();

        Assert.Multiple(() =>
        {
            Assert.That(fileSystem.EnsuredDirectories, Has.Member("C:\\Users\\pablo\\AppData\\Roaming\\Microsoft\\Windows\\Start Menu\\Programs\\Startup"));
            Assert.That(service.IsEnabled(), Is.True);
            Assert.That(shortcutWriter.WriteCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void Disable_WhenEnabled_RemovesShortcutAndMarksServiceDisabled()
    {
        var fileSystem = new FakeFileSystem();
        var shortcutWriter = new FakeStartupShortcutWriter(fileSystem);
        var service = CreateService(fileSystem, shortcutWriter);
        service.Enable();

        service.Disable();

        Assert.That(service.IsEnabled(), Is.False);
    }

    [Test]
    public void Disable_WhenAlreadyDisabled_IsNoOp()
    {
        var fileSystem = new FakeFileSystem();
        var shortcutWriter = new FakeStartupShortcutWriter(fileSystem);
        var service = CreateService(fileSystem, shortcutWriter);

        service.Disable();

        Assert.Multiple(() =>
        {
            Assert.That(service.IsEnabled(), Is.False);
            Assert.That(fileSystem.DeleteCalls, Is.EqualTo(0));
        });
    }

    [Test]
    public void Enable_IsIdempotentAndKeepsSingleShortcutPath()
    {
        var fileSystem = new FakeFileSystem();
        var shortcutWriter = new FakeStartupShortcutWriter(fileSystem);
        var service = CreateService(fileSystem, shortcutWriter);

        service.Enable();
        service.Enable();

        Assert.Multiple(() =>
        {
            Assert.That(service.IsEnabled(), Is.True);
            Assert.That(shortcutWriter.WriteCount, Is.EqualTo(2));
            Assert.That(fileSystem.Files.Count, Is.EqualTo(1));
        });
    }

    private static StartupShortcutRegistrationService CreateService(
        FakeFileSystem fileSystem,
        FakeStartupShortcutWriter shortcutWriter)
    {
        return new StartupShortcutRegistrationService(
            "C:\\Users\\pablo\\AppData\\Roaming\\Microsoft\\Windows\\Start Menu\\Programs\\Startup",
            "LoginShot.lnk",
            "C:\\Tools\\LoginShot\\LoginShot.exe",
            shortcutWriter,
            fileSystem);
    }

    private sealed class FakeStartupShortcutWriter : IStartupShortcutWriter
    {
        private readonly FakeFileSystem fileSystem;

        public FakeStartupShortcutWriter(FakeFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        public int WriteCount { get; private set; }

        public void WriteShortcut(string shortcutPath, string targetPath, string workingDirectory, string description)
        {
            WriteCount++;
            fileSystem.Files.Add(shortcutPath);
        }
    }

    private sealed class FakeFileSystem : IFileSystem
    {
        public HashSet<string> Files { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> EnsuredDirectories { get; } = new();
        public int DeleteCalls { get; private set; }

        public bool FileExists(string path)
        {
            return Files.Contains(path);
        }

        public void EnsureDirectory(string path)
        {
            EnsuredDirectories.Add(path);
        }

        public void DeleteFile(string path)
        {
            DeleteCalls++;
            Files.Remove(path);
        }
    }
}
