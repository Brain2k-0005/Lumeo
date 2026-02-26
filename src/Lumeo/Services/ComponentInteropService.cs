using Microsoft.JSInterop;

namespace Lumeo.Services;

public sealed class ComponentInteropService : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private IJSObjectReference? _module;
    private DotNetObjectReference<ComponentInteropService>? _selfRef;
    private readonly Dictionary<string, Func<Task>> _clickOutsideHandlers = new();

    public ComponentInteropService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    private async Task<IJSObjectReference> GetModuleAsync()
    {
        _module ??= await _jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/Lumeo/js/components.js");
        return _module;
    }

    private DotNetObjectReference<ComponentInteropService> GetSelfRef()
    {
        _selfRef ??= DotNetObjectReference.Create(this);
        return _selfRef;
    }

    public async ValueTask RegisterClickOutside(string elementId, string? triggerElementId, Func<Task> handler)
    {
        _clickOutsideHandlers[elementId] = handler;
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("registerClickOutside", elementId, triggerElementId, GetSelfRef());
    }

    public async ValueTask UnregisterClickOutside(string elementId)
    {
        _clickOutsideHandlers.Remove(elementId);
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("unregisterClickOutside", elementId);
    }

    [JSInvokable]
    public async Task OnClickOutside(string elementId)
    {
        if (_clickOutsideHandlers.TryGetValue(elementId, out var handler))
        {
            await handler();
        }
    }

    public async ValueTask FocusElement(string elementId)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("focusElementById", elementId);
    }

    public async ValueTask LockScroll()
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("lockScroll");
    }

    public async ValueTask UnlockScroll()
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("unlockScroll");
    }

    public async ValueTask SetupFocusTrap(string elementId)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("setupFocusTrap", elementId);
    }

    public async ValueTask RemoveFocusTrap(string elementId)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("removeFocusTrap", elementId);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var id in _clickOutsideHandlers.Keys.ToList())
        {
            try
            {
                if (_module is not null)
                {
                    await _module.InvokeVoidAsync("unregisterClickOutside", id);
                }
            }
            catch (JSDisconnectedException)
            {
                // Circuit disconnected, safe to ignore
            }
        }

        _clickOutsideHandlers.Clear();

        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // Circuit disconnected, safe to ignore
            }
        }

        _selfRef?.Dispose();
    }
}
