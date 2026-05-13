using Microsoft.JSInterop;

namespace Lumeo.Services.Interop;

/// <summary>
/// Touch-based reordering for SortableList. The HTML5 Drag-and-Drop API
/// does not fire on touch devices, so we route a parallel touchstart →
/// touchmove → touchend path through this adapter. The JS side computes
/// source / target indices via <c>document.elementFromPoint</c> and calls
/// back into <see cref="ComponentInteropService.OnSortableTouchDrop"/>,
/// which dispatches here.
/// </summary>
internal sealed class SortableInterop
{
    private readonly Dictionary<string, Func<int, int, Task>> _handlers = new();

    public async ValueTask RegisterSortableTouch(
        IJSObjectReference module,
        DotNetObjectReference<ComponentInteropService> selfRef,
        string containerId,
        Func<int, int, Task> handler)
    {
        _handlers[containerId] = handler;
        await module.InvokeVoidAsync("registerSortableTouch", containerId, selfRef);
    }

    public async ValueTask UnregisterSortableTouch(IJSObjectReference module, string containerId)
    {
        _handlers.Remove(containerId);
        await module.InvokeVoidAsync("unregisterSortableTouch", containerId);
    }

    public async Task OnSortableTouchDrop(string containerId, int source, int target)
    {
        if (_handlers.TryGetValue(containerId, out var handler))
        {
            await handler(source, target);
        }
    }

    public void Clear() => _handlers.Clear();
}
