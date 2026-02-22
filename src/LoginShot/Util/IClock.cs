namespace LoginShot.Util;

internal interface IClock
{
    DateTimeOffset UtcNow { get; }
}
