namespace Lumeo.Internal;

/// <summary>
/// Decides, for a text control that keeps its own live value (Input, InputMask,
/// Textarea), whether an incoming controlled <c>Value</c> parameter is the parent's
/// verdict on what the user typed (adopt it) or merely an echo of a value this control
/// pushed through <c>ValueChanged</c> (keep the live value).
///
/// The hard case is a parent whose <c>ValueChanged</c> handler stores the value only
/// after an <c>await</c>. Its re-renders then lag the keystrokes, and they can arrive
/// out of order: two handlers whose awaits finish in the same timer tick run their
/// continuations in either order (measured on a real Blazor Server circuit at 15 ms/key
/// against <c>await Task.Delay(30)</c>: about 2% of keystroke pairs). An echo of an
/// older push that lands after the newer push's echo must not be adopted, or the newest
/// keystroke is overwritten in the DOM.
///
/// The rule: a pushed value stays a possible echo for exactly as long as the parent's
/// handler for it is still running. Blazor re-renders the parent from inside that
/// handler's task (ComponentBase.HandleEventAsync calls StateHasChanged before the task
/// completes), so every echo of a push arrives while the push is still pending. Once the
/// handler has finished, a Value equal to that old push is the parent's real state and is
/// adopted like any other change.
/// </summary>
internal sealed class ControlledValueEcho
{
    // A push whose handler returned an incomplete task. Entries whose task has completed
    // are pruned before every classification.
    private readonly List<(string? Value, Task Pending)> _pending = new();

    // Bounds a parent whose handler never completes; the oldest pending push then stops
    // being recognised as an echo.
    internal const int Cap = 64;

    private string? _dispatchValue;
    private bool _unchangedDuringDispatch;
    private bool _adoptedDuringDispatch;

    /// <summary>The value most recently pushed through ValueChanged (or adopted from the
    /// parent). A Value equal to it is always an echo.</summary>
    public string? LastPushed { get; private set; }

    /// <summary>True while the synchronous part of a ValueChanged dispatch runs, i.e. a
    /// parameter set arriving now was rendered from inside that InvokeAsync call.</summary>
    public bool Dispatching { get; private set; }

    public void Seed(string? value)
    {
        LastPushed = value;
        _pending.Clear();
    }

    /// <summary>Adopted a Value from the parent: nothing we pushed earlier can be told
    /// apart from the parent's state any more.</summary>
    public void Adopt(string? value)
    {
        LastPushed = value;
        _pending.Clear();
        _unchangedDuringDispatch = false;
        if (Dispatching) _adoptedDuringDispatch = true;
    }

    /// <summary>Records the value the control is about to push. Call it as soon as the
    /// live value changes (before OnInput), so any re-render from then on already sees
    /// this value as the control's own.</summary>
    public void Push(string? value)
    {
        LastPushed = value;
        _dispatchValue = value;
    }

    /// <summary>Call right before <c>ValueChanged.InvokeAsync</c>.</summary>
    public void BeginDispatch()
    {
        _unchangedDuringDispatch = false;
        _adoptedDuringDispatch = false;
        Dispatching = true;
    }

    /// <summary>
    /// Call right after <c>ValueChanged.InvokeAsync</c> returned <paramref name="task"/>
    /// (a faulted task when it threw synchronously).
    /// Returns true when the parent's synchronous reaction was to keep its previous Value
    /// (a rejection): the control must then roll its live value back to that Value.
    /// </summary>
    public bool EndDispatch(Task task)
    {
        Dispatching = false;
        var unchanged = _unchangedDuringDispatch;
        _unchangedDuringDispatch = false;
        var adopted = _adoptedDuringDispatch;
        _adoptedDuringDispatch = false;

        // The parent already answered this push with a value of its own (a transform);
        // there is no push of ours left for a later echo to refer to.
        if (adopted) return false;

        if (!task.IsCompleted)
        {
            _pending.Add((_dispatchValue, task));
            if (_pending.Count > Cap) _pending.RemoveAt(0);
            // An unchanged Value rendered while the handler is still awaiting is the
            // parent's pre-await re-render, not a verdict.
            return false;
        }

        // The handler finished synchronously. If it re-rendered us with its old Value
        // while we were dispatching, that was its verdict: reject.
        return unchanged;
    }

    /// <summary>
    /// Classifies a controlled parameter set. <paramref name="previousValue"/> is the Value
    /// the parent supplied on its previous parameter set. Returns true when the control
    /// must adopt <paramref name="value"/>; the tracker has then already been reset.
    /// </summary>
    public bool ShouldAdopt(string? value, string? previousValue)
    {
        if (string.Equals(value, LastPushed, StringComparison.Ordinal)) return false;

        if (Dispatching)
        {
            // Rendered from inside the ValueChanged call, after the handler's synchronous
            // part. A CHANGED Value is its synchronous verdict (a transform or a
            // normalization) and wins now. An UNCHANGED Value is either a synchronous
            // rejection or an async handler's re-render before its first await; which one
            // is only known once InvokeAsync returns, so EndDispatch decides.
            if (string.Equals(value, previousValue, StringComparison.Ordinal))
            {
                _unchangedDuringDispatch = true;
                return false;
            }
            Adopt(value);
            return true;
        }

        Prune();
        foreach (var p in _pending)
        {
            // An echo of a push whose handler is still running: stale, keep.
            if (string.Equals(p.Value, value, StringComparison.Ordinal)) return false;
        }

        // The parent re-rendered without changing its Value while one of our pushes is
        // still being handled: no verdict yet (typically the pre-await re-render when the
        // parent passes a parameter Blazor always treats as changed, e.g. a RenderFragment).
        if (_pending.Count > 0 && string.Equals(value, previousValue, StringComparison.Ordinal)) return false;

        Adopt(value);
        return true;
    }

    private void Prune() => _pending.RemoveAll(p => p.Pending.IsCompleted);

    /// <summary>
    /// Awaits an already-completed ValueChanged task (so a synchronous handler's exception
    /// propagates exactly as before) and observes a still-pending one in the background.
    /// Awaiting it would hold the input event open for the parent's whole async tail, and
    /// Blazor Server dispatches a circuit's UI events one at a time, so every later
    /// keystroke would queue behind it. An exception the async handler throws later is
    /// handed to <paramref name="dispatchException"/> (the component's
    /// <c>DispatchExceptionAsync</c>), so it reaches an ErrorBoundary or the circuit's error
    /// handling exactly as it would have through the event pipeline.
    /// </summary>
    public static Task AwaitOrObserve(Task task, Func<Exception, Task> dispatchException)
    {
        if (task.IsCompleted) return task;
        _ = ObserveAsync(task, dispatchException);
        return Task.CompletedTask;
    }

    private static async Task ObserveAsync(Task task, Func<Exception, Task> dispatchException)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
            // Blazor ignores a cancelled event-handler task too (ComponentBase and the
            // renderer both skip IsCanceled), so this matches the event pipeline.
        }
        catch (Microsoft.JSInterop.JSDisconnectedException)
        {
            // The circuit is already gone; there is nothing left to report to.
        }
        catch (Exception ex)
        {
            try
            {
                await dispatchException(ex);
            }
            catch (ObjectDisposedException)
            {
                // Renderer torn down between the throw and the dispatch.
            }
        }
    }
}
