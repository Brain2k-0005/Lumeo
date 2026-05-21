using Lumeo.Services;

namespace Lumeo.Internal;

/// <summary>
/// Schedules a callback after a quiet period. Each call to <see cref="Set"/>
/// resets the timer so only the final value's callback fires. Backed by
/// <see cref="DelayedDispatch"/> so timers run on the Blazor sync context.
/// Dispose to cancel any pending invocation.
/// </summary>
internal sealed class DebouncedValue<T> : IDisposable
{
    private readonly int _delayMs;
    private readonly object _lock = new();
    private System.Threading.Timer? _timer;
    private T? _pending;
    private bool _hasPending;
    private Func<T, Task>? _callback;

    public DebouncedValue(int delayMs)
    {
        _delayMs = Math.Max(0, delayMs);
    }

    public void OnChanged(Func<T, Task> callback) => _callback = callback;

    public void Set(T value)
    {
        lock (_lock)
        {
            _pending = value;
            _hasPending = true;
            _timer?.Dispose();
            _timer = new System.Threading.Timer(_ => Fire(), null, _delayMs, System.Threading.Timeout.Infinite);
        }
    }

    public void Cancel()
    {
        lock (_lock)
        {
            _timer?.Dispose();
            _timer = null;
            _hasPending = false;
        }
    }

    private void Fire()
    {
        T? value;
        Func<T, Task>? cb;
        lock (_lock)
        {
            if (!_hasPending) return;
            value = _pending;
            cb = _callback;
            _hasPending = false;
        }
        if (cb is not null && value is not null)
        {
            _ = cb(value);
        }
        else if (cb is not null)
        {
            _ = cb(default!);
        }
    }

    public void Dispose() => Cancel();
}
