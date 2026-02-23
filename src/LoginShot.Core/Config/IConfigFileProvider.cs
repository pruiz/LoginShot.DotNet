namespace LoginShot.Config;

public interface IConfigFileProvider
{
	bool FileExists(string path);
	string ReadAllText(string path);
}
