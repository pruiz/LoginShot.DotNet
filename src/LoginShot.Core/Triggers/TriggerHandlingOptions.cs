namespace LoginShot.Triggers;

public sealed record TriggerHandlingOptions(
    bool EnableUnlock,
    bool EnableLock,
    TimeSpan DebounceWindow);
