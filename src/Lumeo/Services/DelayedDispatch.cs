using System;
using System.Threading;
using System.Threading.Tasks;

namespace Lumeo.Services;

/// <summary>
/// Provides a consistent pattern for delayed, debounced, or deferred
/// actions dispatched through Blazor's synchronization context.
/// </summary>
internal sealed class DelayedDispatch : IDisposable
{
    private Timer? _timer;
    private int _sequence;

    /// <summary>
    /// Schedule an action to run after a delay. Cancels any pending action.
    /// The action runs on the caller's context (use InvokeAsync inside).
    /// </summary>
    internal void Schedule(int delayMs, Func<Task> action)
    {
        _timer?.Dispose();
        var seq = ++_sequence;
        _timer = new Timer(_ =>
        {
            if (seq != _sequence) return;
            _ = RunSafe(action);
        }, null, delayMs, Timeout.Infinite);
    }

    /// <summary>
    /// Schedule a synchronous action to run after a delay.
    /// </summary>
    internal void Schedule(int delayMs, Action action)
    {
        _timer?.Dispose();
        var seq = ++_sequence;
        _timer = new Timer(_ =>
        {
            if (seq != _sequence) return;
            action();
        }, null, delayMs, Timeout.Infinite);
    }

    /// <summary>
    /// Cancel any pending scheduled action.
    /// </summary>
    internal void Cancel()
    {
        ++_sequence;
        _timer?.Dispose();
        _timer = null;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }

    private static async Task RunSafe(Func<Task> action)
    {
        try { await action(); }
        catch (ObjectDisposedException) { }
    }
}
