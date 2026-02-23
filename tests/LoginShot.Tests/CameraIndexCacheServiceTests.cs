using LoginShot.App;
using LoginShot.Capture;
using Microsoft.Extensions.Logging.Abstractions;

namespace LoginShot.Tests;

public class CameraIndexCacheServiceTests
{
	[Test]
	public void GetSnapshotAndRefreshIfNeeded_FirstCallStartsRefreshAndPublishesCachedIndexes()
	{
		using var refreshStarted = new ManualResetEventSlim(false);
		using var allowRefreshToFinish = new ManualResetEventSlim(false);
		using var callbackInvoked = new ManualResetEventSlim(false);

		var enumerator = new BlockingEnumerator(
			onEnumerateStarted: () => refreshStarted.Set(),
			beforeReturn: () => allowRefreshToFinish.Wait(TimeSpan.FromSeconds(2)),
			result: new[] { 0, 2, 5 });
		var service = new CameraIndexCacheService(enumerator, NullLogger.Instance, cameraIndexProbeCount: 10, refreshInterval: TimeSpan.FromMinutes(10));

		var initialSnapshot = service.GetSnapshotAndRefreshIfNeeded(() => callbackInvoked.Set());
		Assert.That(initialSnapshot.IsRefreshing, Is.True);

		Assert.That(refreshStarted.Wait(TimeSpan.FromSeconds(2)), Is.True, "Refresh did not start.");
		allowRefreshToFinish.Set();
		Assert.That(callbackInvoked.Wait(TimeSpan.FromSeconds(2)), Is.True, "Refresh callback was not invoked.");

		var refreshedSnapshot = service.GetSnapshotAndRefreshIfNeeded(() => { });
		Assert.Multiple(() =>
		{
			Assert.That(refreshedSnapshot.IsRefreshing, Is.False);
			Assert.That(refreshedSnapshot.Indexes, Is.EqualTo(new[] { 0, 2, 5 }));
			Assert.That(enumerator.CallCount, Is.EqualTo(1));
		});
	}

	[Test]
	public void GetSnapshotAndRefreshIfNeeded_WithinRefreshWindow_DoesNotReenumerate()
	{
		using var callbackInvoked = new ManualResetEventSlim(false);
		var enumerator = new BlockingEnumerator(
			onEnumerateStarted: static () => { },
			beforeReturn: static () => { },
			result: new[] { 1 });
		var service = new CameraIndexCacheService(enumerator, NullLogger.Instance, cameraIndexProbeCount: 10, refreshInterval: TimeSpan.FromHours(1));

		service.GetSnapshotAndRefreshIfNeeded(() => callbackInvoked.Set());
		Assert.That(callbackInvoked.Wait(TimeSpan.FromSeconds(2)), Is.True, "Initial refresh callback was not invoked.");

		var snapshot = service.GetSnapshotAndRefreshIfNeeded(() => { });

		Assert.Multiple(() =>
		{
			Assert.That(snapshot.IsRefreshing, Is.False);
			Assert.That(snapshot.Indexes, Is.EqualTo(new[] { 1 }));
			Assert.That(enumerator.CallCount, Is.EqualTo(1));
		});
	}

	[Test]
	public void GetSnapshotAndRefreshIfNeeded_WhenEnumeratorThrows_FallsBackToEmptyIndexes()
	{
		using var callbackInvoked = new ManualResetEventSlim(false);
		var enumerator = new ThrowingEnumerator();
		var service = new CameraIndexCacheService(enumerator, NullLogger.Instance, cameraIndexProbeCount: 10, refreshInterval: TimeSpan.FromMinutes(1));

		service.GetSnapshotAndRefreshIfNeeded(() => callbackInvoked.Set());
		Assert.That(callbackInvoked.Wait(TimeSpan.FromSeconds(2)), Is.True, "Refresh callback was not invoked.");

		var snapshot = service.GetSnapshotAndRefreshIfNeeded(() => { });
		Assert.Multiple(() =>
		{
			Assert.That(snapshot.IsRefreshing, Is.False);
			Assert.That(snapshot.Indexes, Is.Empty);
			Assert.That(enumerator.CallCount, Is.EqualTo(1));
		});
	}

	private sealed class BlockingEnumerator : ICameraDeviceEnumerator
	{
		private readonly Action onEnumerateStarted;
		private readonly Action beforeReturn;
		private readonly IReadOnlyList<int> result;

		public BlockingEnumerator(Action onEnumerateStarted, Action beforeReturn, IReadOnlyList<int> result)
		{
			this.onEnumerateStarted = onEnumerateStarted;
			this.beforeReturn = beforeReturn;
			this.result = result;
		}

		public int CallCount { get; private set; }

		public IReadOnlyList<int> EnumerateIndexes(int maxIndexExclusive = 10)
		{
			CallCount++;
			onEnumerateStarted();
			beforeReturn();
			return result;
		}
	}

	private sealed class ThrowingEnumerator : ICameraDeviceEnumerator
	{
		public int CallCount { get; private set; }

		public IReadOnlyList<int> EnumerateIndexes(int maxIndexExclusive = 10)
		{
			CallCount++;
			throw new InvalidOperationException("camera unavailable");
		}
	}
}
