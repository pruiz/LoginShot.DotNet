using DirectShowLib;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace LoginShot.Capture;

internal interface ICameraDeviceEnumerator
{
	IReadOnlyList<CameraDeviceDescriptor> EnumerateDevices(int maxIndexExclusive = 10);
}

internal readonly record struct CameraDeviceDescriptor(int Index, string? Name);

internal sealed class OpenCvCameraDeviceEnumerator : ICameraDeviceEnumerator
{
	private readonly ILogger logger;

	public OpenCvCameraDeviceEnumerator(ILogger logger)
	{
		this.logger = logger;
	}

	public IReadOnlyList<CameraDeviceDescriptor> EnumerateDevices(int maxIndexExclusive = 10)
	{
		var openCvIndexes = new List<int>();

		for (var index = 0; index < maxIndexExclusive; index++)
		{
			using var capture = new VideoCapture(index);
			if (capture.IsOpened())
			{
				openCvIndexes.Add(index);
			}

			logger.LogDebug("Camera probe index={Index}, opened={Opened}", index, capture.IsOpened());
		}

		var friendlyNames = GetFriendlyCameraNames();
		if (friendlyNames.Count != openCvIndexes.Count)
		{
			logger.LogInformation(
				"Friendly camera name count mismatch. openCvCount={OpenCvCount}, directShowCount={DirectShowCount}. Omitting friendly names.",
				openCvIndexes.Count,
				friendlyNames.Count);
			friendlyNames = Array.Empty<string>();
		}
		else
		{
			logger.LogInformation("Friendly camera names mapped successfully. count={Count}", friendlyNames.Count);
		}

		var result = new List<CameraDeviceDescriptor>(openCvIndexes.Count);
		for (var i = 0; i < openCvIndexes.Count; i++)
		{
			var index = openCvIndexes[i];
			var name = i < friendlyNames.Count ? friendlyNames[i] : null;
			result.Add(new CameraDeviceDescriptor(index, name));
		}

		return result;
	}

	private static IReadOnlyList<string> GetFriendlyCameraNames()
	{
		try
		{
			var devices = DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice);
			var names = new List<string>(devices.Length);
			foreach (var device in devices)
			{
				names.Add(device.Name ?? string.Empty);
			}

			return names;
		}
		catch
		{
			return Array.Empty<string>();
		}
	}
}
