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
// Not sealed: OverlayExitAnimationRaceTests derives a variant whose open-interop
// chain blocks (LockScroll/UnlockScroll are virtual) to reproduce the Server
// "dismiss during open interop" race deterministically.
public class TrackingInteropService : IComponentInteropService
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

    /// <summary>Per-container DOM order returned by <see cref="GetOrderedDescendantIds"/>.
    /// Empty by default so the component falls back to its own registry order (existing
    /// tests are unaffected); a reorder-class test seeds an entry keyed by the container
    /// id with the ids in a DESIRED (e.g. reordered) order to drive the nav-follows-DOM
    /// assertion WITHOUT bUnit having to physically reorder reused child instances.</summary>
    public Dictionary<string, string[]> OrderedDescendantIds { get; } = new();
    private readonly List<(string ContainerId, string Selector)> _orderedDescendantCalls = new();
    public IReadOnlyList<(string ContainerId, string Selector)> OrderedDescendantCalls => _orderedDescendantCalls;

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
    public ValueTask<string[]> GetOrderedDescendantIds(string containerId, string selector)
    {
        _orderedDescendantCalls.Add((containerId, selector));
        return ValueTask.FromResult(OrderedDescendantIds.TryGetValue(containerId, out var ids) ? ids : System.Array.Empty<string>());
    }
    public ValueTask<int> FocusMenuItemByTypeahead(string containerId, string query, int currentIndex)
    {
        _typeaheadCalls.Add((containerId, query, currentIndex));
        return ValueTask.FromResult(TypeaheadMatchIndex);
    }
    // virtual so a test subclass can make the open-interop chain block (return an
    // incomplete task) to reproduce the Blazor-Server "dismiss lands while the open
    // interop is still in flight" race deterministically — see
    // OverlayExitAnimationRaceTests (B11: exit animation must not depend on the open
    // interop having completed).
    public virtual ValueTask LockScroll() => ValueTask.CompletedTask;
    public virtual ValueTask UnlockScroll() => ValueTask.CompletedTask;

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
    public ValueTask SaveFocus(string key) => ValueTask.CompletedTask;
    private readonly List<string> _restoreFocusKeys = new();
    public int RestoreFocusCallCount => _restoreFocusKeys.Count;
    public IReadOnlyList<string> RestoreFocusKeys => _restoreFocusKeys;
    public ValueTask RestoreFocus(string key)
    {
        _restoreFocusKeys.Add(key);
        return ValueTask.CompletedTask;
    }
    // virtual so a test subclass can observe the CALLER's rendered DOM state at the
    // exact moment this fires (ConsentBannerSlideEndTests' non-tautological
    // "wired after the element is actually mounted" regression coverage).
    public virtual ValueTask AttachOverlaySlideEnd(string elementId) => ValueTask.CompletedTask;

    // Overlay EXIT wiring capture (B11 Radix-Presence parity). Records each
    // wire-up and the last captured callback so a test can SIMULATE the JS
    // animationend by invoking OnExitAnimationEnd — driving the content's
    // animationend-based unmount without a real browser. Does NOT auto-fire, so
    // tests that don't invoke it exercise the fallback-timer path instead.
    private readonly List<string> _overlayExitEndWirings = new();
    public IReadOnlyList<string> OverlayExitEndWirings => _overlayExitEndWirings;
    public global::Lumeo.IOverlayExitCallback? LastOverlayExitCallback { get; private set; }
    public virtual ValueTask AttachOverlayExitEnd<T>(string elementId, DotNetObjectReference<T> dotNetRef) where T : class
    {
        _overlayExitEndWirings.Add(elementId);
        LastOverlayExitCallback = dotNetRef.Value as global::Lumeo.IOverlayExitCallback;
        return ValueTask.CompletedTask;
    }

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
    // No JS in tests — echo the requested side back (no collision flip), matching the live fallback.
    public ValueTask<string> PositionFixed(string contentId, string referenceId, string align = "start", bool matchWidth = false, string side = "bottom") => ValueTask.FromResult(side);
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
    // Drawer/Sheet swipe-to-dismiss tracking — records each (elementId, direction)
    // registration and each unregistration so tests can assert that a swipe
    // eligibility/direction toggle WHILE the overlay stays open re-wires the
    // gesture (Sheet #89), not just on close+reopen.
    private readonly List<(string ElementId, string? Direction)> _drawerSwipeRegistrations = new();
    private readonly List<string> _drawerSwipeUnregistrations = new();
    public IReadOnlyList<(string ElementId, string? Direction)> DrawerSwipeRegistrations => _drawerSwipeRegistrations;
    public IReadOnlyList<string> DrawerSwipeUnregistrations => _drawerSwipeUnregistrations;
    public ValueTask RegisterDrawerSwipe(string elementId, string direction, Func<Task> handler)
    {
        _drawerSwipeRegistrations.Add((elementId, direction));
        return ValueTask.CompletedTask;
    }
    public ValueTask RegisterDrawerSwipe(string elementId, Func<Task> handler)
    {
        _drawerSwipeRegistrations.Add((elementId, null));
        return ValueTask.CompletedTask;
    }
    public ValueTask UnregisterDrawerSwipe(string elementId)
    {
        _drawerSwipeUnregistrations.Add(elementId);
        return ValueTask.CompletedTask;
    }
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

    // Tabs overflow scroll arrows (#239) — capture the observer handler so tests
    // can drive arrow visibility, and record TabsScrollBy nudges.
    private readonly Dictionary<string, Func<bool, bool, Task>> _tabsOverflowHandlers = new();
    private readonly List<(string ListId, double Delta, bool Horizontal)> _tabsScrollByCalls = new();
    public IReadOnlyCollection<string> TabsOverflowRegistrations => _tabsOverflowHandlers.Keys.ToList();
    public IReadOnlyList<(string ListId, double Delta, bool Horizontal)> TabsScrollByCalls => _tabsScrollByCalls;
    /// <summary>Simulates a JS overflow report for the given tablist.</summary>
    public Task RaiseTabsOverflow(string listId, bool canScrollStart, bool canScrollEnd) =>
        _tabsOverflowHandlers.TryGetValue(listId, out var h) ? h(canScrollStart, canScrollEnd) : Task.CompletedTask;
    public ValueTask RegisterTabsOverflow(string listId, Func<bool, bool, Task> handler)
    {
        _tabsOverflowHandlers[listId] = handler;
        return ValueTask.CompletedTask;
    }
    public ValueTask UnregisterTabsOverflow(string listId)
    {
        _tabsOverflowHandlers.Remove(listId);
        return ValueTask.CompletedTask;
    }
    public ValueTask TabsScrollBy(string listId, double delta, bool horizontal)
    {
        _tabsScrollByCalls.Add((listId, delta, horizontal));
        return ValueTask.CompletedTask;
    }
    public ValueTask SetupAutoResize(string elementId, int maxRows) => ValueTask.CompletedTask;
    public ValueTask UnregisterAutoResize(string elementId) => ValueTask.CompletedTask;
    // OTP paste registration tracking (#42 lifecycle) — records each
    // (baseId, length) register/unregister so tests can assert the paste listener
    // is (re-)wired against the CURRENT Length: a runtime Length change must
    // unregister the old span and register the new one, and dispose must
    // unregister against the last-registered length. Set
    // ThrowObjectDisposedOnUnregisterOtpPaste to exercise the teardown
    // ObjectDisposedException-swallow path (#157).
    private readonly List<(string BaseId, int Length)> _otpPasteRegistrations = new();
    private readonly List<(string BaseId, int Length)> _otpPasteUnregistrations = new();
    public IReadOnlyList<(string BaseId, int Length)> OtpPasteRegistrations => _otpPasteRegistrations;
    public IReadOnlyList<(string BaseId, int Length)> OtpPasteUnregistrations => _otpPasteUnregistrations;
    /// <summary>When true, <see cref="UnregisterOtpPaste"/> throws
    /// <see cref="ObjectDisposedException"/> (simulating a circuit/prerender
    /// teardown race) so dispose tests can assert the component swallows it.</summary>
    public bool ThrowObjectDisposedOnUnregisterOtpPaste { get; set; }
    public ValueTask RegisterOtpPaste(string baseId, int length, Func<string, Task> handler)
    {
        _otpPasteRegistrations.Add((baseId, length));
        return ValueTask.CompletedTask;
    }
    public ValueTask UnregisterOtpPaste(string baseId, int length)
    {
        _otpPasteUnregistrations.Add((baseId, length));
        if (ThrowObjectDisposedOnUnregisterOtpPaste)
            throw new ObjectDisposedException(nameof(TrackingInteropService));
        return ValueTask.CompletedTask;
    }
    private readonly List<string> _registerPreventDefaultKeysElementIds = new();
    public IReadOnlyList<string> RegisterPreventDefaultKeysElementIds => _registerPreventDefaultKeysElementIds;
    // PR #356 round-3: also captures the rule SET per call (keyed by elementId, last
    // call wins) so tests can assert flags like SkipEditable on the registered rules,
    // not just that SOME registration happened against an id.
    private readonly Dictionary<string, IReadOnlyList<PreventDefaultKeyRule>> _registerPreventDefaultKeysRules = new();
    public IReadOnlyDictionary<string, IReadOnlyList<PreventDefaultKeyRule>> RegisterPreventDefaultKeysRules => _registerPreventDefaultKeysRules;
    public ValueTask RegisterPreventDefaultKeys(string elementId, IReadOnlyList<PreventDefaultKeyRule> rules)
    {
        _registerPreventDefaultKeysElementIds.Add(elementId);
        _registerPreventDefaultKeysRules[elementId] = rules;
        return ValueTask.CompletedTask;
    }
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

    // Last commit handler captured per handle so tests can simulate a JS-side
    // resize commit (drag/keyboard/auto-fit) and assert the width + persistence
    // path without needing a real browser pointer.
    private readonly Dictionary<string, Func<double, bool, Task>> _columnResizeCommitHandlers = new();
    public IReadOnlyDictionary<string, Func<double, bool, Task>> ColumnResizeCommitHandlers => _columnResizeCommitHandlers;
    /// <summary>Invoke the captured commit for a handle, mimicking JS on pointerup /
    /// dblclick. Returns false when no handler is registered for that id.</summary>
    public async Task<bool> SimulateColumnResizeCommit(string handleId, double finalWidth, bool autoFit)
    {
        if (!_columnResizeCommitHandlers.TryGetValue(handleId, out var handler)) return false;
        await handler(finalWidth, autoFit);
        return true;
    }

    // round-9 #4: the interface's original (non-autoFit) abstract member — forwards
    // to the autoFit-aware overload below with autoFit always false, mirroring
    // ComponentInteropService's own explicit implementation of this shape.
    public ValueTask RegisterColumnResize(string handleId, double minWidth, double? maxWidth, Func<double, Task> commitHandler) =>
        RegisterColumnResize(handleId, minWidth, maxWidth, (w, _) => commitHandler(w));

    public ValueTask RegisterColumnResize(string handleId, double minWidth, double? maxWidth, Func<double, bool, Task> commitHandler)
    {
        _columnResizeRegistrations.Add(handleId);
        _columnResizeCommitHandlers[handleId] = commitHandler;
        return ValueTask.CompletedTask;
    }
    public ValueTask UnregisterColumnResize(string handleId)
    {
        _columnResizeUnregistrations.Add(handleId);
        _columnResizeCommitHandlers.Remove(handleId);
        return ValueTask.CompletedTask;
    }

    private readonly List<(string HandleId, double Delta)> _nudgeColumnResizeCalls = new();
    public IReadOnlyList<(string HandleId, double Delta)> NudgeColumnResizeCalls => _nudgeColumnResizeCalls;
    public ValueTask NudgeColumnResize(string handleId, double delta)
    {
        _nudgeColumnResizeCalls.Add((handleId, delta));
        return ValueTask.CompletedTask;
    }

    // Pointer-based (touch/pen) column reorder registration + commit capture.
    private readonly List<string> _columnReorderRegistrations = new();
    private readonly List<string> _columnReorderUnregistrations = new();
    private readonly Dictionary<string, Func<string, string, Task>> _columnReorderCommitHandlers = new();
    public IReadOnlyList<string> ColumnReorderRegistrations => _columnReorderRegistrations;
    public IReadOnlyList<string> ColumnReorderUnregistrations => _columnReorderUnregistrations;
    /// <summary>Simulate a JS-side pointer-reorder commit (drop of source onto target).</summary>
    public async Task<bool> SimulateColumnReorderCommit(string gridId, string sourceColumnId, string targetColumnId)
    {
        if (!_columnReorderCommitHandlers.TryGetValue(gridId, out var handler)) return false;
        await handler(sourceColumnId, targetColumnId);
        return true;
    }
    public ValueTask RegisterColumnReorder(string gridId, Func<string, string, Task> commitHandler)
    {
        _columnReorderRegistrations.Add(gridId);
        _columnReorderCommitHandlers[gridId] = commitHandler;
        return ValueTask.CompletedTask;
    }
    public ValueTask UnregisterColumnReorder(string gridId)
    {
        _columnReorderUnregistrations.Add(gridId);
        _columnReorderCommitHandlers.Remove(gridId);
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

    private readonly List<string> _clearColumnReorderTransformsCalls = new();
    public IReadOnlyList<string> ClearColumnReorderTransformsGridIds => _clearColumnReorderTransformsCalls;
    public ValueTask ClearColumnReorderTransforms(string gridId)
    {
        _clearColumnReorderTransformsCalls.Add(gridId);
        return ValueTask.CompletedTask;
    }

    // Pointer-based (mouse/touch/pen) row reorder registration + commit capture —
    // vertical mirror of the column tracking block above. Keyed by stable row
    // identity (data-row-key), not plain index — see ReorderRowByKeyAsync.
    private readonly List<string> _rowReorderRegistrations = new();
    private readonly List<string> _rowReorderUnregistrations = new();
    private readonly Dictionary<string, Func<string, string, Task>> _rowReorderCommitHandlers = new();
    public IReadOnlyList<string> RowReorderRegistrations => _rowReorderRegistrations;
    public IReadOnlyList<string> RowReorderUnregistrations => _rowReorderUnregistrations;
    /// <summary>Simulate a JS-side pointer-reorder commit (drop of source row key onto target row key).</summary>
    public async Task<bool> SimulateRowReorderCommit(string gridId, string sourceRowKey, string targetRowKey)
    {
        if (!_rowReorderCommitHandlers.TryGetValue(gridId, out var handler)) return false;
        await handler(sourceRowKey, targetRowKey);
        return true;
    }
    public ValueTask RegisterRowReorder(string gridId, Func<string, string, Task> commitHandler)
    {
        _rowReorderRegistrations.Add(gridId);
        _rowReorderCommitHandlers[gridId] = commitHandler;
        return ValueTask.CompletedTask;
    }
    public ValueTask UnregisterRowReorder(string gridId)
    {
        _rowReorderUnregistrations.Add(gridId);
        _rowReorderCommitHandlers.Remove(gridId);
        return ValueTask.CompletedTask;
    }

    private readonly List<string> _captureRowRectsCalls = new();
    private readonly List<(string gridId, int durationMs)> _animateRowReorderCalls = new();
    public IReadOnlyList<string> CaptureRowRectsGridIds => _captureRowRectsCalls;
    public IReadOnlyList<(string gridId, int durationMs)> AnimateRowReorderCalls => _animateRowReorderCalls;
    public ValueTask CaptureRowRects(string gridId)
    {
        _captureRowRectsCalls.Add(gridId);
        return ValueTask.CompletedTask;
    }
    public ValueTask AnimateRowReorder(string gridId, int durationMs)
    {
        _animateRowReorderCalls.Add((gridId, durationMs));
        return ValueTask.CompletedTask;
    }

    private readonly List<string> _clearRowReorderTransformsCalls = new();
    public IReadOnlyList<string> ClearRowReorderTransformsGridIds => _clearRowReorderTransformsCalls;
    public ValueTask ClearRowReorderTransforms(string gridId)
    {
        _clearRowReorderTransformsCalls.Add(gridId);
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

    // InputMask forced value write (#41) — records each (elementId, value) so the
    // rejected-char test can assert the masked display was pushed to the DOM even
    // when it matched the previous render (no Blazor patch).
    private readonly List<(string ElementId, string Value)> _setInputValueCalls = new();
    public IReadOnlyList<(string ElementId, string Value)> SetInputValueCalls => _setInputValueCalls;
    public ValueTask SetInputValue(string elementId, string value)
    {
        _setInputValueCalls.Add((elementId, value));
        return ValueTask.CompletedTask;
    }

    public ValueTask RegisterBackToTop(string id, int threshold, Func<bool, Task> handler) => ValueTask.CompletedTask;
    public ValueTask UnregisterBackToTop(string id) => ValueTask.CompletedTask;
    public ValueTask ScrollToTop() => ValueTask.CompletedTask;
    public ValueTask DownloadFile(string fileName, string contentBase64, string mimeType = "application/octet-stream") => ValueTask.CompletedTask;

    // Clipboard tracking (#AgentMessageActions copy confirmation). Records each
    // copied text so the success path can be asserted; when
    // ThrowOnCopyToClipboard is set the call throws JSDisconnectedException
    // (simulating a dead Server circuit) so the "copy failed → no confirmation"
    // path is exercised.
    private readonly List<string> _copyToClipboardCalls = new();
    public IReadOnlyList<string> CopyToClipboardCalls => _copyToClipboardCalls;
    /// <summary>When true, <see cref="CopyToClipboard"/> throws
    /// <see cref="JSDisconnectedException"/> to simulate a disconnected circuit.</summary>
    public bool ThrowOnCopyToClipboard { get; set; }
    /// <summary>When set, <see cref="CopyToClipboard"/> throws this exception
    /// (checked before <see cref="ThrowOnCopyToClipboard"/>). Lets a test simulate
    /// the browser REJECTING the clipboard write — an insecure origin or a denied
    /// permission surfaces from the JS interop as a plain <see cref="JSException"/>,
    /// distinct from a circuit disconnect.</summary>
    public Exception? CopyToClipboardException { get; set; }
    public ValueTask CopyToClipboard(string text)
    {
        _copyToClipboardCalls.Add(text);
        if (CopyToClipboardException is not null)
            throw CopyToClipboardException;
        if (ThrowOnCopyToClipboard)
            throw new JSDisconnectedException("circuit disconnected");
        return ValueTask.CompletedTask;
    }
    // Ripple press-effect tracking (Button/Card/Chip/...). The JS pointerdown
    // listener attach/detach is the testable seam for the PressEffect
    // state-on-data-change reconciliation: a runtime None->Ripple flip must
    // attach, Ripple->None must detach, and dispose must detach if attached.
    private int _rippleAttachCount;
    private int _rippleDetachCount;
    public int RippleAttachCallCount => _rippleAttachCount;
    public int RippleDetachCallCount => _rippleDetachCount;
    public ValueTask RippleAttachAsync(ElementReference element)
    {
        _rippleAttachCount++;
        return ValueTask.CompletedTask;
    }
    public ValueTask RippleDetachAsync(ElementReference element)
    {
        _rippleDetachCount++;
        return ValueTask.CompletedTask;
    }

    // File input reset tracking (#70) — UploadTrigger resets its native
    // <input type=file> after every pick so re-selecting the SAME file fires
    // `change` again. Tests assert the reset fires once per picker confirmation.
    private int _resetFileInputCount;
    public int ResetFileInputCallCount => _resetFileInputCount;
    public ValueTask ResetFileInput(ElementReference element)
    {
        _resetFileInputCount++;
        return ValueTask.CompletedTask;
    }

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
    public ValueTask MotionTickNumber(string elementId, double from, double to, int durationMs, int decimals, string separator = ",", string decimalSeparator = ".") => ValueTask.CompletedTask;
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
    public ValueTask RichTextSetAriaAttributesAsync(string id, bool ariaInvalid, string? ariaDescribedBy) => ValueTask.CompletedTask;

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
    public ValueTask SignaturePadInit<T>(string elementId, object options, Microsoft.JSInterop.DotNetObjectReference<T> dotNetRef) where T : class => ValueTask.CompletedTask;
    public ValueTask SignaturePadClear(string elementId) => ValueTask.CompletedTask;
    public ValueTask<string?> SignaturePadDataUrl(string elementId, string mimeType) => ValueTask.FromResult<string?>(null);
    public ValueTask SignaturePadSetStrokeStyle(string elementId, string color, double width) => ValueTask.CompletedTask;
    public ValueTask SignaturePadSetDisabled(string elementId, bool disabled) => ValueTask.CompletedTask;

    // SignaturePad surface repaint tracking (#120) — a runtime Width/Height
    // change blanks the canvas buffer, so the component must re-issue
    // SignaturePadLoadDataUrl to repaint the existing signature. Tests assert
    // that repaint fires (with the live value) after a resize.
    private readonly List<(string ElementId, string? DataUrl)> _signaturePadLoadCalls = new();
    public IReadOnlyList<(string ElementId, string? DataUrl)> SignaturePadLoadCalls => _signaturePadLoadCalls;
    public ValueTask SignaturePadLoadDataUrl(string elementId, string? dataUrl)
    {
        _signaturePadLoadCalls.Add((elementId, dataUrl));
        return ValueTask.CompletedTask;
    }
    public ValueTask SignaturePadDestroy(string elementId) => ValueTask.CompletedTask;
}
