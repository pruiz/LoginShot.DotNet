namespace LoginShot.Config;

internal interface IConfigWriter
{
    string Save(LoginShotConfig config, string? preferredPath);
}
