using Microsoft.JSInterop;

namespace Lumeo.Services.Interop;

internal sealed class ResizeInterop
{
    private readonly Dictionary<string, Func<double, Task>> _resizeHandlers = new();
    private readonly Dictionary<string, Func<Task>> _resizeEndHandlers = new();
    private readonly Dictionary<string, Func<double, Task>> _columnResizeHandlers = new();
    private readonly Dictionary<string, Func<Task>> _columnResizeEndHandlers = new();
    private readonly Dictionary<string, Func<double, bool, Task>> _columnResizeCommitHandlers = new();

    // --- Panel Resize ---

    public async ValueTask RegisterResizeHandle(
        IJSObjectReference module,
        DotNetObjectReference<ComponentInteropService> selfRef,
        string elementId,
        string direction,
        Func<double, Task> resizeHandler,
        Func<Task> resizeEndHandler)
    {
        _resizeHandlers[elementId] = resizeHandler;
        _resizeEndHandlers[elementId] = resizeEndHandler;
        await module.InvokeVoidAsync("registerResizeHandle", elementId, direction, selfRef);
    }

    public async ValueTask UnregisterResizeHandle(IJSObjectReference module, string elementId)
    {
        _resizeHandlers.Remove(elementId);
        _resizeEndHandlers.Remove(elementId);
        await module.InvokeVoidAsync("unregisterResizeHandle", elementId);
    }

    public async Task OnResize(string elementId, double delta)
    {
        if (_resizeHandlers.TryGetValue(elementId, out var handler))
        {
            await handler(delta);
        }
    }

    public async Task OnResizeEnd(string elementId)
    {
        if (_resizeEndHandlers.TryGetValue(elementId, out var handler))
        {
            await handler();
        }
    }

    // --- Column Resize ---

    /// <summary>Registers a column resize handle. JS handles the drag entirely in the DOM
    /// (preview by directly writing styles) and invokes <see cref="OnColumnResizeCommit"/>
    /// once on mouseup — so we do ONE Blazor re-render per resize instead of one per pixel.</summary>
    public async ValueTask RegisterColumnResize(
        IJSObjectReference module,
        DotNetObjectReference<ComponentInteropService> selfRef,
        string handleId,
        double minWidth,
        double? maxWidth,
        Func<double, bool, Task> commitHandler)
    {
        _columnResizeCommitHandlers[handleId] = commitHandler;
        await module.InvokeVoidAsync("registerColumnResize", handleId, selfRef, minWidth, maxWidth ?? 0);
    }

    public async ValueTask UnregisterColumnResize(IJSObjectReference module, string handleId)
    {
        _columnResizeHandlers.Remove(handleId);
        _columnResizeEndHandlers.Remove(handleId);
        _columnResizeCommitHandlers.Remove(handleId);
        await module.InvokeVoidAsync("unregisterColumnResize", handleId);
    }

    /// <summary>Keyboard resize: nudges the column width by <paramref name="delta"/>
    /// px in JS (which clamps to the registered min/max and re-commits through the
    /// same commit handler), so a keyboard resize persists identically to a drag.</summary>
    public async ValueTask NudgeColumnResize(IJSObjectReference module, string handleId, double delta)
    {
        await module.InvokeVoidAsync("nudgeColumnResize", handleId, delta);
    }

    public async Task OnColumnResize(string handleId, double delta)
    {
        if (_columnResizeHandlers.TryGetValue(handleId, out var handler))
        {
            await handler(delta);
        }
    }

    public async Task OnColumnResizeEnd(string handleId)
    {
        if (_columnResizeEndHandlers.TryGetValue(handleId, out var handler))
        {
            await handler();
        }
    }

    public async Task OnColumnResizeCommit(string handleId, double finalWidth, bool autoFit)
    {
        if (_columnResizeCommitHandlers.TryGetValue(handleId, out var handler))
        {
            await handler(finalWidth, autoFit);
        }
    }

    public void Clear()
    {
        _resizeHandlers.Clear();
        _resizeEndHandlers.Clear();
        _columnResizeHandlers.Clear();
        _columnResizeEndHandlers.Clear();
        _columnResizeCommitHandlers.Clear();
    }
}
