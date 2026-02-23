namespace LoginShot.AppLaunch;

public static class AppLaunchTriggerParser
{
	public static bool IsStartupLogonLaunch(IEnumerable<string> args)
	{
		return args.Any(arg => string.Equals(arg, "--startup-trigger=logon", StringComparison.OrdinalIgnoreCase));
	}
}
