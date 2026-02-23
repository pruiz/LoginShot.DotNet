namespace LoginShot.Config;

public sealed class ConfigPathResolver
{
	private readonly string userProfilePath;
	private readonly string appDataPath;
	private readonly string localAppDataPath;
	private readonly IConfigFileProvider fileProvider;

	public ConfigPathResolver(string userProfilePath, string appDataPath, string localAppDataPath, IConfigFileProvider fileProvider)
	{
		this.userProfilePath = userProfilePath;
		this.appDataPath = appDataPath;
		this.localAppDataPath = localAppDataPath;
		this.fileProvider = fileProvider;
	}

	public string UserProfilePath => userProfilePath;
	public string LocalAppDataPath => localAppDataPath;

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
			.Replace("%APPDATA%", appDataPath, StringComparison.OrdinalIgnoreCase)
			.Replace("%LOCALAPPDATA%", localAppDataPath, StringComparison.OrdinalIgnoreCase);
	}

	public string NormalizeWindowsPathSeparators(string value)
	{
		return value.Replace('/', '\\');
	}
}
