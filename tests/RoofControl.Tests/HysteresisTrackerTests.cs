using Microsoft.Extensions.Logging.Abstractions;
using RoofControl.Decision;

namespace RoofControl.Tests;

public class HysteresisTrackerTests
{
    [Fact]
    public void Evaluate_ImmediatelyFalse_NoTrigger()
    {
        var tracker = new HysteresisTracker(
            NullLogger.Instance, "test", TimeSpan.FromSeconds(10));
        var now = new DateTime(2026, 6, 12, 12, 0, 0, DateTimeKind.Utc);

        Assert.False(tracker.Evaluate(false, now));
    }

    [Fact]
    public void Evaluate_WithinWindow_NoTrigger()
    {
        var tracker = new HysteresisTracker(
            NullLogger.Instance, "test", TimeSpan.FromSeconds(10));
        var now = new DateTime(2026, 6, 12, 12, 0, 0, DateTimeKind.Utc);

        // First tick: start the timer
        Assert.False(tracker.Evaluate(true, now));
        // 5 seconds later: still within window
        Assert.False(tracker.Evaluate(true, now.AddSeconds(5)));
    }

    [Fact]
    public void Evaluate_AfterWindow_Triggers()
    {
        var tracker = new HysteresisTracker(
            NullLogger.Instance, "test", TimeSpan.FromSeconds(10));
        var now = new DateTime(2026, 6, 12, 12, 0, 0, DateTimeKind.Utc);

        Assert.False(tracker.Evaluate(true, now));
        // After window: triggers
        Assert.True(tracker.Evaluate(true, now.AddSeconds(10)));
    }

    [Fact]
    public void Evaluate_StaysTrueAfterTrigger()
    {
        var tracker = new HysteresisTracker(
            NullLogger.Instance, "test", TimeSpan.FromSeconds(10));
        var now = new DateTime(2026, 6, 12, 12, 0, 0, DateTimeKind.Utc);

        Assert.False(tracker.Evaluate(true, now));
        Assert.True(tracker.Evaluate(true, now.AddSeconds(10)));
        Assert.True(tracker.Evaluate(true, now.AddSeconds(20)));
    }

    [Fact]
    public void Evaluate_ConditionClears_ResetsTimer()
    {
        var tracker = new HysteresisTracker(
            NullLogger.Instance, "test", TimeSpan.FromSeconds(10));
        var now = new DateTime(2026, 6, 12, 12, 0, 0, DateTimeKind.Utc);

        Assert.False(tracker.Evaluate(true, now));
        Assert.False(tracker.Evaluate(false, now.AddSeconds(5))); // cleared
        Assert.False(tracker.Evaluate(true, now.AddSeconds(6))); // restart
        Assert.False(tracker.Evaluate(true, now.AddSeconds(12))); // 6s later, still < 10 from restart
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var tracker = new HysteresisTracker(
            NullLogger.Instance, "test", TimeSpan.FromSeconds(10));
        var now = new DateTime(2026, 6, 12, 12, 0, 0, DateTimeKind.Utc);

        tracker.Evaluate(true, now);
        tracker.Reset();
        // After reset, should behave as fresh
        Assert.False(tracker.Evaluate(true, now));
    }

    [Fact]
    public void Evaluate_ZeroWindow_TriggersImmediately()
    {
        var tracker = new HysteresisTracker(
            NullLogger.Instance, "test", TimeSpan.Zero);
        var now = new DateTime(2026, 6, 12, 12, 0, 0, DateTimeKind.Utc);

        Assert.True(tracker.Evaluate(true, now));
    }
}
