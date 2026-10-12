namespace SolarOfThings.Core.Diagnostics;

/// <summary>
/// Pure testable filter for an active foreground WPF DispatcherTimer heartbeat.
/// A delayed callback suggests scheduling latency but cannot isolate its cause.
/// Drops ordinary jitter and large gaps likely caused by OS sleep/suspension.
/// No permanent storage, sampling threads, SQL or sensitive information.
/// </summary>
public static class DispatcherTimingPolicy
{
    public static TimeSpan? ObserveLateness(TimeSpan elapsed, TimeSpan expected)
    {
        if (expected <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(expected));
        if (elapsed <= expected || elapsed > TimeSpan.FromSeconds(10))
            return null;

        var late = elapsed - expected;
        return late >= TimeSpan.FromMilliseconds(150) ? late : null;
    }
}
