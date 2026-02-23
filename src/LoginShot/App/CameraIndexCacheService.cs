using LoginShot.Capture;
using Microsoft.Extensions.Logging;

namespace LoginShot.App;

internal readonly record struct CameraIndexSnapshot(IReadOnlyList<CameraDeviceDescriptor> Devices, bool IsRefreshing);

internal sealed class CameraIndexCacheService
{
	private readonly ICameraDeviceEnumerator cameraDeviceEnumerator;
	private readonly ILogger logger;
	private readonly int cameraIndexProbeCount;
	private readonly TimeSpan refreshInterval;
	private readonly object syncLock = new();
	private IReadOnlyList<CameraDeviceDescriptor> cachedDevices = Array.Empty<CameraDeviceDescriptor>();
	private DateTimeOffset? lastRefreshUtc;
	private bool isRefreshing;

	public CameraIndexCacheService(
		ICameraDeviceEnumerator cameraDeviceEnumerator,
		ILogger logger,
		int cameraIndexProbeCount,
		TimeSpan refreshInterval)
	{
		this.cameraDeviceEnumerator = cameraDeviceEnumerator;
		this.logger = logger;
		this.cameraIndexProbeCount = cameraIndexProbeCount;
		this.refreshInterval = refreshInterval;
	}

	public CameraIndexSnapshot GetSnapshotAndRefreshIfNeeded(Action onRefreshed)
	{
		var shouldStartRefresh = false;
		lock (syncLock)
		{
			if (!isRefreshing && (lastRefreshUtc is null || DateTimeOffset.UtcNow - lastRefreshUtc >= refreshInterval))
			{
				isRefreshing = true;
				shouldStartRefresh = true;
			}
		}

		if (shouldStartRefresh)
		{
			_ = Task.Run(() => RefreshInBackground(onRefreshed));
		}

		lock (syncLock)
		{
			return new CameraIndexSnapshot(cachedDevices, isRefreshing);
		}
	}

	private void RefreshInBackground(Action onRefreshed)
	{
		IReadOnlyList<CameraDeviceDescriptor> devices;
		try
		{
			devices = cameraDeviceEnumerator.EnumerateDevices(cameraIndexProbeCount);
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Failed to enumerate camera devices");
			devices = Array.Empty<CameraDeviceDescriptor>();
		}

		lock (syncLock)
		{
			cachedDevices = devices;
			lastRefreshUtc = DateTimeOffset.UtcNow;
			isRefreshing = false;
		}

		onRefreshed();
	}
}
