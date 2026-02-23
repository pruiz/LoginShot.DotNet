using LoginShot.Capture;
using Microsoft.Extensions.Logging;

namespace LoginShot.App;

internal readonly record struct CameraIndexSnapshot(IReadOnlyList<int> Indexes, bool IsRefreshing);

internal sealed class CameraIndexCacheService
{
	private readonly ICameraDeviceEnumerator cameraDeviceEnumerator;
	private readonly ILogger logger;
	private readonly int cameraIndexProbeCount;
	private readonly TimeSpan refreshInterval;
	private readonly object syncLock = new();
	private IReadOnlyList<int> cachedIndexes = Array.Empty<int>();
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
			return new CameraIndexSnapshot(cachedIndexes, isRefreshing);
		}
	}

	private void RefreshInBackground(Action onRefreshed)
	{
		IReadOnlyList<int> indexes;
		try
		{
			indexes = cameraDeviceEnumerator.EnumerateIndexes(cameraIndexProbeCount);
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Failed to enumerate camera indexes");
			indexes = Array.Empty<int>();
		}

		lock (syncLock)
		{
			cachedIndexes = indexes;
			lastRefreshUtc = DateTimeOffset.UtcNow;
			isRefreshing = false;
		}

		onRefreshed();
	}
}
