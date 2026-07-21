using System.Reflection;
using Lumeo.Services.Interop;
using Microsoft.JSInterop;

namespace Lumeo.Services;

public sealed class ComponentInteropService : IComponentInteropService
{
    private readonly IJSRuntime _jsRuntime;
    private IJSObjectReference? _module;
    private IJSObjectReference? _motionModule;
    private IJSObjectReference? _toolbarModule;
    private IJSObjectReference? _signaturePadModule;
    private DotNetObjectReference<ComponentInteropService>? _selfRef;

    // Adapters
    private readonly ClickOutsideInterop _clickOutside = new();
    private readonly FloatingPositionInterop _floatingPosition = new();
    private readonly FocusInterop _focus = new();
    private readonly SwipeInterop _swipe = new();
    private readonly ResizeInterop _resize = new();
    private readonly ReorderInterop _reorder = new();
    private readonly ScrollInterop _scroll = new();
    private readonly UtilityInterop _utility = new();
    private readonly RichTextInterop _richText = new();
    private readonly SortableInterop _sortable = new();

    public ComponentInteropService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    // Trim safety: the deserializer constructs ViewportSize purely via reflection, which the
    // linker cannot see — without this the parameterless ctor/property setters get
    // trimmed and JSRuntime throws ConstructorContainsNullParameterNames at runtime.
    [System.Diagnostics.CodeAnalysis.DynamicDependency(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties, typeof(ViewportSize))]
    private async Task<IJSObjectReference> GetModuleAsync()
    {
        _module ??= await _jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", $"./_content/Lumeo/js/components.js?v={_jsModuleVersion}");
        return _module;
    }

    // Cache-buster query string appended to the components.js import URL so a
    // version bump invalidates stale browser caches automatically. Static
    // assets under _content/<Lib>/ are served WITHOUT content-hashes (unlike
    // the Blazor framework JS), so without this query string a user who hit
    // an older version would get the cached file and any new exports (e.g.
    // registerViewportListener added in 2.1.3) would resolve to "not a
    // function" at runtime. Derived once from the assembly's
    // AssemblyInformationalVersion (driven by Directory.Build.props
    // <Version>), so it tracks every release without manual maintenance.
    private static readonly string _jsModuleVersion =
        typeof(ComponentInteropService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
        ?? typeof(ComponentInteropService).Assembly.GetName().Version?.ToString()
        ?? "0";

    // --- Motion module (Lumeo.Motion satellite) ---
    // Loaded lazily on first use; apps that don't install Lumeo.Motion never pay
    // the import cost. If Lumeo.Motion is not referenced, the JS 404 will surface
    // as a clear JS exception pointing at _content/Lumeo.Motion/js/motion.js.

    private async Task<IJSObjectReference> GetMotionModuleAsync()
    {
        _motionModule ??= await _jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/Lumeo.Motion/js/motion.js");
        return _motionModule;
    }

    private DotNetObjectReference<ComponentInteropService> GetSelfRef()
    {
        _selfRef ??= DotNetObjectReference.Create(this);
        return _selfRef;
    }

    // --- Click Outside ---

    public async ValueTask RegisterClickOutside(string elementId, string? triggerElementId, Func<Task> handler)
    {
        var module = await GetModuleAsync();
        await _clickOutside.Register(module, GetSelfRef(), elementId, triggerElementId, handler);
    }

    public async ValueTask UnregisterClickOutside(string elementId)
    {
        var module = await GetModuleAsync();
        await _clickOutside.Unregister(module, elementId);
    }

    [JSInvokable]
    public async Task OnClickOutside(string elementId) => await _clickOutside.OnCallback(elementId);

    // --- Focus / Scroll Lock ---

    public async ValueTask FocusElement(string elementId)
    {
        var module = await GetModuleAsync();
        await _focus.FocusElement(module, elementId);
    }

    public async ValueTask FocusMenuItemByIndex(string containerId, int index)
    {
        var module = await GetModuleAsync();
        await _focus.FocusMenuItemByIndex(module, containerId, index);
    }

    public async ValueTask<int> GetMenuItemCount(string containerId)
    {
        var module = await GetModuleAsync();
        return await _focus.GetMenuItemCount(module, containerId);
    }

    public async ValueTask<string[]> GetOrderedDescendantIds(string containerId, string selector)
    {
        var module = await GetModuleAsync();
        return await _focus.GetOrderedDescendantIds(module, containerId, selector);
    }

    public async ValueTask<int> FocusMenuItemByTypeahead(string containerId, string query, int currentIndex)
    {
        var module = await GetModuleAsync();
        return await _focus.FocusMenuItemByTypeahead(module, containerId, query, currentIndex);
    }

    public async ValueTask SaveFocus(string key)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("saveFocus", key);
    }

    public async ValueTask RestoreFocus(string key)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("restoreFocus", key);
    }

    public async ValueTask LockScroll()
    {
        var module = await GetModuleAsync();
        await _focus.LockScroll(module);
    }

    public async ValueTask AttachOverlaySlideEnd(string elementId)
    {
        var module = await GetModuleAsync();
        try
        {
            await module.InvokeVoidAsync("attachOverlaySlideEnd", elementId);
        }
        catch (Microsoft.JSInterop.JSDisconnectedException) { }
    }

    public async ValueTask AttachOverlayExitEnd<[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicMethods)] T>(string elementId, DotNetObjectReference<T> dotNetRef) where T : class
    {
        var module = await GetModuleAsync();
        try
        {
            await module.InvokeVoidAsync("attachOverlayExitEnd", elementId, dotNetRef);
        }
        catch (Microsoft.JSInterop.JSDisconnectedException) { }
    }

    public async ValueTask UnlockScroll()
    {
        var module = await GetModuleAsync();
        await _focus.UnlockScroll(module);
    }

    public async ValueTask SetHtmlClass(string className, bool active)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("setHtmlClass", className, active);
    }

    public async ValueTask SetupFocusTrap(string elementId, string? initialFocusSelector = null)
    {
        var module = await GetModuleAsync();
        await _focus.SetupFocusTrap(module, elementId, initialFocusSelector);
    }

    public async ValueTask RemoveFocusTrap(string elementId)
    {
        var module = await GetModuleAsync();
        await _focus.RemoveFocusTrap(module, elementId);
    }

    // --- ColorPicker SV Drag ---

    private readonly Dictionary<string, Func<double, double, Task>> _svDragHandlers = new();

    public async ValueTask RegisterSvDrag(string elementId, Func<double, double, Task> handler)
    {
        var module = await GetModuleAsync();
        _svDragHandlers[elementId] = handler;
        await module.InvokeVoidAsync("registerSvDrag", elementId, GetSelfRef());
    }

    public async ValueTask UnregisterSvDrag(string elementId)
    {
        _svDragHandlers.Remove(elementId);
        try
        {
            var module = await GetModuleAsync();
            await module.InvokeVoidAsync("unregisterSvDrag", elementId);
        }
        catch (JSDisconnectedException) { }
    }

    [JSInvokable]
    public Task OnSvDrag(string elementId, double s, double v)
    {
        if (_svDragHandlers.TryGetValue(elementId, out var handler))
            return handler(s, v);
        return Task.CompletedTask;
    }

    // --- Pinch Zoom (multi-touch gesture) ---
    //
    // Generic two-finger pinch detector. The JS helper calls back with a per-
    // event scale delta (current distance / previous distance), and the C#
    // consumer multiplies that into its own accumulated zoom. We keep a small
    // per-element handler dictionary here so multiple registered components
    // can share the singleton service without colliding.

    private readonly Dictionary<string, Func<double, Task>> _pinchZoomHandlers = new();

    public async ValueTask RegisterPinchZoom(string elementId, Func<double, Task> handler)
    {
        var module = await GetModuleAsync();
        _pinchZoomHandlers[elementId] = handler;
        await module.InvokeVoidAsync("registerPinchZoom", elementId, GetSelfRef(), "OnPinchZoom");
    }

    public async ValueTask UnregisterPinchZoom(string elementId)
    {
        _pinchZoomHandlers.Remove(elementId);
        try
        {
            var module = await GetModuleAsync();
            await module.InvokeVoidAsync("unregisterPinchZoom", elementId);
        }
        catch (JSDisconnectedException) { }
    }

    [JSInvokable]
    public Task OnPinchZoom(string elementId, double scaleDelta)
    {
        if (_pinchZoomHandlers.TryGetValue(elementId, out var handler))
            return handler(scaleDelta);
        return Task.CompletedTask;
    }

    // --- Viewport Size ---

    // Trim safety: the deserializer constructs ViewportSize purely via reflection, which the
    // linker cannot see — without this the parameterless ctor/property setters get
    // trimmed and JSRuntime throws ConstructorContainsNullParameterNames at runtime.
    [System.Diagnostics.CodeAnalysis.DynamicDependency(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties, typeof(ViewportSize))]
    public async ValueTask<ViewportSize> GetViewportSize()
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<ViewportSize>("getViewportSize");
    }

    // --- Viewport Listener (2.1.3 — backs IResponsiveService) ---

    public async ValueTask<ViewportSize?> RegisterViewportListener(DotNetObjectReference<ResponsiveService> dotnetRef)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<ViewportSize?>("registerViewportListener", dotnetRef);
    }

    public async ValueTask UnregisterViewportListener()
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("unregisterViewportListener");
    }

    // --- Floating Position ---

    public ValueTask<string> PositionFixed(string contentId, string referenceId, string align = "start", bool matchWidth = false, string side = "bottom")
        => PositionFixed(contentId, referenceId, align, matchWidth, side, 4);

    public async ValueTask<string> PositionFixed(string contentId, string referenceId, string align, bool matchWidth, string side, int offset)
    {
        var module = await GetModuleAsync();
        return await _floatingPosition.PositionFixed(module, contentId, referenceId, align, matchWidth, side, offset);
    }

    public async ValueTask<string> PositionFixed(string contentId, string referenceId, string align, bool matchWidth, string side, int offset, Func<string, Task>? onSideChanged)
    {
        var module = await GetModuleAsync();
        return await _floatingPosition.PositionFixed(module, GetSelfRef(), contentId, referenceId, align, matchWidth, side, offset, onSideChanged);
    }

    [JSInvokable]
    public async Task OnPositionSideChanged(string contentId, string side) => await _floatingPosition.OnSideChanged(contentId, side);

    public async ValueTask UnpositionFixed(string contentId)
    {
        var module = await GetModuleAsync();
        await _floatingPosition.UnpositionFixed(module, contentId);
    }

    public async ValueTask PositionAtPoint(string contentId, double x, double y)
    {
        var module = await GetModuleAsync();
        await _floatingPosition.PositionAtPoint(module, contentId, x, y);
    }

    // --- Toolbar roving focus (lives in components.js, always loaded) ---

    public async ValueTask InitToolbarRoving(string toolbarId)
    {
        try
        {
            var module = await GetModuleAsync();
            await module.InvokeVoidAsync("initToolbarRoving", toolbarId);
        }
        catch (JSDisconnectedException) { }
    }

    public async ValueTask MoveToolbarFocus(string toolbarId, int delta)
    {
        try
        {
            var module = await GetModuleAsync();
            await module.InvokeVoidAsync("moveToolbarFocus", toolbarId, delta);
        }
        catch (JSDisconnectedException) { }
    }

    public async ValueTask FocusToolbarEdge(string toolbarId, bool last)
    {
        try
        {
            var module = await GetModuleAsync();
            await module.InvokeVoidAsync("focusToolbarEdge", toolbarId, last);
        }
        catch (JSDisconnectedException) { }
    }

    // --- Element Rect ---

    public async ValueTask<ElementRect?> GetElementRect(string elementId)
    {
        var module = await GetModuleAsync();
        return await _floatingPosition.GetElementRect(module, elementId);
    }

    public async ValueTask<double> GetElementDimension(string elementId, string dimension)
    {
        var module = await GetModuleAsync();
        return await _floatingPosition.GetElementDimension(module, elementId, dimension);
    }

    // --- ScrollTop lookup (used by PullToRefresh to gate pointer-down on scrollTop==0) ---

    public async ValueTask<double> GetScrollTop(string elementId)
    {
        try
        {
            var module = await GetModuleAsync();
            return await module.InvokeAsync<double>("getScrollTop", elementId);
        }
        catch (JSDisconnectedException) { return 0; }
    }

    // --- PullToRefresh gesture guard (#308) ---

    public async ValueTask RegisterPullToRefresh(string elementId)
    {
        try
        {
            var module = await GetModuleAsync();
            await module.InvokeVoidAsync("registerPullToRefresh", elementId);
        }
        catch (JSDisconnectedException) { }
    }

    public async ValueTask UnregisterPullToRefresh(string elementId)
    {
        try
        {
            var module = await GetModuleAsync();
            await module.InvokeVoidAsync("unregisterPullToRefresh", elementId);
        }
        catch (JSDisconnectedException) { }
    }

    // --- Wheel Pickers (DateWheelPicker / TimeWheelPicker) ---

    public async ValueTask<double> WheelScrollTop(Microsoft.AspNetCore.Components.ElementReference element)
    {
        try
        {
            var module = await GetModuleAsync();
            return await module.InvokeAsync<double>("wheelScrollTop", element);
        }
        catch (JSDisconnectedException) { return 0; }
    }

    public async ValueTask WheelScrollTo(Microsoft.AspNetCore.Components.ElementReference element, double top)
    {
        try
        {
            var module = await GetModuleAsync();
            await module.InvokeVoidAsync("wheelScrollTo", element, top);
        }
        catch (JSDisconnectedException) { }
    }

    // --- Pointer Capture (used by Splitter dividers) ---

    public async ValueTask SetPointerCaptureOnElement(string elementId, long pointerId)
    {
        try
        {
            var module = await GetModuleAsync();
            await module.InvokeVoidAsync("setPointerCaptureOnElement", elementId, pointerId);
        }
        catch (JSDisconnectedException) { }
    }

    public async ValueTask ReleasePointerCaptureOnElement(string elementId, long pointerId)
    {
        try
        {
            var module = await GetModuleAsync();
            await module.InvokeVoidAsync("releasePointerCaptureOnElement", elementId, pointerId);
        }
        catch (JSDisconnectedException) { }
    }

    // --- Drawer Swipe ---

    public async ValueTask RegisterDrawerSwipe(string elementId, string direction, Func<Task> handler)
    {
        var module = await GetModuleAsync();
        await _swipe.RegisterDrawerSwipe(module, GetSelfRef(), elementId, direction, handler);
    }

    public async ValueTask RegisterDrawerSwipe(string elementId, Func<Task> handler)
    {
        var module = await GetModuleAsync();
        await _swipe.RegisterDrawerSwipe(module, GetSelfRef(), elementId, handler);
    }

    // 3.0.1 — threshold-aware overload, ultimately driven by LumeoGestureOptions.
    public async ValueTask RegisterDrawerSwipe(string elementId, string direction, Func<Task> handler, int? activationPx, int? firePx)
    {
        var module = await GetModuleAsync();
        await _swipe.RegisterDrawerSwipe(module, GetSelfRef(), elementId, direction, handler, activationPx, firePx);
    }

    // 3.19 — adds velocity/flick dismiss (px/ms) on top of the distance thresholds.
    public async ValueTask RegisterDrawerSwipe(string elementId, string direction, Func<Task> handler, int? activationPx, int? firePx, double? velocity)
    {
        var module = await GetModuleAsync();
        await _swipe.RegisterDrawerSwipe(module, GetSelfRef(), elementId, direction, handler, activationPx, firePx, velocity);
    }

    public async ValueTask UnregisterDrawerSwipe(string elementId)
    {
        var module = await GetModuleAsync();
        await _swipe.UnregisterDrawerSwipe(module, elementId);
    }

    [JSInvokable]
    public async Task OnSwipeDismiss(string elementId) => await _swipe.OnSwipeDismiss(elementId);

    // --- Drawer Snap Points (3.19) ---

    public async ValueTask RegisterDrawerSnap(string elementId, string direction, Func<Task<bool>> dismissHandler, Func<int, Task> snapHandler, IReadOnlyList<double> snapPoints, int activeIndex, bool dismissible, int? activationPx, int? firePx, double? velocity)
    {
        var module = await GetModuleAsync();
        await _swipe.RegisterDrawerSnap(module, GetSelfRef(), elementId, direction, dismissHandler, snapHandler, snapPoints, activeIndex, dismissible, activationPx, firePx, velocity);
    }

    public async ValueTask SetDrawerSnap(string elementId, int index)
    {
        var module = await GetModuleAsync();
        await _swipe.SetDrawerSnap(module, elementId, index);
    }

    public async ValueTask UnregisterDrawerSnap(string elementId)
    {
        var module = await GetModuleAsync();
        await _swipe.UnregisterDrawerSnap(module, elementId);
    }

    [JSInvokable]
    public async Task OnDrawerSnapChange(string elementId, int index) => await _swipe.OnDrawerSnapChange(elementId, index);

    [JSInvokable]
    public async Task<bool> OnDrawerSnapDismiss(string elementId) => await _swipe.OnDrawerSnapDismiss(elementId);

    // --- Sortable Touch (rc.44 mobile fix) ---

    public async ValueTask RegisterSortableTouch(string containerId, Func<int, int, Task> handler)
    {
        var module = await GetModuleAsync();
        await _sortable.RegisterSortableTouch(module, GetSelfRef(), containerId, handler);
    }

    public async ValueTask UnregisterSortableTouch(string containerId)
    {
        try
        {
            var module = await GetModuleAsync();
            await _sortable.UnregisterSortableTouch(module, containerId);
        }
        catch (JSDisconnectedException) { }
    }

    [JSInvokable]
    public async Task OnSortableTouchDrop(string containerId, int source, int target)
        => await _sortable.OnSortableTouchDrop(containerId, source, target);

    // --- Carousel Swipe ---

    public async ValueTask RegisterCarouselSwipe(string elementId, string orientation, Func<string, Task> swipeHandler, Func<double, double, int, Task> scrollHandler)
    {
        var module = await GetModuleAsync();
        await _swipe.RegisterCarouselSwipe(module, GetSelfRef(), elementId, orientation, swipeHandler, scrollHandler);
    }

    public async ValueTask RegisterCarouselSwipe(string elementId, string orientation, Func<string, Task> swipeHandler, Func<double, double, int, Task> scrollHandler, int? swipeThresholdPx, int? verticalDeadZonePx)
    {
        var module = await GetModuleAsync();
        await _swipe.RegisterCarouselSwipe(module, GetSelfRef(), elementId, orientation, swipeHandler, scrollHandler, swipeThresholdPx, verticalDeadZonePx);
    }

    public async ValueTask UnregisterCarouselSwipe(string elementId)
    {
        var module = await GetModuleAsync();
        await _swipe.UnregisterCarouselSwipe(module, elementId);
    }

    // --- Horizontal Swipe (Calendar month navigation) ---

    public async ValueTask RegisterHorizontalSwipe(string elementId, Func<string, Task> handler)
    {
        var module = await GetModuleAsync();
        await _swipe.RegisterHorizontalSwipe(module, GetSelfRef(), elementId, handler);
    }

    public async ValueTask RegisterHorizontalSwipe(string elementId, Func<string, Task> handler, int? swipeThresholdPx, int? verticalDeadZonePx)
    {
        var module = await GetModuleAsync();
        await _swipe.RegisterHorizontalSwipe(module, GetSelfRef(), elementId, handler, swipeThresholdPx, verticalDeadZonePx);
    }

    public async ValueTask UnregisterHorizontalSwipe(string elementId)
    {
        var module = await GetModuleAsync();
        await _swipe.UnregisterHorizontalSwipe(module, elementId);
    }

    [JSInvokable]
    public async Task OnCalendarSwipe(string elementId, string direction)
        => await _swipe.OnCalendarSwipe(elementId, direction);

    // --- Gallery Swipe (ImageGallery fullscreen prev/next, rc.52) ---

    public async ValueTask RegisterGallerySwipe(string elementId, Func<string, Task> handler)
    {
        var module = await GetModuleAsync();
        await _swipe.RegisterGallerySwipe(module, GetSelfRef(), elementId, handler);
    }

    public async ValueTask RegisterGallerySwipe(string elementId, Func<string, Task> handler, int? swipeThresholdPx, int? verticalDeadZonePx)
    {
        var module = await GetModuleAsync();
        await _swipe.RegisterGallerySwipe(module, GetSelfRef(), elementId, handler, swipeThresholdPx, verticalDeadZonePx);
    }

    public async ValueTask UnregisterGallerySwipe(string elementId)
    {
        try
        {
            var module = await GetModuleAsync();
            await _swipe.UnregisterGallerySwipe(module, elementId);
        }
        catch (JSDisconnectedException) { }
    }

    [JSInvokable]
    public async Task OnGallerySwipe(string elementId, string direction)
        => await _swipe.OnGallerySwipe(elementId, direction);

    // --- Tab Swipe ---

    public async ValueTask RegisterTabSwipe(string elementId, bool wrap, Func<string, Task> handler)
    {
        var module = await GetModuleAsync();
        await _swipe.RegisterTabSwipe(module, GetSelfRef(), elementId, wrap, handler);
    }

    public async ValueTask RegisterTabSwipe(string elementId, bool wrap, Func<string, Task> handler, int? swipeThresholdPx, int? verticalDeadZonePx)
    {
        var module = await GetModuleAsync();
        await _swipe.RegisterTabSwipe(module, GetSelfRef(), elementId, wrap, handler, swipeThresholdPx, verticalDeadZonePx);
    }

    public async ValueTask UnregisterTabSwipe(string elementId)
    {
        try
        {
            var module = await GetModuleAsync();
            await _swipe.UnregisterTabSwipe(module, elementId);
        }
        catch (JSDisconnectedException) { }
    }

    [JSInvokable]
    public async Task OnTabSwipe(string elementId, string direction)
        => await _swipe.OnTabSwipe(elementId, direction);

    public async ValueTask CarouselScrollTo(string elementId, int index, string behavior = "smooth")
    {
        var module = await GetModuleAsync();
        await _swipe.CarouselScrollTo(module, elementId, index, behavior);
    }

    [JSInvokable]
    public async Task OnSwipe(string elementId, string direction) => await _swipe.OnSwipe(elementId, direction);

    [JSInvokable]
    public async Task OnScrollPosition(string elementId, double scrollPos, double maxScroll, int nearestIndex = -1)
        => await _swipe.OnScrollPosition(elementId, scrollPos, maxScroll, nearestIndex);

    // --- Resizable Handle ---

    public async ValueTask RegisterResizeHandle(string elementId, string direction, Func<double, Task> resizeHandler, Func<Task> resizeEndHandler)
    {
        var module = await GetModuleAsync();
        await _resize.RegisterResizeHandle(module, GetSelfRef(), elementId, direction, resizeHandler, resizeEndHandler);
    }

    public async ValueTask UnregisterResizeHandle(string elementId)
    {
        var module = await GetModuleAsync();
        await _resize.UnregisterResizeHandle(module, elementId);
    }

    [JSInvokable]
    public async Task OnResize(string elementId, double delta) => await _resize.OnResize(elementId, delta);

    [JSInvokable]
    public async Task OnResizeEnd(string elementId) => await _resize.OnResizeEnd(elementId);

    // --- Scrollspy ---

    public async ValueTask RegisterScrollspy(string containerId, int offset, bool smooth, Func<string?, Task> handler)
    {
        var module = await GetModuleAsync();
        await _scroll.RegisterScrollspy(module, GetSelfRef(), containerId, offset, smooth, handler);
    }

    public async ValueTask UnregisterScrollspy(string containerId)
    {
        var module = await GetModuleAsync();
        await _scroll.UnregisterScrollspy(module, containerId);
    }

    public async ValueTask ScrollspyScrollTo(string containerId, string sectionId, bool smooth)
    {
        var module = await GetModuleAsync();
        await _scroll.ScrollspyScrollTo(module, containerId, sectionId, smooth, 0);
    }

    public async ValueTask ScrollspyScrollTo(string containerId, string sectionId, bool smooth, int offset)
    {
        var module = await GetModuleAsync();
        await _scroll.ScrollspyScrollTo(module, containerId, sectionId, smooth, offset);
    }

    [JSInvokable]
    public async Task OnScrollspyUpdate(string containerId, string? activeId)
        => await _scroll.OnScrollspyUpdate(containerId, activeId);

    // --- Tabs overflow scroll arrows ---

    public async ValueTask RegisterTabsOverflow(string listId, Func<bool, bool, Task> handler)
    {
        var module = await GetModuleAsync();
        await _scroll.RegisterTabsOverflow(module, GetSelfRef(), listId, handler);
    }

    public async ValueTask UnregisterTabsOverflow(string listId)
    {
        // Runs during component teardown — swallow a circuit drop so the rest of
        // the dispose chain still executes.
        try
        {
            var module = await GetModuleAsync();
            await _scroll.UnregisterTabsOverflow(module, listId);
        }
        catch (Microsoft.JSInterop.JSDisconnectedException) { }
    }

    public async ValueTask TabsScrollBy(string listId, double delta, bool horizontal)
    {
        var module = await GetModuleAsync();
        await _scroll.TabsScrollBy(module, listId, delta, horizontal);
    }

    [JSInvokable]
    public async Task OnTabsOverflowChange(string listId, bool canScrollStart, bool canScrollEnd)
        => await _scroll.OnTabsOverflowChange(listId, canScrollStart, canScrollEnd);

    // --- Toast Swipe ---

    public async ValueTask RegisterToastSwipe(string elementId, string toastId, Func<string, Task> handler)
    {
        var module = await GetModuleAsync();
        await _swipe.RegisterToastSwipe(module, GetSelfRef(), elementId, toastId, handler);
    }

    public async ValueTask UnregisterToastSwipe(string toastId, string elementId)
    {
        var module = await GetModuleAsync();
        await _swipe.UnregisterToastSwipe(module, toastId, elementId);
    }

    [JSInvokable]
    public async Task OnToastSwipeDismiss(string toastId) => await _swipe.OnToastSwipeDismiss(toastId);

    // --- Auto Resize ---

    public async ValueTask SetupAutoResize(string elementId, int maxRows)
    {
        var module = await GetModuleAsync();
        await _utility.SetupAutoResize(module, elementId, maxRows);
    }

    public async ValueTask UnregisterAutoResize(string elementId)
    {
        var module = await GetModuleAsync();
        await _utility.UnregisterAutoResize(module, elementId);
    }

    // --- OTP Paste ---

    public async ValueTask RegisterOtpPaste(string baseId, int length, Func<string, Task> handler)
    {
        var module = await GetModuleAsync();
        await _utility.RegisterOtpPaste(module, GetSelfRef(), baseId, length, handler);
    }

    public async ValueTask UnregisterOtpPaste(string baseId, int length)
    {
        var module = await GetModuleAsync();
        await _utility.UnregisterOtpPaste(module, baseId, length);
    }

    [JSInvokable]
    public async Task OnOtpPaste(string baseId, string digits) => await _utility.OnOtpPaste(baseId, digits);

    // --- Selective keydown preventDefault ---

    public async ValueTask RegisterPreventDefaultKeys(string elementId, IReadOnlyList<PreventDefaultKeyRule> rules)
    {
        var module = await GetModuleAsync();
        await _utility.RegisterPreventDefaultKeys(module, elementId, rules);
    }

    public async ValueTask UnregisterPreventDefaultKeys(string elementId)
    {
        var module = await GetModuleAsync();
        await _utility.UnregisterPreventDefaultKeys(module, elementId);
    }

    // --- DataGrid Column Resize ---

    // Explicit implementation of the interface's original (non-autoFit) abstract
    // member (round-9 #4) — delegates to the autoFit-aware overload below with
    // autoFit always false, mirroring the interface's own default so callers that
    // still bind to the 4-parameter shape behave identically either way.
    public ValueTask RegisterColumnResize(string handleId, double minWidth, double? maxWidth, Func<double, Task> commitHandler) =>
        RegisterColumnResize(handleId, minWidth, maxWidth, (w, _) => commitHandler(w));

    public async ValueTask RegisterColumnResize(string handleId, double minWidth, double? maxWidth, Func<double, bool, Task> commitHandler)
    {
        var module = await GetModuleAsync();
        await _resize.RegisterColumnResize(module, GetSelfRef(), handleId, minWidth, maxWidth, commitHandler);
    }

    public async ValueTask UnregisterColumnResize(string handleId)
    {
        try
        {
            var module = await GetModuleAsync();
            await _resize.UnregisterColumnResize(module, handleId);
        }
        catch (JSDisconnectedException) { }
    }

    public async ValueTask NudgeColumnResize(string handleId, double delta)
    {
        var module = await GetModuleAsync();
        await _resize.NudgeColumnResize(module, handleId, delta);
    }

    [JSInvokable]
    public async Task OnColumnResize(string handleId, double delta) => await _resize.OnColumnResize(handleId, delta);

    [JSInvokable]
    public async Task OnColumnResizeEnd(string handleId) => await _resize.OnColumnResizeEnd(handleId);

    [JSInvokable]
    public async Task OnColumnResizeCommit(string handleId, double finalWidth, bool autoFit) => await _resize.OnColumnResizeCommit(handleId, finalWidth, autoFit);

    // --- DataGrid Column Reorder (pointer-based touch/pen) ---

    public async ValueTask RegisterColumnReorder(string gridId, Func<string, string, Task> commitHandler)
    {
        var module = await GetModuleAsync();
        await _reorder.RegisterColumnReorder(module, GetSelfRef(), gridId, commitHandler);
    }

    public async ValueTask UnregisterColumnReorder(string gridId)
    {
        try
        {
            var module = await GetModuleAsync();
            await _reorder.UnregisterColumnReorder(module, gridId);
        }
        catch (JSDisconnectedException) { }
    }

    [JSInvokable]
    public async Task OnColumnReorderCommit(string gridId, string sourceColumnId, string targetColumnId)
        => await _reorder.OnColumnReorderCommit(gridId, sourceColumnId, targetColumnId);

    // --- DataGrid Column Reorder FLIP Animation ---

    public async ValueTask CaptureColumnRects(string gridId)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("captureColumnRects", gridId);
    }

    public async ValueTask AnimateColumnReorder(string gridId, int durationMs)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("animateColumnReorder", gridId, durationMs);
    }

    public async ValueTask ClearColumnReorderTransforms(string gridId)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("clearColumnReorderTransforms", gridId);
    }

    // --- DataGrid Row Reorder (pointer-based mouse/touch/pen) ---

    public async ValueTask RegisterRowReorder(string gridId, Func<string, string, Task> commitHandler)
    {
        var module = await GetModuleAsync();
        await _reorder.RegisterRowReorder(module, GetSelfRef(), gridId, commitHandler);
    }

    public async ValueTask UnregisterRowReorder(string gridId)
    {
        try
        {
            var module = await GetModuleAsync();
            await _reorder.UnregisterRowReorder(module, gridId);
        }
        catch (JSDisconnectedException) { }
    }

    [JSInvokable]
    public async Task OnRowReorderCommit(string gridId, string sourceRowKey, string targetRowKey)
        => await _reorder.OnRowReorderCommit(gridId, sourceRowKey, targetRowKey);

    // --- DataGrid Row Reorder FLIP Animation ---

    public async ValueTask CaptureRowRects(string gridId)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("captureRowRects", gridId);
    }

    public async ValueTask AnimateRowReorder(string gridId, int durationMs)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("animateRowReorder", gridId, durationMs);
    }

    public async ValueTask ClearRowReorderTransforms(string gridId)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("clearRowReorderTransforms", gridId);
    }

    // --- Tour: Element Rect By Selector ---

    public async ValueTask<ElementRect?> GetElementRectBySelector(string selector)
    {
        var module = await GetModuleAsync();
        return await _floatingPosition.GetElementRectBySelector(module, selector);
    }

    public async ValueTask ScrollSelectorIntoView(string selector)
    {
        var module = await GetModuleAsync();
        await _floatingPosition.ScrollSelectorIntoView(module, selector);
    }

    public async ValueTask ScrollIntoView(string elementId, string block = "nearest")
    {
        try
        {
            var module = await GetModuleAsync();
            await _floatingPosition.ScrollIntoView(module, elementId, block);
        }
        catch (JSDisconnectedException) { }
    }

    // --- Affix ---

    public async ValueTask RegisterAffix(string elementId, int offsetTop, int? offsetBottom, string? target, Func<bool, Task> handler)
    {
        var module = await GetModuleAsync();
        await _scroll.RegisterAffix(module, GetSelfRef(), elementId, offsetTop, offsetBottom, target, handler);
    }

    public async ValueTask UnregisterAffix(string elementId)
    {
        var module = await GetModuleAsync();
        await _scroll.UnregisterAffix(module, elementId);
    }

    [JSInvokable]
    public async Task OnAffixChanged(string elementId, bool isFixed) => await _scroll.OnAffixChanged(elementId, isFixed);

    // --- Mention: Textarea Caret Position ---

    public async ValueTask<TextareaCaretInfo> GetTextareaCaretPosition(string elementId)
    {
        var module = await GetModuleAsync();
        return await _utility.GetTextareaCaretPosition(module, elementId);
    }

    /// <summary><see cref="Top"/>/<see cref="Left"/> are viewport-relative (legacy).
    /// <see cref="OffsetTop"/>/<see cref="OffsetLeft"/> are relative to the textarea's
    /// offset parent (its <c>position: relative</c> wrapper) so a caret-anchored
    /// dropdown can be positioned absolutely and track the textarea on scroll.
    /// <see cref="LineHeight"/> is the computed line height so callers can drop the
    /// dropdown one line below the caret.</summary>
    public record TextareaCaretInfo(double Top, double Left, int SelectionStart, double OffsetTop = 0, double OffsetLeft = 0, double LineHeight = 20)
    {
        // Trim safety: JSRuntime's reflection-based serializer must never bind the
        // positional ctor — the trimmer strips its parameter names
        // ("ConstructorContainsNullParameterNames", crashes the component under a
        // trimmed publish). With this parameterless ctor STJ uses property-based
        // (de)serialization instead. Do not remove.
        public TextareaCaretInfo() : this(0, 0, 0) { }
    }

    // --- InputMask caret (text input selectionStart) ---
    // Read/restore the caret of a masked text <input> so edits insert and delete
    // at the caret rather than snapping to the end after re-masking.

    public async ValueTask<int> GetInputCaret(string elementId)
    {
        try
        {
            var module = await GetModuleAsync();
            return await module.InvokeAsync<int>("getInputCaret", elementId);
        }
        catch (JSDisconnectedException) { return 0; }
    }

    public async ValueTask SetInputCaret(string elementId, int position)
    {
        try
        {
            var module = await GetModuleAsync();
            await module.InvokeVoidAsync("setInputCaret", elementId, position);
        }
        catch (JSDisconnectedException) { }
    }

    public async ValueTask SetInputValue(string elementId, string value)
    {
        try
        {
            var module = await GetModuleAsync();
            await module.InvokeVoidAsync("setInputValue", elementId, value);
        }
        catch (JSDisconnectedException) { }
    }

    // --- BackToTop ---

    public ValueTask RegisterBackToTop(string id, int threshold, Func<bool, Task> handler)
        => RegisterBackToTop(id, threshold, handler, null);

    public async ValueTask RegisterBackToTop(string id, int threshold, Func<bool, Task> handler, string? target)
    {
        var module = await GetModuleAsync();
        await _scroll.RegisterBackToTop(module, GetSelfRef(), id, threshold, handler, target);
    }

    public async ValueTask UnregisterBackToTop(string id)
    {
        var module = await GetModuleAsync();
        await _scroll.UnregisterBackToTop(module, id);
    }

    public ValueTask ScrollToTop() => ScrollToTop(null);

    public async ValueTask ScrollToTop(string? target)
    {
        var module = await GetModuleAsync();
        await _scroll.ScrollToTop(module, target);
    }

    [JSInvokable]
    public async Task OnScrollVisibilityChanged(string id, bool visible) => await _scroll.OnScrollVisibilityChanged(id, visible);

    // --- File Download ---

    public async ValueTask DownloadFile(string fileName, string contentBase64, string mimeType = "application/octet-stream")
    {
        var module = await GetModuleAsync();
        await _utility.DownloadFile(module, fileName, contentBase64, mimeType);
    }

    // --- Clipboard ---

    public async ValueTask CopyToClipboard(string text)
    {
        var module = await GetModuleAsync();
        await _utility.CopyToClipboard(module, text);
    }

    // --- Press feedback (ripple click effect) ---

    public async ValueTask RippleAttachAsync(Microsoft.AspNetCore.Components.ElementReference element)
    {
        try
        {
            var module = await GetModuleAsync();
            await module.InvokeVoidAsync("ripple.attach", element);
        }
        catch (JSDisconnectedException) { }
    }

    public async ValueTask RippleDetachAsync(Microsoft.AspNetCore.Components.ElementReference element)
    {
        try
        {
            var module = await GetModuleAsync();
            await module.InvokeVoidAsync("ripple.detach", element);
        }
        catch (JSDisconnectedException) { }
    }

    // --- File input reset (#70) ---

    public async ValueTask ResetFileInput(Microsoft.AspNetCore.Components.ElementReference element)
    {
        try
        {
            var module = await GetModuleAsync();
            await module.InvokeVoidAsync("resetFileInput", element);
        }
        catch (JSDisconnectedException) { }
    }

    // --- Reduced motion (core) + TouchRipple coordinate resolution ---

    public async ValueTask<bool> PrefersReducedMotion()
    {
        try
        {
            var module = await GetModuleAsync();
            return await module.InvokeAsync<bool>("prefersReducedMotion");
        }
        catch (JSDisconnectedException) { return false; }
    }

    public async ValueTask<bool> IsActiveElementFocusVisible()
    {
        // Fails OPEN (assume focus-visible) on ANY interop failure, not just disconnection:
        // this is a UI-polish detail (avoid a click-focus tooltip sticking open), so an
        // unrelated interop hiccup must never suppress a genuine keyboard-focus tooltip.
        try
        {
            var module = await GetModuleAsync();
            return await module.InvokeAsync<bool>("isActiveElementFocusVisible");
        }
        catch (Exception) { return true; }
    }

    // Trim safety: the deserializer constructs RipplePoint purely via reflection, which the
    // linker cannot see — without this the parameterless ctor/property setters get
    // trimmed and JSRuntime throws ConstructorContainsNullParameterNames at runtime.
    [System.Diagnostics.CodeAnalysis.DynamicDependency(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties, typeof(RipplePoint))]
    public async ValueTask<RipplePoint> TouchRippleCoords(string hostElementId, double clientX, double clientY)
    {
        try
        {
            var module = await GetModuleAsync();
            return await module.InvokeAsync<RipplePoint>("touchRippleCoords", hostElementId, clientX, clientY);
        }
        catch (JSDisconnectedException) { return new RipplePoint(0, 0); }
    }

    // --- HTMLMediaElement helpers (AudioPlayer 3.1.0) ---

    public async ValueTask PlayMedia(Microsoft.AspNetCore.Components.ElementReference element)
    {
        try
        {
            var module = await GetModuleAsync();
            await module.InvokeVoidAsync("playMedia", element);
        }
        catch (JSDisconnectedException) { }
    }

    public async ValueTask PauseMedia(Microsoft.AspNetCore.Components.ElementReference element)
    {
        try
        {
            var module = await GetModuleAsync();
            await module.InvokeVoidAsync("pauseMedia", element);
        }
        catch (JSDisconnectedException) { }
    }

    public async ValueTask SetMediaVolume(Microsoft.AspNetCore.Components.ElementReference element, double volume, bool muted)
    {
        try
        {
            var module = await GetModuleAsync();
            await module.InvokeVoidAsync("setMediaVolume", element, volume, muted);
        }
        catch (JSDisconnectedException) { }
    }

    public async ValueTask SeekMedia(Microsoft.AspNetCore.Components.ElementReference element, double seconds)
    {
        try
        {
            var module = await GetModuleAsync();
            await module.InvokeVoidAsync("seekMedia", element, seconds);
        }
        catch (JSDisconnectedException) { }
    }

    public async ValueTask SetPlaybackRate(Microsoft.AspNetCore.Components.ElementReference element, double rate)
    {
        try
        {
            var module = await GetModuleAsync();
            await module.InvokeVoidAsync("setPlaybackRate", element, rate);
        }
        catch (JSDisconnectedException) { }
    }

    // Trim safety: the deserializer constructs MediaState purely via reflection, which the
    // linker cannot see — without this the parameterless ctor/property setters get
    // trimmed and JSRuntime throws ConstructorContainsNullParameterNames at runtime.
    [System.Diagnostics.CodeAnalysis.DynamicDependency(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties, typeof(MediaState))]
    public async ValueTask<MediaState> GetMediaState(Microsoft.AspNetCore.Components.ElementReference element)
    {
        try
        {
            var module = await GetModuleAsync();
            return await module.InvokeAsync<MediaState>("getMediaState", element);
        }
        catch (JSDisconnectedException)
        {
            return new MediaState(0, 0);
        }
    }

    // --- Haptic feedback ---

    public async ValueTask Vibrate(int milliseconds)
    {
        try
        {
            var module = await GetModuleAsync();
            await module.InvokeVoidAsync("vibrate", milliseconds);
        }
        catch (JSDisconnectedException) { }
    }

    // --- LocalStorage ---

    public async ValueTask SaveToLocalStorage(string key, string value)
    {
        var module = await GetModuleAsync();
        await _utility.SaveToLocalStorage(module, key, value);
    }

    public async ValueTask<string?> LoadFromLocalStorage(string key)
    {
        var module = await GetModuleAsync();
        return await _utility.LoadFromLocalStorage(module, key);
    }

    public async ValueTask RemoveFromLocalStorage(string key)
    {
        var module = await GetModuleAsync();
        await _utility.RemoveFromLocalStorage(module, key);
    }

    // --- Motion primitives ---
    // These methods delegate to the Lumeo.Motion satellite JS module, loaded
    // lazily via GetMotionModuleAsync(). The C# API surface is unchanged so
    // existing consumers of IComponentInteropService keep working as-is.

    public async ValueTask MotionTickNumber(string elementId, double from, double to, int durationMs, int decimals, string separator = ",", string decimalSeparator = ".")
    {
        var module = await GetMotionModuleAsync();
        await module.InvokeVoidAsync("motion.tickNumber", elementId, from, to, durationMs, decimals, separator, decimalSeparator);
    }

    public async ValueTask MotionDisposeTicker(string elementId)
    {
        try
        {
            var module = await GetMotionModuleAsync();
            await module.InvokeVoidAsync("motion.disposeTicker", elementId);
        }
        catch (JSDisconnectedException) { }
    }

    // NOTE (trim safety): JS-interop option bags must NOT be anonymous types. Under a
    // trimmed publish (IsTrimmable) the linker strips the anonymous type's constructor
    // parameter names, and JSRuntime's reflection-based serializer then throws
    // NotSupportedException("ConstructorContainsNullParameterNames") at runtime —
    // crashing the component (hit live on the docs site by BlurFade). A
    // Dictionary<string, object?> serializes to the identical JSON object (keys pass
    // through verbatim; JSRuntime applies no DictionaryKeyPolicy) with no constructor
    // metadata involved, so the JS side is unchanged.
    public async ValueTask MotionRevealText(string elementId, int staggerMs, double threshold)
    {
        var module = await GetMotionModuleAsync();
        await module.InvokeVoidAsync("motion.revealText", elementId,
            new Dictionary<string, object?> { ["stagger"] = staggerMs, ["threshold"] = threshold });
    }

    public async ValueTask MotionBlurFade(string elementId, int delayMs, bool once, bool forceHidden = false)
    {
        var module = await GetMotionModuleAsync();
        await module.InvokeVoidAsync("motion.blurFade", elementId,
            new Dictionary<string, object?> { ["delayMs"] = delayMs, ["once"] = once, ["forceHidden"] = forceHidden });
    }

    public async ValueTask MotionDisposeObserver(string elementId)
    {
        try
        {
            var module = await GetMotionModuleAsync();
            await module.InvokeVoidAsync("motion.disposeObserver", elementId);
        }
        catch (JSDisconnectedException) { }
    }

    public async ValueTask<bool> MotionPrefersReducedMotion()
    {
        try
        {
            var module = await GetMotionModuleAsync();
            return await module.InvokeAsync<bool>("motion.prefersReducedMotion");
        }
        catch (JSDisconnectedException) { return false; }
    }

    public async ValueTask MotionAnimatedBeam(string elementId, string fromId, string toId, object options)
    {
        try
        {
            var module = await GetMotionModuleAsync();
            await module.InvokeVoidAsync("motion.animatedBeam", elementId, fromId, toId, options);
        }
        catch (JSDisconnectedException) { }
    }

    public async ValueTask MotionDisposeAnimatedBeam(string elementId)
    {
        try
        {
            var module = await GetMotionModuleAsync();
            await module.InvokeVoidAsync("motion.disposeAnimatedBeam", elementId);
        }
        catch (JSDisconnectedException) { }
    }

    public async ValueTask MotionDock(string elementId, object options)
    {
        try
        {
            var module = await GetMotionModuleAsync();
            await module.InvokeVoidAsync("motion.dock", elementId, options);
        }
        catch (JSDisconnectedException) { }
    }

    public async ValueTask MotionDisposeDock(string elementId)
    {
        try
        {
            var module = await GetMotionModuleAsync();
            await module.InvokeVoidAsync("motion.disposeDock", elementId);
        }
        catch (JSDisconnectedException) { }
    }

    public async ValueTask MotionConfettiInit(string elementId)
    {
        try
        {
            var module = await GetMotionModuleAsync();
            await module.InvokeVoidAsync("motion.confettiInit", elementId);
        }
        catch (JSDisconnectedException) { }
    }

    public async ValueTask MotionConfettiFire(string elementId, object options)
    {
        try
        {
            var module = await GetMotionModuleAsync();
            await module.InvokeVoidAsync("motion.confettiFire", elementId, options);
        }
        catch (JSDisconnectedException) { }
    }

    public async ValueTask MotionDisposeConfetti(string elementId)
    {
        try
        {
            var module = await GetMotionModuleAsync();
            await module.InvokeVoidAsync("motion.disposeConfetti", elementId);
        }
        catch (JSDisconnectedException) { }
    }

    // --- Tabs sliding indicator measurement ---

    public record TabMeasurement(double X, double Width)
    {
        // Trim safety: see TextareaCaretInfo's parameterless ctor above. Do not remove.
        public TabMeasurement() : this(0, 0) { }
    }

    // Trim safety: the deserializer constructs TabMeasurement purely via reflection, which the
    // linker cannot see — without this the parameterless ctor/property setters get
    // trimmed and JSRuntime throws ConstructorContainsNullParameterNames at runtime.
    [System.Diagnostics.CodeAnalysis.DynamicDependency(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties, typeof(TabMeasurement))]
    public async ValueTask<TabMeasurement?> TabsMeasure(string elementId)
    {
        try
        {
            var module = await GetModuleAsync();
            return await module.InvokeAsync<TabMeasurement?>("tabs.measure", elementId);
        }
        catch (JSDisconnectedException)
        {
            return null;
        }
    }

    // --- AI primitives ---

    public async ValueTask AiAutosize(string elementId, int maxPx)
    {
        try
        {
            var module = await GetModuleAsync();
            await module.InvokeVoidAsync("ai.autosize", elementId, maxPx);
        }
        catch (JSDisconnectedException) { }
    }

    public async ValueTask AiObserveAutoScroll(string elementId)
    {
        try
        {
            var module = await GetModuleAsync();
            await module.InvokeVoidAsync("ai.observeAutoScroll", elementId);
        }
        catch (JSDisconnectedException) { }
    }

    public async ValueTask AiDisposeAutoScroll(string elementId)
    {
        try
        {
            var module = await GetModuleAsync();
            await module.InvokeVoidAsync("ai.disposeAutoScroll", elementId);
        }
        catch (JSDisconnectedException) { }
    }

    public async ValueTask AiScrollToBottom(string elementId)
    {
        try
        {
            var module = await GetModuleAsync();
            await module.InvokeVoidAsync("ai.scrollToBottom", elementId);
        }
        catch (JSDisconnectedException) { }
    }

    public async ValueTask AiObserveScrollButton<[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicMethods)] T>(string elementId, DotNetObjectReference<T> dotNetRef) where T : class
    {
        try
        {
            var module = await GetModuleAsync();
            await module.InvokeVoidAsync("ai.observeScrollButton", elementId, dotNetRef);
        }
        catch (JSDisconnectedException) { }
    }

    public async ValueTask AiDisposeScrollButton(string elementId)
    {
        try
        {
            var module = await GetModuleAsync();
            await module.InvokeVoidAsync("ai.disposeScrollButton", elementId);
        }
        catch (JSDisconnectedException) { }
    }

    // --- Generic module import ---
    // Allows components that manage their own heavy JS modules (e.g. Chart with
    // echarts-interop.js) to import via the service rather than injecting IJSRuntime
    // directly in the component, satisfying the "no direct IJSRuntime in components" rule.

    public async ValueTask<IJSObjectReference> ImportModuleAsync(string moduleUrl)
        => await _jsRuntime.InvokeAsync<IJSObjectReference>("import", AppendVersion(moduleUrl));

    // Appends ?v=<assembly-version> to library JS module URLs that live under
    // _content/Lumeo*/ so a version bump invalidates the browser cache. See
    // GetModuleAsync for the full rationale. URLs that already carry a query
    // string are passed through untouched so consumers can override (e.g. for
    // local debugging) without us clobbering their fragment.
    private static string AppendVersion(string url)
    {
        if (string.IsNullOrEmpty(url) || url.Contains('?')) return url;
        if (!url.StartsWith("./_content/Lumeo", StringComparison.Ordinal)) return url;
        return $"{url}?v={_jsModuleVersion}";
    }

    // --- Scheduler (FullCalendar wrapper) ---
    // Scheduler ships its own JS module (scheduler.js) loaded lazily on first
    // use; the library is hefty (>200KB gzip) and many apps never touch it.

    private IJSObjectReference? _schedulerModule;

    private async Task<IJSObjectReference> GetSchedulerModuleAsync()
    {
        _schedulerModule ??= await _jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/Lumeo.Scheduler/js/scheduler.js");
        return _schedulerModule;
    }

    public async Task<string> SchedulerInitAsync(Microsoft.AspNetCore.Components.ElementReference el, object dotNetRef, object options)
    {
        var module = await GetSchedulerModuleAsync();
        return await module.InvokeAsync<string>("scheduler.init", el, dotNetRef, options);
    }

    public async Task SchedulerSetEventsAsync(string id, IEnumerable<object> events)
    {
        try
        {
            var module = await GetSchedulerModuleAsync();
            await module.InvokeVoidAsync("scheduler.setEvents", id, events);
        }
        catch (JSDisconnectedException) { }
    }

    public async Task SchedulerChangeViewAsync(string id, string view)
    {
        try
        {
            var module = await GetSchedulerModuleAsync();
            await module.InvokeVoidAsync("scheduler.changeView", id, view);
        }
        catch (JSDisconnectedException) { }
    }

    public async Task SchedulerGotoDateAsync(string id, string dateIso)
    {
        try
        {
            var module = await GetSchedulerModuleAsync();
            await module.InvokeVoidAsync("scheduler.gotoDate", id, dateIso);
        }
        catch (JSDisconnectedException) { }
    }

    public async Task SchedulerPrevAsync(string id)
    {
        try
        {
            var module = await GetSchedulerModuleAsync();
            await module.InvokeVoidAsync("scheduler.prev", id);
        }
        catch (JSDisconnectedException) { }
    }

    public async Task SchedulerNextAsync(string id)
    {
        try
        {
            var module = await GetSchedulerModuleAsync();
            await module.InvokeVoidAsync("scheduler.next", id);
        }
        catch (JSDisconnectedException) { }
    }

    public async Task SchedulerTodayAsync(string id)
    {
        try
        {
            var module = await GetSchedulerModuleAsync();
            await module.InvokeVoidAsync("scheduler.today", id);
        }
        catch (JSDisconnectedException) { }
    }

    public async Task<string> SchedulerGetTitleAsync(string id)
    {
        try
        {
            var module = await GetSchedulerModuleAsync();
            return await module.InvokeAsync<string>("scheduler.getTitle", id) ?? string.Empty;
        }
        catch (JSDisconnectedException) { return string.Empty; }
    }

    public async Task SchedulerDestroyAsync(string id)
    {
        try
        {
            if (_schedulerModule is null) return;
            await _schedulerModule.InvokeVoidAsync("scheduler.destroy", id);
        }
        catch (JSDisconnectedException) { }
    }

    // --- Gantt (Frappe Gantt wrapper) ---
    // Frappe Gantt is a small SVG-based lib (~20KB gzip) but we still lazy-load
    // it so apps without a Gantt anywhere don't pay the bundle cost.

    private IJSObjectReference? _ganttModule;

    private async Task<IJSObjectReference> GetGanttModuleAsync()
    {
        _ganttModule ??= await _jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/Lumeo.Gantt/js/gantt-v2.js");
        return _ganttModule;
    }

    public async Task<string> GanttInitAsync(Microsoft.AspNetCore.Components.ElementReference el, object dotNetRef, object options)
    {
        var module = await GetGanttModuleAsync();
        return await module.InvokeAsync<string>("gantt.init", el, dotNetRef, options);
    }

    public async Task GanttSetTasksAsync(string id, IEnumerable<object> tasks)
    {
        try
        {
            var module = await GetGanttModuleAsync();
            await module.InvokeVoidAsync("gantt.setTasks", id, tasks);
        }
        catch (JSDisconnectedException) { }
    }

    public async Task GanttRefreshAsync(string id, object options)
    {
        try
        {
            var module = await GetGanttModuleAsync();
            await module.InvokeVoidAsync("gantt.refresh", id, options);
        }
        catch (JSDisconnectedException) { }
    }

    public async Task GanttChangeViewModeAsync(string id, string mode)
    {
        try
        {
            var module = await GetGanttModuleAsync();
            await module.InvokeVoidAsync("gantt.changeViewMode", id, mode);
        }
        catch (JSDisconnectedException) { }
    }

    public async Task GanttDestroyAsync(string id)
    {
        try
        {
            if (_ganttModule is null) return;
            await _ganttModule.InvokeVoidAsync("gantt.destroy", id);
        }
        catch (JSDisconnectedException) { }
    }

    // --- GanttV3 (Blazor-rendered timeline) scroll interop ---
    // Its own tiny module (gantt-v3.js), separate from gantt-v2's Frappe-style
    // wrapper module above — v3 has no JS renderer of its own, only this one
    // scroll helper, so a shared module would be a needless coupling between
    // the two independently-lifecycled Gantt implementations.

    private IJSObjectReference? _ganttV3Module;

    private async Task<IJSObjectReference> GetGanttV3ModuleAsync()
    {
        _ganttV3Module ??= await _jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/Lumeo.Gantt/js/gantt-v3.js");
        return _ganttV3Module;
    }

    public async Task GanttV3ScrollToXAsync(Microsoft.AspNetCore.Components.ElementReference el, double targetX)
    {
        try
        {
            var module = await GetGanttV3ModuleAsync();
            await module.InvokeVoidAsync("ganttV3.centerOn", el, targetX);
        }
        catch (JSDisconnectedException) { }
    }

    public async Task<string?> GanttV3GetLocalDateAsync()
    {
        try
        {
            var module = await GetGanttV3ModuleAsync();
            return await module.InvokeAsync<string>("ganttV3.getLocalDateIso");
        }
        catch (JSDisconnectedException) { return null; }
    }

    public async Task GanttV3RegisterHeaderScrollSyncAsync(Microsoft.AspNetCore.Components.ElementReference canvasEl, Microsoft.AspNetCore.Components.ElementReference headerInnerEl)
    {
        try
        {
            var module = await GetGanttV3ModuleAsync();
            await module.InvokeVoidAsync("ganttV3.registerHeaderScrollSync", canvasEl, headerInnerEl);
        }
        catch (JSDisconnectedException) { }
    }

    public async Task GanttV3UnregisterHeaderScrollSyncAsync(Microsoft.AspNetCore.Components.ElementReference canvasEl)
    {
        try
        {
            var module = await GetGanttV3ModuleAsync();
            await module.InvokeVoidAsync("ganttV3.unregisterHeaderScrollSync", canvasEl);
        }
        catch (JSDisconnectedException) { }
    }

    public async Task GanttV3RegisterVerticalScrollTrackingAsync<[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicMethods)] T>(Microsoft.AspNetCore.Components.ElementReference scrollEl, DotNetObjectReference<T> dotNetRef) where T : class
    {
        try
        {
            var module = await GetGanttV3ModuleAsync();
            await module.InvokeVoidAsync("ganttV3.registerVerticalScrollTracking", scrollEl, dotNetRef);
        }
        catch (JSDisconnectedException) { }
    }

    public async Task GanttV3UnregisterVerticalScrollTrackingAsync(Microsoft.AspNetCore.Components.ElementReference scrollEl)
    {
        try
        {
            var module = await GetGanttV3ModuleAsync();
            await module.InvokeVoidAsync("ganttV3.unregisterVerticalScrollTracking", scrollEl);
        }
        catch (JSDisconnectedException) { }
    }

    // --- Toolbar overflow observer ---
    // Toolbar ships its own tiny JS module (toolbar.js) that wraps a
    // ResizeObserver to measure how many child items fit before an overflow
    // "..." trigger is needed. Loaded lazily so apps that never use the
    // overflow feature don't pay the import cost. Cache-busted by assembly
    // version like the main components.js (see GetModuleAsync for rationale).

    private async Task<IJSObjectReference> GetToolbarModuleAsync()
    {
        _toolbarModule ??= await _jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", $"./_content/Lumeo/js/toolbar.js?v={_jsModuleVersion}");
        return _toolbarModule;
    }

    private readonly Dictionary<string, Func<int, int, Task>> _toolbarOverflowHandlers = new();

    public async ValueTask RegisterToolbarOverflow(string elementId, Func<int, int, Task> handler)
    {
        var module = await GetToolbarModuleAsync();
        _toolbarOverflowHandlers[elementId] = handler;
        await module.InvokeVoidAsync(
            "observeToolbarOverflow", elementId, GetSelfRef(), nameof(OnToolbarOverflowMeasured));
    }

    public async ValueTask UnregisterToolbarOverflow(string elementId)
    {
        _toolbarOverflowHandlers.Remove(elementId);
        try
        {
            if (_toolbarModule is null) return;
            await _toolbarModule.InvokeVoidAsync("disposeToolbarOverflow", elementId);
        }
        catch (JSDisconnectedException) { }
    }

    [JSInvokable]
    public Task OnToolbarOverflowMeasured(string elementId, int fittingCount, int totalCount)
    {
        if (_toolbarOverflowHandlers.TryGetValue(elementId, out var handler))
            return handler(fittingCount, totalCount);
        return Task.CompletedTask;
    }

    // --- Rich Text Editor (TipTap) ---

    public async ValueTask<string> RichTextInitAsync<[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicMethods)] T>(
        Microsoft.AspNetCore.Components.ElementReference elementRef,
        DotNetObjectReference<T> dotNetRef,
        object options)
        where T : class
        => await _richText.InitAsync(_jsRuntime, elementRef, dotNetRef, options);

    public ValueTask RichTextSetContentAsync(string id, string? html)
        => _richText.SetContentAsync(_jsRuntime, id, html);

    public ValueTask RichTextCommandAsync(string id, string name, params object?[]? args)
        => _richText.CommandAsync(_jsRuntime, id, name, args);

    public ValueTask<Interop.RichTextActiveState?> RichTextGetActiveAsync(string id)
        => _richText.GetActiveAsync(_jsRuntime, id);

    public ValueTask RichTextSetDisabledAsync(string id, bool disabled)
        => _richText.SetDisabledAsync(_jsRuntime, id, disabled);

    public ValueTask RichTextDestroyAsync(string id)
        => _richText.DestroyAsync(_jsRuntime, id);

    public ValueTask<string?> RichTextPromptLinkAsync(string? initial)
        => _richText.PromptLinkAsync(_jsRuntime, initial);

    public ValueTask RichTextSetAriaAttributesAsync(string id, bool ariaInvalid, string? ariaDescribedBy)
        => _richText.SetAriaAttributesAsync(_jsRuntime, id, ariaInvalid, ariaDescribedBy);

    // --- SignaturePad ---
    // SignaturePad ships its own tiny JS module (signature-pad.js) that wires
    // pointer events to a canvas. Loaded lazily so apps that never sign a
    // contract don't pay the import cost. Cache-busted by assembly version
    // like the main components.js (see GetModuleAsync for rationale).

    private async Task<IJSObjectReference> GetSignaturePadModuleAsync()
    {
        _signaturePadModule ??= await _jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", $"./_content/Lumeo/js/signature-pad.js?v={_jsModuleVersion}");
        return _signaturePadModule;
    }

    public async ValueTask SignaturePadInit<[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicMethods)] T>(string elementId, object options, DotNetObjectReference<T> dotNetRef) where T : class
    {
        try
        {
            var module = await GetSignaturePadModuleAsync();
            await module.InvokeVoidAsync("init", elementId, options, dotNetRef);
        }
        catch (JSDisconnectedException) { }
    }

    public async ValueTask SignaturePadClear(string elementId)
    {
        try
        {
            if (_signaturePadModule is null) return;
            await _signaturePadModule.InvokeVoidAsync("clear", elementId);
        }
        catch (JSDisconnectedException) { }
    }

    public async ValueTask<string?> SignaturePadDataUrl(string elementId, string mimeType)
    {
        try
        {
            if (_signaturePadModule is null) return null;
            return await _signaturePadModule.InvokeAsync<string?>("getDataUrl", elementId, mimeType);
        }
        catch (JSDisconnectedException) { return null; }
    }

    public async ValueTask SignaturePadSetStrokeStyle(string elementId, string color, double width)
    {
        try
        {
            if (_signaturePadModule is null) return;
            await _signaturePadModule.InvokeVoidAsync("setStrokeStyle", elementId, color, width);
        }
        catch (JSDisconnectedException) { }
    }

    public async ValueTask SignaturePadSetDisabled(string elementId, bool disabled)
    {
        try
        {
            if (_signaturePadModule is null) return;
            await _signaturePadModule.InvokeVoidAsync("setDisabled", elementId, disabled);
        }
        catch (JSDisconnectedException) { }
    }

    public async ValueTask SignaturePadLoadDataUrl(string elementId, string? dataUrl)
    {
        try
        {
            if (_signaturePadModule is null) return;
            await _signaturePadModule.InvokeVoidAsync("loadDataUrl", elementId, dataUrl);
        }
        catch (JSDisconnectedException) { }
    }

    public async ValueTask SignaturePadDestroy(string elementId)
    {
        try
        {
            if (_signaturePadModule is null) return;
            await _signaturePadModule.InvokeVoidAsync("destroy", elementId);
        }
        catch (JSDisconnectedException) { }
    }

    public void Dispose()
    {
        // Best-effort: fire-and-forget the JS-side cleanup so browser observers/listeners
        // don't leak between scopes. Sync Dispose() is rarely the dispose path in Blazor
        // (DI prefers IAsyncDisposable when both are implemented), but if it IS called —
        // e.g. circuit teardown, custom IServiceScope disposal — we still want JS state
        // released. The async work runs detached; lifecycle exceptions are silently
        // swallowed because there's no caller to surface them to.
        _ = DisposeJsStateAsync();
        _selfRef?.Dispose();
    }

    private async Task DisposeJsStateAsync()
    {
        try
        {
            await DisposeJsRegistrationsAsync();
        }
        catch (JSDisconnectedException) { }
        catch (ObjectDisposedException) { }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ComponentInteropService] sync-dispose JS cleanup error: {ex}");
        }
    }

    private async Task DisposeJsRegistrationsAsync()
    {
        foreach (var id in _clickOutside.RegisteredIds.ToList())
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

        _clickOutside.Clear();
        _swipe.Clear();
        _resize.Clear();
        _reorder.Clear();
        _scroll.Clear();
        _utility.Clear();
        _sortable.Clear();

        await _richText.DisposeAsync();

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

        if (_motionModule is not null)
        {
            try
            {
                await _motionModule.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // Circuit disconnected, safe to ignore
            }
        }

        if (_schedulerModule is not null)
        {
            try
            {
                await _schedulerModule.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // Circuit disconnected, safe to ignore
            }
        }

        if (_ganttModule is not null)
        {
            try
            {
                await _ganttModule.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // Circuit disconnected, safe to ignore
            }
        }

        if (_ganttV3Module is not null)
        {
            try
            {
                await _ganttV3Module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // Circuit disconnected, safe to ignore
            }
        }

        if (_toolbarModule is not null)
        {
            try
            {
                await _toolbarModule.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // Circuit disconnected, safe to ignore
            }
        }

        if (_signaturePadModule is not null)
        {
            try
            {
                await _signaturePadModule.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // Circuit disconnected, safe to ignore
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeJsRegistrationsAsync();
        _selfRef?.Dispose();
    }
}
