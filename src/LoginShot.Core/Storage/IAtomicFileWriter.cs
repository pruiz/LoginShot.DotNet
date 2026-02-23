namespace LoginShot.Storage;

public interface IAtomicFileWriter
{
	void EnsureDirectory(string directoryPath);
	void WriteAllBytesAtomic(string path, byte[] bytes);
	void WriteAllTextAtomic(string path, string content);
}
