namespace LoginShot.Config;

public static class LoginShotConfigDefaults
{
	public static LoginShotConfig Create(string userProfilePath, string localAppDataPath)
	{
		return new LoginShotConfig(
			new OutputConfig(
				Path.Combine(userProfilePath, "Pictures", "LoginShot"),
				"jpg",
				1280,
				0.85),
			new TriggerConfig(true, true, true),
			new MetadataConfig(true),
			new UiConfig(true, false),
			new CaptureConfig(
				3,
				"opencv",
				null,
				new CaptureNegotiationConfig(
					BackendOrder: ["dshow", "msmf", "any"],
					PixelFormats: ["auto", "MJPG", "YUY2", "NV12"],
					ConvertRgbMode: "auto",
					Resolutions: ["auto", "1280x720", "640x480"],
					AttemptsPerCombination: 2,
					WarmupFrames: 6)),
			new LoggingConfig(Path.Combine(localAppDataPath, "LoginShot", "logs"), 14, 24, "Information"),
			new WatermarkConfig(true, "yyyy-MM-dd HH:mm:ss zzz"),
			SourcePath: null);
	}
}
