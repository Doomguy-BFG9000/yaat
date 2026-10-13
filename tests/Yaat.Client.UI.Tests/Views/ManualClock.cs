namespace Yaat.Client.UI.Tests.Views;

/// <summary>
/// A clock that moves only when the test advances it; each advance fires every live timer once. A test that shows a
/// strip label which overflows its line sets it as the line's marquee clock, so no frame timer outlives the test.
/// </summary>
internal sealed class ManualClock : TimeProvider
{
    private readonly List<ManualTimer> _timers = [];
    private long _ticks;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => _ticks;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(() => callback(state), _timers);
        _timers.Add(timer);
        return timer;
    }

    public void Advance(TimeSpan by)
    {
        _ticks += by.Ticks;
        foreach (ManualTimer timer in _timers.ToList())
        {
            timer.Fire();
        }
    }

    private sealed class ManualTimer(Action fire, List<ManualTimer> timers) : ITimer
    {
        public void Fire() => fire();

        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose() => timers.Remove(this);

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
