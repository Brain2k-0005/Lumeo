using Lumeo.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Lumeo.Tests.Helpers;

/// <summary>
/// A test-only IComponentInteropService implementation where every method is a
/// no-op except Vibrate(), RegisterTabSwipe(), RegisterHorizontalSwipe(), and
/// FocusElement() and the focus-trap pair (SetupFocusTrap/RemoveFocusTrap),
/// which record each call so tests can assert lifecycle behaviour.
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

    // Focus trap tracking (overlay lifecycle: Dialog / AlertDialog / Sheet / Drawer)
    private readonly List<(string ElementId, string? InitialFocusSelector)> _focusTrapSetups = new();
    private readonly List<string> _focusTrapRemovals = new();
    public IReadOnlyList<(string ElementId, string? InitialFocusSelector)> FocusTrapSetups => _focusTrapSetups;
    public IReadOnlyList<string> FocusTrapRemovals => _focusTrapRemovals;

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

    // Typeahead tracking (#222 DropdownMenu / #225 Menubar / #226 MegaMenu) —
    // records each (containerId, query, currentIndex) call. The returned matched
    // index is configurable so tests can drive the focus-follow assertion.
    private readonly List<(string ContainerId, string Query, int CurrentIndex)> _typeaheadCalls = new();
    public IReadOnlyList<(string ContainerId, string Query, int CurrentIndex)> TypeaheadCalls => _typeaheadCalls;
    /// <summary>Index returned by FocusMenuItemByTypeahead; defaults to -1 (no match).</summary>
    public int TypeaheadMatchIndex { get; set; } = -1;

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
    public ValueTask<int> FocusMenuItemByTypeahead(string containerId, string query, int currentIndex)
    {
        _typeaheadCalls.Add((containerId, query, currentIndex));
        return ValueTask.FromResult(TypeaheadMatchIndex);
    }
    public ValueTask LockScroll() => ValueTask.CompletedTask;
    public ValueTask UnlockScroll() => ValueTask.CompletedTask;

    // Records (className, active) for each SetHtmlClass call so tests can assert
    // html-class lifecycle (e.g. fullscreen-active added on enter / removed on
    // teardown).
    private readonly List<(string ClassName, bool Active)> _setHtmlClassCalls = new();
    public IReadOnlyList<(string ClassName, bool Active)> SetHtmlClassCalls => _setHtmlClassCalls;
    public ValueTask SetHtmlClass(string className, bool active)
    {
        _setHtmlClassCalls.Add((className, active));
        return ValueTask.CompletedTask;
    }
    public ValueTask SetupFocusTrap(string elementId, string? initialFocusSelector = null)
    {
        _focusTrapSetups.Add((elementId, initialFocusSelector));
        return ValueTask.CompletedTask;
    }
    public ValueTask RemoveFocusTrap(string elementId)
    {
        _focusTrapRemovals.Add(elementId);
        return ValueTask.CompletedTask;
    }
    public ValueTask AttachOverlaySlideEnd(string elementId) => ValueTask.CompletedTask;
    public ValueTask RegisterSvDrag(string elementId, Func<double, double, Task> handler) => ValueTask.CompletedTask;
    public ValueTask UnregisterSvDrag(string elementId) => ValueTask.CompletedTask;
    public ValueTask RegisterPinchZoom(string elementId, Func<double, Task> handler) => ValueTask.CompletedTask;
    public ValueTask UnregisterPinchZoom(string elementId) => ValueTask.CompletedTask;
    /// <summary>Viewport size returned by <see cref="GetViewportSize"/>; defaults to 0×0
    /// (no clamping) so existing tests are unaffected. Set it to exercise viewport
    /// clamping (e.g. Window resize/drag bounds).</summary>
    public ViewportSize ViewportSize { get; set; } = new(0, 0);
    public ValueTask<ViewportSize> GetViewportSize() => ValueTask.FromResult(ViewportSize);
    // 2.1.3: viewport listener no-ops (IResponsiveService is exercised separately via NoOpInterop)
    public ValueTask<ViewportSize?> RegisterViewportListener(DotNetObjectReference<ResponsiveService> dotnetRef) => ValueTask.FromResult<ViewportSize?>(new ViewportSize(0, 0));
    public ValueTask UnregisterViewportListener() => ValueTask.CompletedTask;
    public ValueTask PositionFixed(string contentId, string referenceId, string align = "start", bool matchWidth = false, string side = "bottom") => ValueTask.CompletedTask;
    public ValueTask UnpositionFixed(string contentId) => ValueTask.CompletedTask;

    // ContextMenu root-menu viewport clamp (#224) — opens at click coords with
    // no anchor, so it calls PositionAtPoint rather than PositionFixed.
    private readonly List<(string ContentId, double X, double Y)> _positionAtPointCalls = new();
    public IReadOnlyList<(string ContentId, double X, double Y)> PositionAtPointCalls => _positionAtPointCalls;
    public ValueTask PositionAtPoint(string contentId, double x, double y)
    {
        _positionAtPointCalls.Add((contentId, x, y));
        return ValueTask.CompletedTask;
    }

    // Toolbar roving focus (#235).
    private readonly List<string> _initToolbarRovingCalls = new();
    private readonly List<(string ToolbarId, int Delta)> _moveToolbarFocusCalls = new();
    private readonly List<(string ToolbarId, bool Last)> _focusToolbarEdgeCalls = new();
    public IReadOnlyList<string> InitToolbarRovingCalls => _initToolbarRovingCalls;
    public IReadOnlyList<(string ToolbarId, int Delta)> MoveToolbarFocusCalls => _moveToolbarFocusCalls;
    public IReadOnlyList<(string ToolbarId, bool Last)> FocusToolbarEdgeCalls => _focusToolbarEdgeCalls;
    public ValueTask InitToolbarRoving(string toolbarId)
    {
        _initToolbarRovingCalls.Add(toolbarId);
        return ValueTask.CompletedTask;
    }
    public ValueTask MoveToolbarFocus(string toolbarId, int delta)
    {
        _moveToolbarFocusCalls.Add((toolbarId, delta));
        return ValueTask.CompletedTask;
    }
    public ValueTask FocusToolbarEdge(string toolbarId, bool last)
    {
        _focusToolbarEdgeCalls.Add((toolbarId, last));
        return ValueTask.CompletedTask;
    }
    public ValueTask<ElementRect?> GetElementRect(string elementId) => ValueTask.FromResult<ElementRect?>(null);
    /// <summary>Value returned by <see cref="GetElementDimension"/> — defaults to
    /// 0 (so size-dependent paths short-circuit, preserving old behaviour). Set
    /// it to a positive pixel size to exercise Splitter/Resizable drag math.</summary>
    public double ElementDimension { get; set; }
    public ValueTask<double> GetElementDimension(string elementId, string dimension) => ValueTask.FromResult(ElementDimension);
    public ValueTask<double> GetScrollTop(string elementId) => ValueTask.FromResult(0.0);

    // PullToRefresh gesture-guard registration tracking (#308) — tests assert
    // the non-passive touchmove guard is wired on first render and torn down.
    private readonly List<string> _pullToRefreshRegistrations = new();
    private readonly List<string> _pullToRefreshUnregistrations = new();
    public IReadOnlyList<string> PullToRefreshRegistrations => _pullToRefreshRegistrations;
    public IReadOnlyList<string> PullToRefreshUnregistrations => _pullToRefreshUnregistrations;
    public ValueTask RegisterPullToRefresh(string elementId)
    {
        _pullToRefreshRegistrations.Add(elementId);
        return ValueTask.CompletedTask;
    }
    public ValueTask UnregisterPullToRefresh(string elementId)
    {
        _pullToRefreshUnregistrations.Add(elementId);
        return ValueTask.CompletedTask;
    }

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

    // Pointer capture tracking — used by Window drag/resize tests to assert the
    // capture-on-pointerdown / release-on-pointerup contract.
    private readonly List<(string ElementId, long PointerId)> _pointerCaptures = new();
    private readonly List<(string ElementId, long PointerId)> _pointerReleases = new();
    public IReadOnlyList<(string ElementId, long PointerId)> PointerCaptureCalls => _pointerCaptures;
    public IReadOnlyList<(string ElementId, long PointerId)> PointerReleaseCalls => _pointerReleases;

    public ValueTask SetPointerCaptureOnElement(string elementId, long pointerId)
    {
        _pointerCaptures.Add((elementId, pointerId));
        return ValueTask.CompletedTask;
    }
    public ValueTask ReleasePointerCaptureOnElement(string elementId, long pointerId)
    {
        _pointerReleases.Add((elementId, pointerId));
        return ValueTask.CompletedTask;
    }
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
    // Sortable touch tracking — used to assert SortableList (re-)registers its
    // touch handler against the CURRENT Disabled state (a list mounted Disabled
    // then enabled must still wire up touch) and drops it again when disabled.
    private readonly List<string> _sortableTouchRegistrations = new();
    private readonly List<string> _sortableTouchUnregistrations = new();
    public int RegisterSortableTouchCallCount => _sortableTouchRegistrations.Count;
    public IReadOnlyList<string> RegisterSortableTouchContainerIds => _sortableTouchRegistrations;
    public int UnregisterSortableTouchCallCount => _sortableTouchUnregistrations.Count;
    public IReadOnlyList<string> UnregisterSortableTouchContainerIds => _sortableTouchUnregistrations;

    public ValueTask RegisterSortableTouch(string containerId, Func<int, int, Task> handler)
    {
        _sortableTouchRegistrations.Add(containerId);
        return ValueTask.CompletedTask;
    }
    public ValueTask UnregisterSortableTouch(string containerId)
    {
        _sortableTouchUnregistrations.Add(containerId);
        return ValueTask.CompletedTask;
    }
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
    // Scrollspy tracking (#246) — assert click/programmatic scroll honours the
    // Offset, and the registered observer offset.
    private readonly List<(string ContainerId, int Offset, bool Smooth)> _scrollspyRegistrations = new();
    private readonly List<(string ContainerId, string SectionId, bool Smooth, int Offset)> _scrollspyScrollToCalls = new();
    public IReadOnlyList<(string ContainerId, int Offset, bool Smooth)> ScrollspyRegistrations => _scrollspyRegistrations;
    public IReadOnlyList<(string ContainerId, string SectionId, bool Smooth, int Offset)> ScrollspyScrollToCalls => _scrollspyScrollToCalls;
    public ValueTask RegisterScrollspy(string containerId, int offset, bool smooth, Func<string?, Task> handler)
    {
        _scrollspyRegistrations.Add((containerId, offset, smooth));
        return ValueTask.CompletedTask;
    }
    public ValueTask UnregisterScrollspy(string containerId) => ValueTask.CompletedTask;
    public ValueTask ScrollspyScrollTo(string containerId, string sectionId, bool smooth)
    {
        _scrollspyScrollToCalls.Add((containerId, sectionId, smooth, 0));
        return ValueTask.CompletedTask;
    }
    public ValueTask ScrollspyScrollTo(string containerId, string sectionId, bool smooth, int offset)
    {
        _scrollspyScrollToCalls.Add((containerId, sectionId, smooth, offset));
        return ValueTask.CompletedTask;
    }
    // Toast swipe tracking (#232) — the swipe-to-dismiss gesture is wired in
    // JS + service but was never registered by any component. Tests assert the
    // provider now registers per toast and can drive a dismissal via the
    // captured handler.
    private readonly List<(string ElementId, string ToastId, Func<string, Task> Handler)> _toastSwipeRegistrations = new();
    private readonly List<(string ToastId, string ElementId)> _toastSwipeUnregistrations = new();
    public IReadOnlyList<(string ElementId, string ToastId, Func<string, Task> Handler)> ToastSwipeRegistrations => _toastSwipeRegistrations;
    public IReadOnlyList<(string ToastId, string ElementId)> ToastSwipeUnregistrations => _toastSwipeUnregistrations;

    public ValueTask RegisterToastSwipe(string elementId, string toastId, Func<string, Task> handler)
    {
        _toastSwipeRegistrations.Add((elementId, toastId, handler));
        return ValueTask.CompletedTask;
    }
    public ValueTask UnregisterToastSwipe(string toastId, string elementId)
    {
        _toastSwipeUnregistrations.Add((toastId, elementId));
        return ValueTask.CompletedTask;
    }
    public ValueTask SetupAutoResize(string elementId, int maxRows) => ValueTask.CompletedTask;
    public ValueTask UnregisterAutoResize(string elementId) => ValueTask.CompletedTask;
    public ValueTask RegisterOtpPaste(string baseId, int length, Func<string, Task> handler) => ValueTask.CompletedTask;
    public ValueTask UnregisterOtpPaste(string baseId, int length) => ValueTask.CompletedTask;
    public ValueTask RegisterPreventDefaultKeys(string elementId, IReadOnlyList<PreventDefaultKeyRule> rules) => ValueTask.CompletedTask;
    public ValueTask UnregisterPreventDefaultKeys(string elementId) => ValueTask.CompletedTask;
    public ValueTask ScrollSelectorIntoView(string selector) => ValueTask.CompletedTask;

    // Scroll-into-view tracking — Command palette scrolls its active item into
    // view on keyboard navigation (#214); tests assert the call fires with the
    // highlighted item's id.
    private readonly List<(string ElementId, string Block)> _scrollIntoViewCalls = new();
    public IReadOnlyList<(string ElementId, string Block)> ScrollIntoViewCalls => _scrollIntoViewCalls;
    public ValueTask ScrollIntoView(string elementId, string block = "nearest")
    {
        _scrollIntoViewCalls.Add((elementId, block));
        return ValueTask.CompletedTask;
    }

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

    // Affix registration tracking (#248) — assert the sticky element is wired
    // with the consumer's offsets so the resize/rotate width-recompute path
    // stays reachable.
    private readonly List<(string ElementId, int OffsetTop, int? OffsetBottom, string? Target)> _affixRegistrations = new();
    private readonly List<string> _affixUnregistrations = new();
    public IReadOnlyList<(string ElementId, int OffsetTop, int? OffsetBottom, string? Target)> AffixRegistrations => _affixRegistrations;
    public IReadOnlyList<string> AffixUnregistrations => _affixUnregistrations;
    public ValueTask RegisterAffix(string elementId, int offsetTop, int? offsetBottom, string? target, Func<bool, Task> handler)
    {
        _affixRegistrations.Add((elementId, offsetTop, offsetBottom, target));
        return ValueTask.CompletedTask;
    }
    public ValueTask UnregisterAffix(string elementId)
    {
        _affixUnregistrations.Add(elementId);
        return ValueTask.CompletedTask;
    }
    public ValueTask<ComponentInteropService.TextareaCaretInfo> GetTextareaCaretPosition(string elementId) => ValueTask.FromResult(new ComponentInteropService.TextareaCaretInfo(0, 0, 0));

    // InputMask caret (#177). GetInputCaret returns InputCaret (default 0) so tests
    // can stage where the caret sits; SetInputCaret records each restore so tests
    // can assert the component repositions the caret after a masked edit.
    private readonly List<(string ElementId, int Position)> _setInputCaretCalls = new();
    public IReadOnlyList<(string ElementId, int Position)> SetInputCaretCalls => _setInputCaretCalls;
    /// <summary>Caret position returned by <see cref="GetInputCaret"/>; defaults to 0.</summary>
    public int InputCaret { get; set; }
    public ValueTask<int> GetInputCaret(string elementId) => ValueTask.FromResult(InputCaret);
    public ValueTask SetInputCaret(string elementId, int position)
    {
        _setInputCaretCalls.Add((elementId, position));
        return ValueTask.CompletedTask;
    }

    public ValueTask RegisterBackToTop(string id, int threshold, Func<bool, Task> handler) => ValueTask.CompletedTask;
    public ValueTask UnregisterBackToTop(string id) => ValueTask.CompletedTask;
    public ValueTask ScrollToTop() => ValueTask.CompletedTask;
    public ValueTask DownloadFile(string fileName, string contentBase64, string mimeType = "application/octet-stream") => ValueTask.CompletedTask;
    public ValueTask CopyToClipboard(string text) => ValueTask.CompletedTask;
    public ValueTask RippleAttachAsync(ElementReference element) => ValueTask.CompletedTask;
    public ValueTask RippleDetachAsync(ElementReference element) => ValueTask.CompletedTask;

    // Core reduced-motion gate (shares the PrefersReducedMotion flag with the
    // Motion override below) + TouchRipple coordinate resolution. TouchRipple
    // tests set TouchRippleCoordsResult to assert the ripple span uses the
    // host-relative coords the JS helper returns (nested-target fix #310).
    public ValueTask<bool> PrefersReducedMotion() => ValueTask.FromResult(ReducedMotion);
    public RipplePoint TouchRippleCoordsResult { get; set; } = new RipplePoint(0, 0);
    private readonly List<(string HostId, double X, double Y)> _touchRippleCoordsCalls = new();
    public IReadOnlyList<(string HostId, double X, double Y)> TouchRippleCoordsCalls => _touchRippleCoordsCalls;
    public ValueTask<RipplePoint> TouchRippleCoords(string hostElementId, double clientX, double clientY)
    {
        _touchRippleCoordsCalls.Add((hostElementId, clientX, clientY));
        return ValueTask.FromResult(TouchRippleCoordsResult);
    }
    public ValueTask SaveToLocalStorage(string key, string value) => ValueTask.CompletedTask;
    public ValueTask<string?> LoadFromLocalStorage(string key) => ValueTask.FromResult<string?>(null);
    public ValueTask RemoveFromLocalStorage(string key) => ValueTask.CompletedTask;
    // Reduced-motion gate (#310/#327/#328) — tests set ReducedMotion to
    // exercise the no-op / instant-settle branch of JS-driven motion primitives.
    // Shared by both the Motion and the core reduced-motion queries.
    public bool ReducedMotion { get; set; }
    private int _prefersReducedMotionCallCount;
    public int MotionPrefersReducedMotionCallCount => _prefersReducedMotionCallCount;
    public ValueTask<bool> MotionPrefersReducedMotion()
    {
        _prefersReducedMotionCallCount++;
        return ValueTask.FromResult(ReducedMotion);
    }
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
    public ValueTask SetPlaybackRate(Microsoft.AspNetCore.Components.ElementReference element, double rate) => ValueTask.CompletedTask;
    public ValueTask<Lumeo.Services.MediaState> GetMediaState(Microsoft.AspNetCore.Components.ElementReference element) => ValueTask.FromResult(new Lumeo.Services.MediaState(0, 0));
    public ValueTask SignaturePadInit(string elementId, object options, Microsoft.JSInterop.DotNetObjectReference<Lumeo.SignaturePad> dotNetRef) => ValueTask.CompletedTask;
    public ValueTask SignaturePadClear(string elementId) => ValueTask.CompletedTask;
    public ValueTask<string?> SignaturePadDataUrl(string elementId, string mimeType) => ValueTask.FromResult<string?>(null);
    public ValueTask SignaturePadSetStrokeStyle(string elementId, string color, double width) => ValueTask.CompletedTask;
    public ValueTask SignaturePadSetDisabled(string elementId, bool disabled) => ValueTask.CompletedTask;
    public ValueTask SignaturePadLoadDataUrl(string elementId, string? dataUrl) => ValueTask.CompletedTask;
    public ValueTask SignaturePadDestroy(string elementId) => ValueTask.CompletedTask;
}
