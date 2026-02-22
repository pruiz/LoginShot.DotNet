namespace LoginShot.Config;

public sealed class ConfigValidationException : Exception
{
    public ConfigValidationException(string message)
        : base(message)
    {
    }

    public ConfigValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
