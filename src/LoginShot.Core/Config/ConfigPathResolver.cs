namespace LoginShot.Config;

public sealed class ConfigPathResolver
{
    private readonly string userProfilePath;
    private readonly string appDataPath;
    private readonly IConfigFileProvider fileProvider;

    public ConfigPathResolver(string userProfilePath, string appDataPath, IConfigFileProvider fileProvider)
    {
        this.userProfilePath = userProfilePath;
        this.appDataPath = appDataPath;
        this.fileProvider = fileProvider;
    }

    public string UserProfilePath => userProfilePath;

    public IReadOnlyList<string> GetSearchPaths()
    {
        return new[]
        {
            Path.Combine(userProfilePath, ".config", "LoginShot", "config.yml"),
            Path.Combine(appDataPath, "LoginShot", "config.yml")
        };
    }

    public string? ResolveFirstExistingPath()
    {
        foreach (var path in GetSearchPaths())
        {
            if (fileProvider.FileExists(path))
            {
                return path;
            }
        }

        return null;
    }

    public string ExpandKnownVariables(string value)
    {
        return value
            .Replace("%USERPROFILE%", userProfilePath, StringComparison.OrdinalIgnoreCase)
            .Replace("%APPDATA%", appDataPath, StringComparison.OrdinalIgnoreCase);
    }
}
