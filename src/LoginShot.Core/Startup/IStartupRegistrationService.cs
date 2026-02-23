namespace LoginShot.Startup;

public interface IStartupRegistrationService
{
	bool IsEnabled();
	void Enable();
	void Disable();
}
