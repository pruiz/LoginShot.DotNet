using DirectShowLib;
using OpenCvSharp;

namespace LoginShot.Capture;

internal interface ICameraDeviceEnumerator
{
	IReadOnlyList<CameraDeviceDescriptor> EnumerateDevices(int maxIndexExclusive = 10);
}

internal readonly record struct CameraDeviceDescriptor(int Index, string? Name);

internal sealed class OpenCvCameraDeviceEnumerator : ICameraDeviceEnumerator
{
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
		}

		var friendlyNames = GetFriendlyCameraNames();
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
				if (!string.IsNullOrWhiteSpace(device.Name))
				{
					names.Add(device.Name);
				}
			}

			return names;
		}
		catch
		{
			return Array.Empty<string>();
		}
	}
}
