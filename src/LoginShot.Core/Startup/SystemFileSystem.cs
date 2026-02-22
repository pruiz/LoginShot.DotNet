namespace LoginShot.Startup;

public sealed class SystemFileSystem : IFileSystem
{
    public bool FileExists(string path)
    {
        return File.Exists(path);
    }

    public void EnsureDirectory(string path)
    {
        Directory.CreateDirectory(path);
    }

    public void DeleteFile(string path)
    {
        File.Delete(path);
    }
}
