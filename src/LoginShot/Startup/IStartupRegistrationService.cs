namespace LoginShot.Startup;

internal interface IStartupRegistrationService
{
    bool IsEnabled();
    void Enable();
    void Disable();
}
