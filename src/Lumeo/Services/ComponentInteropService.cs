using Lumeo.Services.Interop;
using Microsoft.JSInterop;

namespace Lumeo.Services;

public sealed class ComponentInteropService : IComponentInteropService
{
    private readonly IJSRuntime _jsRuntime;
    private IJSObjectReference? _module;
    private IJSObjectReference? _motionModule;
    private DotNetObjectReference<ComponentInteropService>? _selfRef;

    // Adapters
    private readonly ClickOutsideInterop _clickOutside = new();
    private readonly FloatingPositionInterop _floatingPosition = new();
    private readonly FocusInterop _focus = new();
    private readonly SwipeInterop _swipe = new();
    private readonly ResizeInterop _resize = new();
    private readonly ScrollInterop _scroll = new();
    private readonly UtilityInterop _utility = new();
    private readonly RichTextInterop _richText = new();

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

    public async ValueTask LockScroll()
    {
        var module = await GetModuleAsync();
        await _focus.LockScroll(module);
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

    public async ValueTask SetupFocusTrap(string elementId)
    {
        var module = await GetModuleAsync();
        await _focus.SetupFocusTrap(module, elementId);
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

    // --- Floating Position ---

    public async ValueTask PositionFixed(string contentId, string referenceId, string align = "start", bool matchWidth = false, string side = "bottom")
    {
        var module = await GetModuleAsync();
        await _floatingPosition.PositionFixed(module, contentId, referenceId, align, matchWidth, side);
    }

    public async ValueTask UnpositionFixed(string contentId)
    {
        var module = await GetModuleAsync();
        await _floatingPosition.UnpositionFixed(module, contentId);
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

    public async ValueTask UnregisterDrawerSwipe(string elementId)
    {
        var module = await GetModuleAsync();
        await _swipe.UnregisterDrawerSwipe(module, elementId);
    }

    [JSInvokable]
    public async Task OnSwipeDismiss(string elementId) => await _swipe.OnSwipeDismiss(elementId);

    // --- Carousel Swipe ---

    public async ValueTask RegisterCarouselSwipe(string elementId, string orientation, Func<string, Task> swipeHandler, Func<double, double, Task> scrollHandler)
    {
        var module = await GetModuleAsync();
        await _swipe.RegisterCarouselSwipe(module, GetSelfRef(), elementId, orientation, swipeHandler, scrollHandler);
    }

    public async ValueTask UnregisterCarouselSwipe(string elementId)
    {
        var module = await GetModuleAsync();
        await _swipe.UnregisterCarouselSwipe(module, elementId);
    }

    public async ValueTask CarouselScrollTo(string elementId, int index, string behavior = "smooth")
    {
        var module = await GetModuleAsync();
        await _swipe.CarouselScrollTo(module, elementId, index, behavior);
    }

    [JSInvokable]
    public async Task OnSwipe(string elementId, string direction) => await _swipe.OnSwipe(elementId, direction);

    [JSInvokable]
    public async Task OnScrollPosition(string elementId, double scrollPos, double maxScroll)
        => await _swipe.OnScrollPosition(elementId, scrollPos, maxScroll);

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
        await _scroll.ScrollspyScrollTo(module, containerId, sectionId, smooth);
    }

    [JSInvokable]
    public async Task OnScrollspyUpdate(string containerId, string? activeId)
        => await _scroll.OnScrollspyUpdate(containerId, activeId);

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

    // --- DataGrid Column Resize ---

    public async ValueTask RegisterColumnResize(string handleId, double minWidth, double? maxWidth, Func<double, Task> commitHandler)
    {
        var module = await GetModuleAsync();
        await _resize.RegisterColumnResize(module, GetSelfRef(), handleId, minWidth, maxWidth, commitHandler);
    }

    public async ValueTask UnregisterColumnResize(string handleId)
    {
        var module = await GetModuleAsync();
        await _resize.UnregisterColumnResize(module, handleId);
    }

    [JSInvokable]
    public async Task OnColumnResize(string handleId, double delta) => await _resize.OnColumnResize(handleId, delta);

    [JSInvokable]
    public async Task OnColumnResizeEnd(string handleId) => await _resize.OnColumnResizeEnd(handleId);

    [JSInvokable]
    public async Task OnColumnResizeCommit(string handleId, double finalWidth) => await _resize.OnColumnResizeCommit(handleId, finalWidth);

    // --- Tour: Element Rect By Selector ---

    public async ValueTask<ElementRect?> GetElementRectBySelector(string selector)
    {
        var module = await GetModuleAsync();
        return await _floatingPosition.GetElementRectBySelector(module, selector);
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

    public record TextareaCaretInfo(double Top, double Left, int SelectionStart);

    // --- BackToTop ---

    public async ValueTask RegisterBackToTop(string id, int threshold, Func<bool, Task> handler)
    {
        var module = await GetModuleAsync();
        await _scroll.RegisterBackToTop(module, GetSelfRef(), id, threshold, handler);
    }

    public async ValueTask UnregisterBackToTop(string id)
    {
        var module = await GetModuleAsync();
        await _scroll.UnregisterBackToTop(module, id);
    }

    public async ValueTask ScrollToTop()
    {
        var module = await GetModuleAsync();
        await _scroll.ScrollToTop(module);
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

    public async ValueTask MotionTickNumber(string elementId, double from, double to, int durationMs, int decimals, string separator = ",")
    {
        var module = await GetMotionModuleAsync();
        await module.InvokeVoidAsync("motion.tickNumber", elementId, from, to, durationMs, decimals, separator);
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

    public async ValueTask MotionRevealText(string elementId, int staggerMs, double threshold)
    {
        var module = await GetMotionModuleAsync();
        await module.InvokeVoidAsync("motion.revealText", elementId, new { stagger = staggerMs, threshold });
    }

    public async ValueTask MotionBlurFade(string elementId, int delayMs, bool once, bool forceHidden = false)
    {
        var module = await GetMotionModuleAsync();
        await module.InvokeVoidAsync("motion.blurFade", elementId, new { delayMs, once, forceHidden });
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

    public record TabMeasurement(double X, double Width);

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

    // --- Generic module import ---
    // Allows components that manage their own heavy JS modules (e.g. Chart with
    // echarts-interop.js) to import via the service rather than injecting IJSRuntime
    // directly in the component, satisfying the "no direct IJSRuntime in components" rule.

    public async ValueTask<IJSObjectReference> ImportModuleAsync(string moduleUrl)
        => await _jsRuntime.InvokeAsync<IJSObjectReference>("import", moduleUrl);

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

    // --- Rich Text Editor (TipTap) ---

    public async ValueTask<string> RichTextInitAsync<T>(
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
        _scroll.Clear();
        _utility.Clear();

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
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeJsRegistrationsAsync();
        _selfRef?.Dispose();
    }
}
