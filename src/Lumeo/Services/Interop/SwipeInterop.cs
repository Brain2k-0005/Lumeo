using Microsoft.JSInterop;

namespace Lumeo.Services.Interop;

// NOTE (trim safety): JS-interop option bags in this file use Dictionary<string, object?>
// instead of anonymous types — under a trimmed publish the linker strips anonymous types'
// constructor parameter names and JSRuntime's serializer throws
// "ConstructorContainsNullParameterNames" at runtime (hit live by BlurFade; same class of
// bug here). The dictionary serializes to the identical JSON; the JS side is unchanged.
internal sealed class SwipeInterop
{
    private readonly Dictionary<string, Func<Task>> _drawerSwipeHandlers = new();
    private readonly Dictionary<string, Func<int, Task>> _drawerSnapHandlers = new();
    // Snap dismiss returns whether the close was honored, so JS can snap back on an OnBeforeClose veto.
    private readonly Dictionary<string, Func<Task<bool>>> _drawerSnapDismissHandlers = new();
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
        Func<Task> handler,
        int? activationPx = null,
        int? firePx = null,
        double? velocity = null)
    {
        _drawerSwipeHandlers[elementId] = handler;
        await module.InvokeVoidAsync("registerDrawerSwipe", elementId, direction, selfRef, new Dictionary<string, object?> { ["activationPx"] = activationPx, ["firePx"] = firePx, ["velocity"] = velocity });
    }

    // --- Drawer Snap Points (3.19) ---

    public async ValueTask RegisterDrawerSnap(
        IJSObjectReference module,
        DotNetObjectReference<ComponentInteropService> selfRef,
        string elementId,
        string direction,
        Func<Task<bool>> dismissHandler,
        Func<int, Task> snapHandler,
        IReadOnlyList<double> snapPoints,
        int activeIndex,
        bool dismissible = true,
        int? activationPx = null,
        int? firePx = null,
        double? velocity = null)
    {
        _drawerSnapDismissHandlers[elementId] = dismissHandler;
        _drawerSnapHandlers[elementId] = snapHandler;
        await module.InvokeVoidAsync("registerDrawerSnap", elementId, direction, selfRef,
            new Dictionary<string, object?> { ["snapPoints"] = snapPoints, ["activeIndex"] = activeIndex, ["dismissible"] = dismissible, ["activationPx"] = activationPx, ["firePx"] = firePx, ["velocity"] = velocity });
    }

    public async Task<bool> OnDrawerSnapDismiss(string elementId)
    {
        if (_drawerSnapDismissHandlers.TryGetValue(elementId, out var handler))
        {
            return await handler();
        }
        return true;
    }

    public async ValueTask SetDrawerSnap(IJSObjectReference module, string elementId, int index)
    {
        await module.InvokeVoidAsync("setDrawerSnap", elementId, index);
    }

    public async ValueTask UnregisterDrawerSnap(IJSObjectReference module, string elementId)
    {
        _drawerSnapDismissHandlers.Remove(elementId);
        _drawerSnapHandlers.Remove(elementId);
        await module.InvokeVoidAsync("unregisterDrawerSnap", elementId);
    }

    public async Task OnDrawerSnapChange(string elementId, int index)
    {
        if (_drawerSnapHandlers.TryGetValue(elementId, out var handler))
        {
            await handler(index);
        }
    }

    public async ValueTask RegisterDrawerSwipe(
        IJSObjectReference module,
        DotNetObjectReference<ComponentInteropService> selfRef,
        string elementId,
        Func<Task> handler,
        int? activationPx = null,
        int? firePx = null)
    {
        _drawerSwipeHandlers[elementId] = handler;
        await module.InvokeVoidAsync("registerDrawerSwipe", elementId, "down", selfRef, new Dictionary<string, object?> { ["activationPx"] = activationPx, ["firePx"] = firePx });
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
        Func<double, double, int, Task> scrollHandler,
        int? swipeThresholdPx = null,
        int? verticalDeadZonePx = null)
    {
        _carouselSwipeHandlers[elementId] = swipeHandler;
        _carouselScrollHandlers[elementId] = scrollHandler;
        await module.InvokeVoidAsync("registerCarouselSwipe", elementId, orientation, selfRef, new Dictionary<string, object?> { ["swipeThresholdPx"] = swipeThresholdPx, ["verticalDeadZonePx"] = verticalDeadZonePx });
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
        Func<string, Task> handler,
        int? swipeThresholdPx = null,
        int? verticalDeadZonePx = null)
    {
        _horizontalSwipeHandlers[elementId] = handler;
        await module.InvokeVoidAsync("registerHorizontalSwipe", elementId, selfRef, new Dictionary<string, object?> { ["swipeThresholdPx"] = swipeThresholdPx, ["verticalDeadZonePx"] = verticalDeadZonePx });
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
        Func<string, Task> handler,
        int? swipeThresholdPx = null,
        int? verticalDeadZonePx = null)
    {
        _gallerySwipeHandlers[elementId] = handler;
        await module.InvokeVoidAsync("registerGallerySwipe", elementId, selfRef, new Dictionary<string, object?> { ["swipeThresholdPx"] = swipeThresholdPx, ["verticalDeadZonePx"] = verticalDeadZonePx });
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
        Func<string, Task> handler,
        int? swipeThresholdPx = null,
        int? verticalDeadZonePx = null)
    {
        _tabSwipeHandlers[elementId] = handler;
        await module.InvokeVoidAsync("registerTabSwipe", elementId, wrap, selfRef, new Dictionary<string, object?> { ["swipeThresholdPx"] = swipeThresholdPx, ["verticalDeadZonePx"] = verticalDeadZonePx });
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
        _drawerSnapHandlers.Clear();
        _drawerSnapDismissHandlers.Clear();
        _carouselSwipeHandlers.Clear();
        _carouselScrollHandlers.Clear();
        _toastSwipeHandlers.Clear();
        _horizontalSwipeHandlers.Clear();
        _gallerySwipeHandlers.Clear();
        _tabSwipeHandlers.Clear();
    }
}
