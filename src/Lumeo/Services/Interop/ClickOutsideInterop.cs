using Microsoft.JSInterop;

namespace Lumeo.Services.Interop;

internal sealed class ClickOutsideInterop
{
    private readonly Dictionary<string, Func<Task>> _handlers = new();

    public IEnumerable<string> RegisteredIds => _handlers.Keys;

    public async ValueTask Register(
        IJSObjectReference module,
        DotNetObjectReference<ComponentInteropService> selfRef,
        string elementId,
        string? triggerElementId,
        Func<Task> handler)
    {
        _handlers[elementId] = handler;
        await module.InvokeVoidAsync("registerClickOutside", elementId, triggerElementId, selfRef);
    }

    public async ValueTask Unregister(IJSObjectReference module, string elementId)
    {
        _handlers.Remove(elementId);
        await module.InvokeVoidAsync("unregisterClickOutside", elementId);
    }

    public async Task OnCallback(string elementId)
    {
        if (_handlers.TryGetValue(elementId, out var handler))
        {
            await handler();
        }
    }

    public void Clear() => _handlers.Clear();
}
