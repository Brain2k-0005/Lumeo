using Lumeo.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Lumeo.Tests.Helpers;

/// <summary>
/// A test-only IComponentInteropService implementation where every method is a
/// no-op except Vibrate(), RegisterTabSwipe(), RegisterHorizontalSwipe(), and
/// FocusElement(), which record each call so tests can assert lifecycle behaviour.
/// </summary>
public sealed class TrackingInteropService : IComponentInteropService
{
    private readonly List<int> _vibrateArgs = new();
    private readonly List<string> _focusedElementIds = new();
    private readonly List<string> _tabSwipeRegistrations = new();
    private readonly List<string> _tabSwipeUnregistrations = new();
    private readonly List<string> _horizontalSwipeRegistrations = new();
    private readonly List<string> _horizontalSwipeUnregistrations = new();

    public int VibrateCallCount => _vibrateArgs.Count;
    public IReadOnlyList<int> VibrateArgs => _vibrateArgs;

    // Tab swipe tracking
    public int RegisterTabSwipeCallCount => _tabSwipeRegistrations.Count;
    public IReadOnlyList<string> RegisterTabSwipeElementIds => _tabSwipeRegistrations;
    public int UnregisterTabSwipeCallCount => _tabSwipeUnregistrations.Count;

    // Calendar horizontal swipe tracking
    public int RegisterHorizontalSwipeCallCount => _horizontalSwipeRegistrations.Count;
    public IReadOnlyList<string> RegisterHorizontalSwipeElementIds => _horizontalSwipeRegistrations;
    public int UnregisterHorizontalSwipeCallCount => _horizontalSwipeUnregistrations.Count;

    // Focus tracking (e.g. TreeView roving-tabindex keyboard navigation)
    public IReadOnlyList<string> FocusedElementIds => _focusedElementIds;

    public ValueTask Vibrate(int milliseconds)
    {
        _vibrateArgs.Add(milliseconds);
        return ValueTask.CompletedTask;
    }

    // Menu family tracking — click-outside registrations (with their trigger
    // exclusion), focus calls and item-nav calls, so tests can assert the
    // overlay/keyboard wiring without a real DOM.
    private readonly List<(string ElementId, string? TriggerElementId, Func<Task> Handler)> _clickOutsideRegistrations = new();
    private readonly List<string> _clickOutsideUnregistrations = new();
    private readonly List<string> _focusElementCalls = new();
    private readonly List<(string ContainerId, int Index)> _focusMenuItemCalls = new();

    public IReadOnlyList<(string ElementId, string? TriggerElementId, Func<Task> Handler)> ClickOutsideRegistrations => _clickOutsideRegistrations;
    public IReadOnlyList<string> ClickOutsideUnregistrations => _clickOutsideUnregistrations;
    public IReadOnlyList<string> FocusElementCalls => _focusElementCalls;
    public IReadOnlyList<(string ContainerId, int Index)> FocusMenuItemCalls => _focusMenuItemCalls;

    /// <summary>Value returned by GetMenuItemCount; defaults to 0 (= no-op nav).</summary>
    public int MenuItemCount { get; set; }

    // ---- All remaining members are silent no-ops ----

    public ValueTask RegisterClickOutside(string elementId, string? triggerElementId, Func<Task> handler)
    {
        _clickOutsideRegistrations.Add((elementId, triggerElementId, handler));
        return ValueTask.CompletedTask;
    }
    public ValueTask UnregisterClickOutside(string elementId)
    {
        _clickOutsideUnregistrations.Add(elementId);
        return ValueTask.CompletedTask;
    }
    public ValueTask FocusElement(string elementId)
    {
        // Recorded in both views: menu tests assert via FocusElementCalls,
        // TreeView roving-tabindex tests via FocusedElementIds.
        _focusElementCalls.Add(elementId);
        _focusedElementIds.Add(elementId);
        return ValueTask.CompletedTask;
    }
    public ValueTask FocusMenuItemByIndex(string containerId, int index)
    {
        _focusMenuItemCalls.Add((containerId, index));
        return ValueTask.CompletedTask;
    }
    public ValueTask<int> GetMenuItemCount(string containerId) => ValueTask.FromResult(MenuItemCount);
    public ValueTask LockScroll() => ValueTask.CompletedTask;
    public ValueTask UnlockScroll() => ValueTask.CompletedTask;
    public ValueTask SetHtmlClass(string className, bool active) => ValueTask.CompletedTask;
    public ValueTask SetupFocusTrap(string elementId) => ValueTask.CompletedTask;
    public ValueTask RemoveFocusTrap(string elementId) => ValueTask.CompletedTask;
    public ValueTask AttachOverlaySlideEnd(string elementId) => ValueTask.CompletedTask;
    public ValueTask RegisterSvDrag(string elementId, Func<double, double, Task> handler) => ValueTask.CompletedTask;
    public ValueTask UnregisterSvDrag(string elementId) => ValueTask.CompletedTask;
    public ValueTask RegisterPinchZoom(string elementId, Func<double, Task> handler) => ValueTask.CompletedTask;
    public ValueTask UnregisterPinchZoom(string elementId) => ValueTask.CompletedTask;
    public ValueTask<ViewportSize> GetViewportSize() => ValueTask.FromResult(new ViewportSize(0, 0));
    // 2.1.3: viewport listener no-ops (IResponsiveService is exercised separately via NoOpInterop)
    public ValueTask<ViewportSize?> RegisterViewportListener(DotNetObjectReference<ResponsiveService> dotnetRef) => ValueTask.FromResult<ViewportSize?>(new ViewportSize(0, 0));
    public ValueTask UnregisterViewportListener() => ValueTask.CompletedTask;
    public ValueTask PositionFixed(string contentId, string referenceId, string align = "start", bool matchWidth = false, string side = "bottom") => ValueTask.CompletedTask;
    public ValueTask UnpositionFixed(string contentId) => ValueTask.CompletedTask;
    public ValueTask<ElementRect?> GetElementRect(string elementId) => ValueTask.FromResult<ElementRect?>(null);
    public ValueTask<double> GetElementDimension(string elementId, string dimension) => ValueTask.FromResult(0.0);
    public ValueTask<double> GetScrollTop(string elementId) => ValueTask.FromResult(0.0);
    public ValueTask<double> WheelScrollTop(ElementReference element) => ValueTask.FromResult(0.0);

    // Wheel-picker scroll tracking — used to assert that DateWheelPicker /
    // TimeWheelPicker re-position their columns when the bound value changes
    // externally (not just on first render).
    private readonly List<double> _wheelScrollToCalls = new();
    public int WheelScrollToCallCount => _wheelScrollToCalls.Count;
    public IReadOnlyList<double> WheelScrollToTops => _wheelScrollToCalls;
    public ValueTask WheelScrollTo(ElementReference element, double top)
    {
        _wheelScrollToCalls.Add(top);
        return ValueTask.CompletedTask;
    }
    public ValueTask SetPointerCaptureOnElement(string elementId, long pointerId) => ValueTask.CompletedTask;
    public ValueTask ReleasePointerCaptureOnElement(string elementId, long pointerId) => ValueTask.CompletedTask;
    public ValueTask RegisterDrawerSwipe(string elementId, string direction, Func<Task> handler) => ValueTask.CompletedTask;
    public ValueTask RegisterDrawerSwipe(string elementId, Func<Task> handler) => ValueTask.CompletedTask;
    public ValueTask UnregisterDrawerSwipe(string elementId) => ValueTask.CompletedTask;
    public ValueTask RegisterTabSwipe(string elementId, bool wrap, Func<string, Task> handler)
    {
        _tabSwipeRegistrations.Add(elementId);
        return ValueTask.CompletedTask;
    }
    public ValueTask UnregisterTabSwipe(string elementId)
    {
        _tabSwipeUnregistrations.Add(elementId);
        return ValueTask.CompletedTask;
    }
    public ValueTask RegisterSortableTouch(string containerId, Func<int, int, Task> handler) => ValueTask.CompletedTask;
    public ValueTask UnregisterSortableTouch(string containerId) => ValueTask.CompletedTask;
    public ValueTask RegisterCarouselSwipe(string elementId, string orientation, Func<string, Task> swipeHandler, Func<double, double, int, Task> scrollHandler) => ValueTask.CompletedTask;
    public ValueTask UnregisterCarouselSwipe(string elementId) => ValueTask.CompletedTask;
    public ValueTask CarouselScrollTo(string elementId, int index, string behavior = "smooth") => ValueTask.CompletedTask;
    public ValueTask RegisterHorizontalSwipe(string elementId, Func<string, Task> handler)
    {
        _horizontalSwipeRegistrations.Add(elementId);
        return ValueTask.CompletedTask;
    }
    public ValueTask UnregisterHorizontalSwipe(string elementId)
    {
        _horizontalSwipeUnregistrations.Add(elementId);
        return ValueTask.CompletedTask;
    }
    public ValueTask RegisterGallerySwipe(string elementId, Func<string, Task> handler) => ValueTask.CompletedTask;
    public ValueTask UnregisterGallerySwipe(string elementId) => ValueTask.CompletedTask;
    public ValueTask RegisterResizeHandle(string elementId, string direction, Func<double, Task> resizeHandler, Func<Task> resizeEndHandler) => ValueTask.CompletedTask;
    public ValueTask UnregisterResizeHandle(string elementId) => ValueTask.CompletedTask;
    public ValueTask RegisterScrollspy(string containerId, int offset, bool smooth, Func<string?, Task> handler) => ValueTask.CompletedTask;
    public ValueTask UnregisterScrollspy(string containerId) => ValueTask.CompletedTask;
    public ValueTask ScrollspyScrollTo(string containerId, string sectionId, bool smooth) => ValueTask.CompletedTask;
    public ValueTask RegisterToastSwipe(string elementId, string toastId, Func<string, Task> handler) => ValueTask.CompletedTask;
    public ValueTask UnregisterToastSwipe(string toastId, string elementId) => ValueTask.CompletedTask;
    public ValueTask SetupAutoResize(string elementId, int maxRows) => ValueTask.CompletedTask;
    public ValueTask UnregisterAutoResize(string elementId) => ValueTask.CompletedTask;
    public ValueTask RegisterOtpPaste(string baseId, int length, Func<string, Task> handler) => ValueTask.CompletedTask;
    public ValueTask UnregisterOtpPaste(string baseId, int length) => ValueTask.CompletedTask;
    public ValueTask RegisterPreventDefaultKeys(string elementId, IReadOnlyList<PreventDefaultKeyRule> rules) => ValueTask.CompletedTask;
    public ValueTask UnregisterPreventDefaultKeys(string elementId) => ValueTask.CompletedTask;
    public ValueTask ScrollSelectorIntoView(string selector) => ValueTask.CompletedTask;
    // Column resize tracking — used to assert that the JS pointerdown listener
    // is wired during the first render, not lazily on the first pointerdown
    // (the lazy path lost the originating event so the first drag was a no-op).
    private readonly List<string> _columnResizeRegistrations = new();
    private readonly List<string> _columnResizeUnregistrations = new();
    public int RegisterColumnResizeCallCount => _columnResizeRegistrations.Count;
    public IReadOnlyList<string> RegisterColumnResizeHandleIds => _columnResizeRegistrations;
    public int UnregisterColumnResizeCallCount => _columnResizeUnregistrations.Count;

    public ValueTask RegisterColumnResize(string handleId, double minWidth, double? maxWidth, Func<double, Task> commitHandler)
    {
        _columnResizeRegistrations.Add(handleId);
        return ValueTask.CompletedTask;
    }
    public ValueTask UnregisterColumnResize(string handleId)
    {
        _columnResizeUnregistrations.Add(handleId);
        return ValueTask.CompletedTask;
    }

    private readonly List<string> _captureColumnRectsCalls = new();
    private readonly List<(string gridId, int durationMs)> _animateColumnReorderCalls = new();
    public IReadOnlyList<string> CaptureColumnRectsGridIds => _captureColumnRectsCalls;
    public IReadOnlyList<(string gridId, int durationMs)> AnimateColumnReorderCalls => _animateColumnReorderCalls;
    public ValueTask CaptureColumnRects(string gridId)
    {
        _captureColumnRectsCalls.Add(gridId);
        return ValueTask.CompletedTask;
    }
    public ValueTask AnimateColumnReorder(string gridId, int durationMs)
    {
        _animateColumnReorderCalls.Add((gridId, durationMs));
        return ValueTask.CompletedTask;
    }

    public ValueTask<ElementRect?> GetElementRectBySelector(string selector) => ValueTask.FromResult<ElementRect?>(null);
    public ValueTask RegisterAffix(string elementId, int offsetTop, int? offsetBottom, string? target, Func<bool, Task> handler) => ValueTask.CompletedTask;
    public ValueTask UnregisterAffix(string elementId) => ValueTask.CompletedTask;
    public ValueTask<ComponentInteropService.TextareaCaretInfo> GetTextareaCaretPosition(string elementId) => ValueTask.FromResult(new ComponentInteropService.TextareaCaretInfo(0, 0, 0));
    public ValueTask RegisterBackToTop(string id, int threshold, Func<bool, Task> handler) => ValueTask.CompletedTask;
    public ValueTask UnregisterBackToTop(string id) => ValueTask.CompletedTask;
    public ValueTask ScrollToTop() => ValueTask.CompletedTask;
    public ValueTask DownloadFile(string fileName, string contentBase64, string mimeType = "application/octet-stream") => ValueTask.CompletedTask;
    public ValueTask CopyToClipboard(string text) => ValueTask.CompletedTask;
    public ValueTask RippleAttachAsync(ElementReference element) => ValueTask.CompletedTask;
    public ValueTask RippleDetachAsync(ElementReference element) => ValueTask.CompletedTask;
    public ValueTask SaveToLocalStorage(string key, string value) => ValueTask.CompletedTask;
    public ValueTask<string?> LoadFromLocalStorage(string key) => ValueTask.FromResult<string?>(null);
    public ValueTask RemoveFromLocalStorage(string key) => ValueTask.CompletedTask;
    public ValueTask MotionTickNumber(string elementId, double from, double to, int durationMs, int decimals, string separator = ",") => ValueTask.CompletedTask;
    public ValueTask MotionDisposeTicker(string elementId) => ValueTask.CompletedTask;
    public ValueTask MotionRevealText(string elementId, int staggerMs, double threshold) => ValueTask.CompletedTask;
    public ValueTask MotionBlurFade(string elementId, int delayMs, bool once, bool forceHidden = false) => ValueTask.CompletedTask;
    public ValueTask MotionDisposeObserver(string elementId) => ValueTask.CompletedTask;
    public ValueTask MotionAnimatedBeam(string elementId, string fromId, string toId, object options) => ValueTask.CompletedTask;
    public ValueTask MotionDisposeAnimatedBeam(string elementId) => ValueTask.CompletedTask;
    public ValueTask MotionDock(string elementId, object options) => ValueTask.CompletedTask;
    public ValueTask MotionDisposeDock(string elementId) => ValueTask.CompletedTask;
    public ValueTask MotionConfettiInit(string elementId) => ValueTask.CompletedTask;
    public ValueTask MotionConfettiFire(string elementId, object options) => ValueTask.CompletedTask;
    public ValueTask MotionDisposeConfetti(string elementId) => ValueTask.CompletedTask;
    public ValueTask AiAutosize(string elementId, int maxPx) => ValueTask.CompletedTask;
    public ValueTask AiObserveAutoScroll(string elementId) => ValueTask.CompletedTask;
    public ValueTask AiDisposeAutoScroll(string elementId) => ValueTask.CompletedTask;
    public ValueTask AiScrollToBottom(string elementId) => ValueTask.CompletedTask;

    public Task<string> SchedulerInitAsync(ElementReference el, object dotNetRef, object options) => Task.FromResult(string.Empty);
    public Task SchedulerSetEventsAsync(string id, IEnumerable<object> events) => Task.CompletedTask;
    public Task SchedulerChangeViewAsync(string id, string view) => Task.CompletedTask;
    public Task SchedulerGotoDateAsync(string id, string dateIso) => Task.CompletedTask;
    public Task SchedulerPrevAsync(string id) => Task.CompletedTask;
    public Task SchedulerNextAsync(string id) => Task.CompletedTask;
    public Task SchedulerTodayAsync(string id) => Task.CompletedTask;
    public Task<string> SchedulerGetTitleAsync(string id) => Task.FromResult(string.Empty);
    public Task SchedulerDestroyAsync(string id) => Task.CompletedTask;

    public Task<string> GanttInitAsync(ElementReference el, object dotNetRef, object options) => Task.FromResult(string.Empty);
    public Task GanttSetTasksAsync(string id, IEnumerable<object> tasks) => Task.CompletedTask;
    public Task GanttChangeViewModeAsync(string id, string mode) => Task.CompletedTask;
    public Task GanttDestroyAsync(string id) => Task.CompletedTask;

    public ValueTask<string> RichTextInitAsync<T>(ElementReference elementRef, DotNetObjectReference<T> dotNetRef, object options) where T : class => ValueTask.FromResult(string.Empty);
    public ValueTask RichTextSetContentAsync(string id, string? html) => ValueTask.CompletedTask;
    public ValueTask RichTextCommandAsync(string id, string name, params object?[]? args) => ValueTask.CompletedTask;
    public ValueTask<Lumeo.Services.Interop.RichTextActiveState?> RichTextGetActiveAsync(string id) => ValueTask.FromResult<Lumeo.Services.Interop.RichTextActiveState?>(null);
    public ValueTask RichTextSetDisabledAsync(string id, bool disabled) => ValueTask.CompletedTask;
    public ValueTask RichTextDestroyAsync(string id) => ValueTask.CompletedTask;
    public ValueTask<string?> RichTextPromptLinkAsync(string? initial) => ValueTask.FromResult<string?>(null);

    public ValueTask<Lumeo.Services.ComponentInteropService.TabMeasurement?> TabsMeasure(string elementId)
        => ValueTask.FromResult<Lumeo.Services.ComponentInteropService.TabMeasurement?>(null);
    public ValueTask RegisterToolbarOverflow(string elementId, Func<int, int, Task> handler) => ValueTask.CompletedTask;
    public ValueTask UnregisterToolbarOverflow(string elementId) => ValueTask.CompletedTask;

    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public ValueTask PlayMedia(Microsoft.AspNetCore.Components.ElementReference element) => ValueTask.CompletedTask;
    public ValueTask PauseMedia(Microsoft.AspNetCore.Components.ElementReference element) => ValueTask.CompletedTask;
    public ValueTask SetMediaVolume(Microsoft.AspNetCore.Components.ElementReference element, double volume, bool muted) => ValueTask.CompletedTask;
    public ValueTask SeekMedia(Microsoft.AspNetCore.Components.ElementReference element, double seconds) => ValueTask.CompletedTask;
    public ValueTask<Lumeo.Services.MediaState> GetMediaState(Microsoft.AspNetCore.Components.ElementReference element) => ValueTask.FromResult(new Lumeo.Services.MediaState(0, 0));
    public ValueTask SignaturePadInit(string elementId, object options, Microsoft.JSInterop.DotNetObjectReference<Lumeo.SignaturePad> dotNetRef) => ValueTask.CompletedTask;
    public ValueTask SignaturePadClear(string elementId) => ValueTask.CompletedTask;
    public ValueTask<string?> SignaturePadDataUrl(string elementId, string mimeType) => ValueTask.FromResult<string?>(null);
    public ValueTask SignaturePadSetStrokeStyle(string elementId, string color, double width) => ValueTask.CompletedTask;
    public ValueTask SignaturePadSetDisabled(string elementId, bool disabled) => ValueTask.CompletedTask;
    public ValueTask SignaturePadLoadDataUrl(string elementId, string? dataUrl) => ValueTask.CompletedTask;
    public ValueTask SignaturePadDestroy(string elementId) => ValueTask.CompletedTask;
}
