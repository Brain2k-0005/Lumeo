using Microsoft.JSInterop;

namespace Lumeo.Services;

public sealed class ComponentInteropService : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private IJSObjectReference? _module;
    private DotNetObjectReference<ComponentInteropService>? _selfRef;
    private readonly Dictionary<string, Func<Task>> _clickOutsideHandlers = new();
    private readonly Dictionary<string, Func<Task>> _drawerSwipeHandlers = new();
    private readonly Dictionary<string, Func<string, Task>> _carouselSwipeHandlers = new();
    private readonly Dictionary<string, Func<double, double, Task>> _carouselScrollHandlers = new();
    private readonly Dictionary<string, Func<double, Task>> _resizeHandlers = new();
    private readonly Dictionary<string, Func<Task>> _resizeEndHandlers = new();

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

    // --- Floating Position ---

    public async ValueTask PositionFixed(string contentId, string referenceId, string align = "start", bool matchWidth = false)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("positionFixed", contentId, referenceId, align, matchWidth);
    }

    // --- Element Rect ---

    public async ValueTask<ElementRect?> GetElementRect(string elementId)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<ElementRect?>("getElementRect", elementId);
    }

    public async ValueTask<double> GetElementDimension(string elementId, string dimension)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<double>("getElementDimension", elementId, dimension);
    }

    // --- Drawer Swipe ---

    public async ValueTask RegisterDrawerSwipe(string elementId, Func<Task> handler)
    {
        _drawerSwipeHandlers[elementId] = handler;
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("registerDrawerSwipe", elementId, GetSelfRef());
    }

    public async ValueTask UnregisterDrawerSwipe(string elementId)
    {
        _drawerSwipeHandlers.Remove(elementId);
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("unregisterDrawerSwipe", elementId);
    }

    [JSInvokable]
    public async Task OnSwipeDismiss()
    {
        foreach (var handler in _drawerSwipeHandlers.Values)
        {
            await handler();
        }
    }

    // --- Carousel Swipe ---

    public async ValueTask RegisterCarouselSwipe(string elementId, string orientation, Func<string, Task> swipeHandler, Func<double, double, Task> scrollHandler)
    {
        _carouselSwipeHandlers[elementId] = swipeHandler;
        _carouselScrollHandlers[elementId] = scrollHandler;
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("registerCarouselSwipe", elementId, orientation, GetSelfRef());
    }

    public async ValueTask UnregisterCarouselSwipe(string elementId)
    {
        _carouselSwipeHandlers.Remove(elementId);
        _carouselScrollHandlers.Remove(elementId);
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("unregisterCarouselSwipe", elementId);
    }

    public async ValueTask CarouselScrollTo(string elementId, int index, string behavior = "smooth")
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("carouselScrollTo", elementId, index, behavior);
    }

    [JSInvokable]
    public async Task OnSwipe(string direction)
    {
        foreach (var handler in _carouselSwipeHandlers.Values)
        {
            await handler(direction);
        }
    }

    [JSInvokable]
    public async Task OnScrollPosition(double scrollPos, double maxScroll)
    {
        foreach (var handler in _carouselScrollHandlers.Values)
        {
            await handler(scrollPos, maxScroll);
        }
    }

    // --- Resizable Handle ---

    public async ValueTask RegisterResizeHandle(string elementId, string direction, Func<double, Task> resizeHandler, Func<Task> resizeEndHandler)
    {
        _resizeHandlers[elementId] = resizeHandler;
        _resizeEndHandlers[elementId] = resizeEndHandler;
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("registerResizeHandle", elementId, direction, GetSelfRef());
    }

    public async ValueTask UnregisterResizeHandle(string elementId)
    {
        _resizeHandlers.Remove(elementId);
        _resizeEndHandlers.Remove(elementId);
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("unregisterResizeHandle", elementId);
    }

    [JSInvokable]
    public async Task OnResize(double delta)
    {
        foreach (var handler in _resizeHandlers.Values)
        {
            await handler(delta);
        }
    }

    [JSInvokable]
    public async Task OnResizeEnd()
    {
        foreach (var handler in _resizeEndHandlers.Values)
        {
            await handler();
        }
    }

    public record ElementRect(double X, double Y, double Width, double Height);

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
        _drawerSwipeHandlers.Clear();
        _carouselSwipeHandlers.Clear();
        _carouselScrollHandlers.Clear();
        _resizeHandlers.Clear();
        _resizeEndHandlers.Clear();

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
