using Microsoft.JSInterop;

namespace Lumeo.Services.Interop;

internal sealed class SwipeInterop
{
    private readonly Dictionary<string, Func<Task>> _drawerSwipeHandlers = new();
    private readonly Dictionary<string, Func<string, Task>> _carouselSwipeHandlers = new();
    private readonly Dictionary<string, Func<double, double, int, Task>> _carouselScrollHandlers = new();
    private readonly Dictionary<string, Func<string, Task>> _toastSwipeHandlers = new();
    private readonly Dictionary<string, Func<string, Task>> _horizontalSwipeHandlers = new();
    private readonly Dictionary<string, Func<string, Task>> _gallerySwipeHandlers = new();
    private readonly Dictionary<string, Func<string, Task>> _tabSwipeHandlers = new();

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
        Func<double, double, int, Task> scrollHandler)
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

    public async Task OnScrollPosition(string elementId, double scrollPos, double maxScroll, int nearestIndex = -1)
    {
        if (_carouselScrollHandlers.TryGetValue(elementId, out var handler))
        {
            await handler(scrollPos, maxScroll, nearestIndex);
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

    // --- Horizontal Swipe (Calendar month navigation) ---

    public async ValueTask RegisterHorizontalSwipe(
        IJSObjectReference module,
        DotNetObjectReference<ComponentInteropService> selfRef,
        string elementId,
        Func<string, Task> handler)
    {
        _horizontalSwipeHandlers[elementId] = handler;
        await module.InvokeVoidAsync("registerHorizontalSwipe", elementId, selfRef);
    }

    public async ValueTask UnregisterHorizontalSwipe(IJSObjectReference module, string elementId)
    {
        _horizontalSwipeHandlers.Remove(elementId);
        await module.InvokeVoidAsync("unregisterHorizontalSwipe", elementId);
    }

    public async Task OnCalendarSwipe(string elementId, string direction)
    {
        if (_horizontalSwipeHandlers.TryGetValue(elementId, out var handler))
        {
            await handler(direction);
        }
    }

    // --- Gallery Swipe (ImageGallery fullscreen prev/next, rc.52) ---

    public async ValueTask RegisterGallerySwipe(
        IJSObjectReference module,
        DotNetObjectReference<ComponentInteropService> selfRef,
        string elementId,
        Func<string, Task> handler)
    {
        _gallerySwipeHandlers[elementId] = handler;
        await module.InvokeVoidAsync("registerGallerySwipe", elementId, selfRef);
    }

    public async ValueTask UnregisterGallerySwipe(IJSObjectReference module, string elementId)
    {
        _gallerySwipeHandlers.Remove(elementId);
        try
        {
            await module.InvokeVoidAsync("unregisterGallerySwipe", elementId);
        }
        catch (JSDisconnectedException) { }
    }

    public async Task OnGallerySwipe(string elementId, string direction)
    {
        if (_gallerySwipeHandlers.TryGetValue(elementId, out var handler))
        {
            await handler(direction);
        }
    }

    // --- Tab Swipe ---

    public async ValueTask RegisterTabSwipe(
        IJSObjectReference module,
        DotNetObjectReference<ComponentInteropService> selfRef,
        string elementId,
        bool wrap,
        Func<string, Task> handler)
    {
        _tabSwipeHandlers[elementId] = handler;
        await module.InvokeVoidAsync("registerTabSwipe", elementId, wrap, selfRef);
    }

    public async ValueTask UnregisterTabSwipe(IJSObjectReference module, string elementId)
    {
        _tabSwipeHandlers.Remove(elementId);
        await module.InvokeVoidAsync("unregisterTabSwipe", elementId);
    }

    public async Task OnTabSwipe(string elementId, string direction)
    {
        if (_tabSwipeHandlers.TryGetValue(elementId, out var handler))
        {
            await handler(direction);
        }
    }

    public void Clear()
    {
        _drawerSwipeHandlers.Clear();
        _carouselSwipeHandlers.Clear();
        _carouselScrollHandlers.Clear();
        _toastSwipeHandlers.Clear();
        _horizontalSwipeHandlers.Clear();
        _gallerySwipeHandlers.Clear();
        _tabSwipeHandlers.Clear();
    }
}
