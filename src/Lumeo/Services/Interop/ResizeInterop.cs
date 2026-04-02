using Microsoft.JSInterop;

namespace Lumeo.Services.Interop;

internal sealed class ResizeInterop
{
    private readonly Dictionary<string, Func<double, Task>> _resizeHandlers = new();
    private readonly Dictionary<string, Func<Task>> _resizeEndHandlers = new();
    private readonly Dictionary<string, Func<double, Task>> _columnResizeHandlers = new();
    private readonly Dictionary<string, Func<Task>> _columnResizeEndHandlers = new();

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

    public async ValueTask RegisterColumnResize(
        IJSObjectReference module,
        DotNetObjectReference<ComponentInteropService> selfRef,
        string handleId,
        Func<double, Task> resizeHandler,
        Func<Task> resizeEndHandler)
    {
        _columnResizeHandlers[handleId] = resizeHandler;
        _columnResizeEndHandlers[handleId] = resizeEndHandler;
        await module.InvokeVoidAsync("registerColumnResize", handleId, selfRef);
    }

    public async ValueTask UnregisterColumnResize(IJSObjectReference module, string handleId)
    {
        _columnResizeHandlers.Remove(handleId);
        _columnResizeEndHandlers.Remove(handleId);
        await module.InvokeVoidAsync("unregisterColumnResize", handleId);
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

    public void Clear()
    {
        _resizeHandlers.Clear();
        _resizeEndHandlers.Clear();
        _columnResizeHandlers.Clear();
        _columnResizeEndHandlers.Clear();
    }
}
