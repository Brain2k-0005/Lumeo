using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Lumeo.Tests.Helpers;

/// <summary>
/// A <see cref="TimeProvider"/> whose clock only moves when a test calls <see cref="Advance"/>.
/// Timers created through it (including every <c>Task.Delay(TimeSpan, TimeProvider, …)</c>) fire
/// synchronously inside <see cref="Advance"/>, in due-time order, so a sequence of real-duration
/// delays becomes a deterministic sequence of steps: nothing elapses while the test thread is
/// descheduled on a starved CI runner, and nothing is late because a thread-pool timer callback
/// could not get a thread.
/// </summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly object _gate = new();
    private readonly List<ManualTimer> _timers = new();
    private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate) return _now;
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        lock (_gate) _timers.Add(timer);
        timer.Change(dueTime, period);
        return timer;
    }

    /// <summary>Number of timers currently armed (created, not disposed, with a due time).</summary>
    public int ArmedTimerCount
    {
        get { lock (_gate) return _timers.Count(t => t.DueAt is not null); }
    }

    /// <summary>
    /// Blocks until exactly <paramref name="count"/> timers are armed. The code under test arms its
    /// delays on the renderer's dispatcher, possibly a moment after the call that triggered them
    /// returned; this waits for that STATE, not for an amount of time. The ceiling is only a hang
    /// guard — it is never what makes the test pass.
    /// </summary>
    public void WaitForArmedTimers(int count)
    {
        if (!SpinWait.SpinUntil(() => ArmedTimerCount == count, TimeSpan.FromSeconds(30)))
        {
            throw new TimeoutException($"expected {count} armed timer(s), found {ArmedTimerCount}");
        }
    }

    /// <summary>Moves the clock forward, firing every timer that falls due on the way.</summary>
    public void Advance(TimeSpan by)
    {
        DateTimeOffset target;
        lock (_gate) target = _now + by;
        while (true)
        {
            ManualTimer? next;
            lock (_gate)
            {
                next = _timers.Where(t => t.DueAt is { } d && d <= target).OrderBy(t => t.DueAt).FirstOrDefault();
                if (next is null)
                {
                    _now = target;
                    return;
                }
                _now = next.DueAt!.Value;
                next.DueAt = next.Period > TimeSpan.Zero && next.Period != Timeout.InfiniteTimeSpan ? _now + next.Period : null;
            }
            next.Callback(next.State);
        }
    }

    private sealed class ManualTimer(ManualTimeProvider owner, TimerCallback callback, object? state) : ITimer
    {
        public TimerCallback Callback { get; } = callback;
        public object? State { get; } = state;
        public DateTimeOffset? DueAt { get; set; }
        public TimeSpan Period { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (owner._gate)
            {
                if (!owner._timers.Contains(this)) return false;
                Period = period;
                DueAt = dueTime == Timeout.InfiniteTimeSpan ? null : owner._now + dueTime;
                return true;
            }
        }

        public void Dispose()
        {
            lock (owner._gate) owner._timers.Remove(this);
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
