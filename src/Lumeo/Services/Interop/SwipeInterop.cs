using Microsoft.JSInterop;

namespace Lumeo.Services.Interop;

internal sealed class SwipeInterop
{
    private readonly Dictionary<string, Func<Task>> _drawerSwipeHandlers = new();
    private readonly Dictionary<string, Func<string, Task>> _carouselSwipeHandlers = new();
    private readonly Dictionary<string, Func<double, double, Task>> _carouselScrollHandlers = new();
    private readonly Dictionary<string, Func<string, Task>> _toastSwipeHandlers = new();

    // --- Drawer Swipe ---

    public async ValueTask RegisterDrawerSwipe(
        IJSObjectReference module,
        DotNetObjectReference<ComponentInteropService> selfRef,
        string elementId,
        string direction,
        Func<Task> handler)
    {
        _drawerSwipeHandlers[elementId] = handler;
        await module.InvokeVoidAsync("registerDrawerSwipe", elementId, direction, selfRef);
    }

    public async ValueTask RegisterDrawerSwipe(
        IJSObjectReference module,
        DotNetObjectReference<ComponentInteropService> selfRef,
        string elementId,
        Func<Task> handler)
    {
        _drawerSwipeHandlers[elementId] = handler;
        await module.InvokeVoidAsync("registerDrawerSwipe", elementId, "down", selfRef);
    }

    public async ValueTask UnregisterDrawerSwipe(IJSObjectReference module, string elementId)
    {
        _drawerSwipeHandlers.Remove(elementId);
        await module.InvokeVoidAsync("unregisterDrawerSwipe", elementId);
    }

    public async Task OnSwipeDismiss(string elementId)
    {
        if (_drawerSwipeHandlers.TryGetValue(elementId, out var handler))
        {
            await handler();
        }
    }

    // --- Carousel Swipe ---

    public async ValueTask RegisterCarouselSwipe(
        IJSObjectReference module,
        DotNetObjectReference<ComponentInteropService> selfRef,
        string elementId,
        string orientation,
        Func<string, Task> swipeHandler,
        Func<double, double, Task> scrollHandler)
    {
        _carouselSwipeHandlers[elementId] = swipeHandler;
        _carouselScrollHandlers[elementId] = scrollHandler;
        await module.InvokeVoidAsync("registerCarouselSwipe", elementId, orientation, selfRef);
    }

    public async ValueTask UnregisterCarouselSwipe(IJSObjectReference module, string elementId)
    {
        _carouselSwipeHandlers.Remove(elementId);
        _carouselScrollHandlers.Remove(elementId);
        await module.InvokeVoidAsync("unregisterCarouselSwipe", elementId);
    }

    public async ValueTask CarouselScrollTo(
        IJSObjectReference module,
        string elementId,
        int index,
        string behavior = "smooth")
    {
        await module.InvokeVoidAsync("carouselScrollTo", elementId, index, behavior);
    }

    public async Task OnSwipe(string elementId, string direction)
    {
        if (_carouselSwipeHandlers.TryGetValue(elementId, out var handler))
        {
            await handler(direction);
        }
    }

    public async Task OnScrollPosition(string elementId, double scrollPos, double maxScroll)
    {
        if (_carouselScrollHandlers.TryGetValue(elementId, out var handler))
        {
            await handler(scrollPos, maxScroll);
        }
    }

    // --- Toast Swipe ---

    public async ValueTask RegisterToastSwipe(
        IJSObjectReference module,
        DotNetObjectReference<ComponentInteropService> selfRef,
        string elementId,
        string toastId,
        Func<string, Task> handler)
    {
        _toastSwipeHandlers[toastId] = handler;
        await module.InvokeVoidAsync("registerToastSwipe", elementId, toastId, selfRef);
    }

    public async ValueTask UnregisterToastSwipe(IJSObjectReference module, string toastId, string elementId)
    {
        _toastSwipeHandlers.Remove(toastId);
        await module.InvokeVoidAsync("unregisterToastSwipe", elementId);
    }

    public async Task OnToastSwipeDismiss(string toastId)
    {
        if (_toastSwipeHandlers.TryGetValue(toastId, out var handler))
        {
            await handler(toastId);
        }
    }

    public void Clear()
    {
        _drawerSwipeHandlers.Clear();
        _carouselSwipeHandlers.Clear();
        _carouselScrollHandlers.Clear();
        _toastSwipeHandlers.Clear();
    }
}
