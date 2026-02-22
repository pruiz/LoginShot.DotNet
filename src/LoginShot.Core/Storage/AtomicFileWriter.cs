namespace LoginShot.Storage;

public sealed class AtomicFileWriter : IAtomicFileWriter
{
    public void EnsureDirectory(string directoryPath)
    {
        Directory.CreateDirectory(directoryPath);
    }

    public void WriteAllBytesAtomic(string path, byte[] bytes)
    {
        var tempPath = GetTempPath(path);
        File.WriteAllBytes(tempPath, bytes);
        ReplaceFile(tempPath, path);
    }

    public void WriteAllTextAtomic(string path, string content)
    {
        var tempPath = GetTempPath(path);
        File.WriteAllText(tempPath, content);
        ReplaceFile(tempPath, path);
    }

    private static string GetTempPath(string path)
    {
        var directory = Path.GetDirectoryName(path) ?? throw new InvalidOperationException("Path does not have a directory.");
        var fileName = Path.GetFileName(path);
        var tempName = $".{fileName}.{Guid.NewGuid():N}.tmp";
        return Path.Combine(directory, tempName);
    }

    private static void ReplaceFile(string tempPath, string destinationPath)
    {
        try
        {
            File.Move(tempPath, destinationPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}
