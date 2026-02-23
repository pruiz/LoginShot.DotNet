using OpenCvSharp;

namespace LoginShot.Capture;

internal interface ICameraDeviceEnumerator
{
	IReadOnlyList<int> EnumerateIndexes(int maxIndexExclusive = 10);
}

internal sealed class OpenCvCameraDeviceEnumerator : ICameraDeviceEnumerator
{
	public IReadOnlyList<int> EnumerateIndexes(int maxIndexExclusive = 10)
	{
		var result = new List<int>();

		for (var index = 0; index < maxIndexExclusive; index++)
		{
			using var capture = new VideoCapture(index);
			if (capture.IsOpened())
			{
				result.Add(index);
			}
		}

		return result;
	}
}
