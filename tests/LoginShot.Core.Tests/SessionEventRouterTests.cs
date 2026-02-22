using LoginShot.Triggers;
using Microsoft.Extensions.Logging.Abstractions;

namespace LoginShot.Core.Tests;

public class SessionEventRouterTests
{
    [Test]
    public async Task HandleEventAsync_DebouncesRepeatedUnlockByEventType()
    {
        var dispatcher = new FakeDispatcher();
        var clock = new FakeClock(new DateTimeOffset(2026, 2, 22, 0, 0, 0, TimeSpan.Zero));
        var router = CreateRouter(dispatcher, clock, debounceSeconds: 3, enableUnlock: true, enableLock: true);

        await router.HandleEventAsync(SessionEventType.Unlock);
        clock.AdvanceBy(TimeSpan.FromSeconds(1));
        await router.HandleEventAsync(SessionEventType.Unlock);
        clock.AdvanceBy(TimeSpan.FromSeconds(3));
        await router.HandleEventAsync(SessionEventType.Unlock);

        Assert.That(dispatcher.Events, Is.EqualTo(new[]
        {
            SessionEventType.Unlock,
            SessionEventType.Unlock
        }));
    }

    [Test]
    public async Task HandleEventAsync_DebouncesPerEventType_AllowsLockThenUnlockWithinWindow()
    {
        var dispatcher = new FakeDispatcher();
        var clock = new FakeClock(new DateTimeOffset(2026, 2, 22, 0, 0, 0, TimeSpan.Zero));
        var router = CreateRouter(dispatcher, clock, debounceSeconds: 3, enableUnlock: true, enableLock: true);

        await router.HandleEventAsync(SessionEventType.Lock);
        clock.AdvanceBy(TimeSpan.FromSeconds(1));
        await router.HandleEventAsync(SessionEventType.Unlock);

        Assert.That(dispatcher.Events, Is.EqualTo(new[]
        {
            SessionEventType.Lock,
            SessionEventType.Unlock
        }));
    }

    [Test]
    public async Task HandleEventAsync_DoesNotDispatchDisabledEvents()
    {
        var dispatcher = new FakeDispatcher();
        var clock = new FakeClock(new DateTimeOffset(2026, 2, 22, 0, 0, 0, TimeSpan.Zero));
        var router = CreateRouter(dispatcher, clock, debounceSeconds: 3, enableUnlock: false, enableLock: true);

        await router.HandleEventAsync(SessionEventType.Unlock);

        Assert.That(dispatcher.Events, Is.Empty);
    }

    [Test]
    public async Task HandleEventAsync_LockFailureIsBestEffortAndDoesNotStopSubsequentDispatches()
    {
        var dispatcher = new FakeDispatcher
        {
            ThrowOn = SessionEventType.Lock
        };
        var clock = new FakeClock(new DateTimeOffset(2026, 2, 22, 0, 0, 0, TimeSpan.Zero));
        var router = CreateRouter(dispatcher, clock, debounceSeconds: 3, enableUnlock: true, enableLock: true);

        await router.HandleEventAsync(SessionEventType.Lock);
        clock.AdvanceBy(TimeSpan.FromSeconds(1));
        await router.HandleEventAsync(SessionEventType.Unlock);

        Assert.That(dispatcher.Events, Is.EqualTo(new[]
        {
            SessionEventType.Lock,
            SessionEventType.Unlock
        }));
    }

    [Test]
    public async Task UpdateOptions_AppliesNewEnableFlags()
    {
        var dispatcher = new FakeDispatcher();
        var clock = new FakeClock(new DateTimeOffset(2026, 2, 22, 0, 0, 0, TimeSpan.Zero));
        var router = CreateRouter(dispatcher, clock, debounceSeconds: 3, enableUnlock: false, enableLock: false);

        await router.HandleEventAsync(SessionEventType.Unlock);
        router.UpdateOptions(new TriggerHandlingOptions(EnableUnlock: true, EnableLock: false, DebounceWindow: TimeSpan.FromSeconds(3)));
        await router.HandleEventAsync(SessionEventType.Unlock);

        Assert.That(dispatcher.Events, Is.EqualTo(new[] { SessionEventType.Unlock }));
    }

    private static SessionEventRouter CreateRouter(
        FakeDispatcher dispatcher,
        FakeClock clock,
        int debounceSeconds,
        bool enableUnlock,
        bool enableLock)
    {
        return new SessionEventRouter(
            dispatcher,
            new PerEventTypeDebouncer(),
            clock,
            NullLogger.Instance,
            new TriggerHandlingOptions(enableUnlock, enableLock, TimeSpan.FromSeconds(debounceSeconds)));
    }

    private sealed class FakeDispatcher : ITriggerDispatcher
    {
        public List<SessionEventType> Events { get; } = new();
        public SessionEventType? ThrowOn { get; init; }

        public Task DispatchAsync(SessionEventType eventType, CancellationToken cancellationToken = default)
        {
            Events.Add(eventType);
            if (ThrowOn == eventType)
            {
                throw new InvalidOperationException("Dispatch failed.");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeClock : IEventTimeProvider
    {
        public FakeClock(DateTimeOffset initialTime)
        {
            UtcNow = initialTime;
        }

        public DateTimeOffset UtcNow { get; private set; }

        public void AdvanceBy(TimeSpan delta)
        {
            UtcNow = UtcNow.Add(delta);
        }
    }
}
