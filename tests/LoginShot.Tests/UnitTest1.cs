using LoginShot.Storage;
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
