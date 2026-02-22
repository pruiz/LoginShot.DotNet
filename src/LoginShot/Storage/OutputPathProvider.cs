namespace LoginShot.Storage;

internal static class OutputPathProvider
{
    public static string GetDefaultOutputDirectory()
    {
        var picturesPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        return Path.Combine(picturesPath, "LoginShot");
    }
}
