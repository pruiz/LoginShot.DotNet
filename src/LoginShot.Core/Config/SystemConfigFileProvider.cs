namespace LoginShot.Config;

public sealed class SystemConfigFileProvider : IConfigFileProvider
{
	public bool FileExists(string path)
	{
		return File.Exists(path);
	}

	public string ReadAllText(string path)
	{
		return File.ReadAllText(path);
	}
}
