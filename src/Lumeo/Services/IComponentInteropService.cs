using Microsoft.JSInterop;

namespace Lumeo.Services;

/// <summary>
/// Provides JS interop helpers for Lumeo components (click-outside, focus traps,
/// floating positioning, swipe gestures, scroll utilities, etc.).
/// Inject this interface in consumers to enable mocking in tests.
/// </summary>
public interface IComponentInteropService : IAsyncDisposable, IDisposable
{
    // Click Outside
    ValueTask RegisterClickOutside(string elementId, string? triggerElementId, Func<Task> handler);
    ValueTask UnregisterClickOutside(string elementId);

    // Focus / Scroll Lock
    ValueTask FocusElement(string elementId);
    ValueTask FocusMenuItemByIndex(string containerId, int index);
    ValueTask<int> GetMenuItemCount(string containerId);

    /// <summary>
    /// Returns the <c>id</c> attributes of the descendants of <paramref name="containerId"/>
    /// matching <paramref name="selector"/>, in live DOM order (document order). Compound
    /// widgets whose children self-register (RadioGroup / ToggleGroup / Segmented / Stepper /
    /// Splitter, …) consult this at NAVIGATION time so roving / arrow-key / neighbour order
    /// tracks the real DOM even after a keyed reorder MOVES reused child instances without
    /// re-rendering them (so the C# mount-order registry has gone stale). Callers MUST treat
    /// an empty result as "DOM order unavailable" and fall back to their own registry order,
    /// so prerender / JS-unavailable / non-configured-test-double paths keep working. Default
    /// implementation returns an empty array so existing implementers / test doubles keep
    /// compiling and behave exactly as before (registry-order fallback).
    /// </summary>
    ValueTask<string[]> GetOrderedDescendantIds(string containerId, string selector)
        => ValueTask.FromResult(System.Array.Empty<string>());

    /// <summary>
    /// Type-to-focus (Radix menu typeahead). Focuses the first enabled menu item
    /// in <paramref name="containerId"/> whose text content starts with
    /// <paramref name="query"/> (case-insensitive), searching after
    /// <paramref name="currentIndex"/> first then wrapping, and returns its index
    /// (or <c>-1</c> when nothing matches). Shared by DropdownMenu / Menubar /
    /// MegaMenu via the <see cref="MenuTypeahead"/> buffer helper. Default
    /// implementation returns <c>-1</c> so existing implementers / test doubles
    /// keep compiling.
    /// </summary>
    ValueTask<int> FocusMenuItemByTypeahead(string containerId, string query, int currentIndex) =>
        ValueTask.FromResult(-1);
    ValueTask LockScroll();
    ValueTask UnlockScroll();
    /// <summary>Toggles a class on <c>document.documentElement</c>. Useful for
    /// global modes (e.g. hiding floating chrome while a DataGrid is fullscreen).</summary>
    ValueTask SetHtmlClass(string className, bool active);
    /// <summary>Engages a Tab-cycling focus trap on the element, saves the
    /// previously focused element (the trigger) and moves focus into the trap.
    /// <paramref name="initialFocusSelector"/> optionally names the element
    /// (resolved within the trap) to focus first — e.g. AlertDialog targets its
    /// least destructive action via <c>[data-lumeo-initial-focus]</c>; when
    /// null/unmatched, the first focusable element receives focus.</summary>
    ValueTask SetupFocusTrap(string elementId, string? initialFocusSelector = null);
    /// <summary>Releases the trap and returns focus to the element that was
    /// focused when <see cref="SetupFocusTrap"/> ran, if it is still in the
    /// document.</summary>
    ValueTask RemoveFocusTrap(string elementId);

    /// <summary>Saves the currently-focused element keyed by <paramref name="key"/>
    /// so <see cref="RestoreFocus"/> can hand focus back later. Unlike
    /// <see cref="SetupFocusTrap"/> this installs NO Tab trap — use it for non-modal
    /// surfaces (menus, listbox popovers) that move focus inward on open but must let
    /// Tab close them per the WAI-ARIA pattern.</summary>
    ValueTask SaveFocus(string key) => ValueTask.CompletedTask;
    /// <summary>Returns focus to the element saved by <see cref="SaveFocus"/> under
    /// the same key, if it is still in the document (WCAG 2.4.3).</summary>
    // Additive — default no-op so external/test implementations of IComponentInteropService keep
    // compiling; ComponentInteropService overrides both with the real JS focus save/restore.
    ValueTask RestoreFocus(string key) => ValueTask.CompletedTask;

    /// <summary>Registers a native animationend listener that filters strictly on
    /// the slide-in animation name and, on completion, sets the element's inline
    /// <c>transform: none</c>. Bypasses Blazor's event roundtrip so the cleanup
    /// runs synchronously from the browser's animation pipeline. Used by Sheet,
    /// Drawer and any future slide-in overlay to defeat the
    /// <c>animation-fill-mode: both</c> identity-matrix transform trap that
    /// would otherwise establish a containing block for fixed-positioned
    /// descendants (Select / DatePicker / Combobox popovers).</summary>
    ValueTask AttachOverlaySlideEnd(string elementId);

    /// <summary>
    /// Radix-Presence-style overlay EXIT: clears the open-time containing-block
    /// guard (the inline <c>animation:none</c>/<c>transform:none</c> that
    /// <see cref="AttachOverlaySlideEnd"/> stamped) so the panel's
    /// <c>animate-slide-out-*</c>/<c>animate-zoom-out</c> class can actually run,
    /// then invokes <c>OnExitAnimationEnd</c> on <paramref name="dotNetRef"/> once
    /// the panel's own exit animation finishes. Lets the overlay content drop
    /// backdrop + panel together on the REAL animation end instead of a blind
    /// timer (which slips late under main-thread load). The component keeps its
    /// timer strictly as a fallback. Default no-op so test doubles / prerender
    /// fall back to that timer.
    /// </summary>
    ValueTask AttachOverlayExitEnd<[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicMethods)] T>(string elementId, DotNetObjectReference<T> dotNetRef) where T : class
        => ValueTask.CompletedTask;

    // ColorPicker SV Drag
    ValueTask RegisterSvDrag(string elementId, Func<double, double, Task> handler);
    ValueTask UnregisterSvDrag(string elementId);

    // Pinch Zoom (two-finger gesture) — handler receives the scale delta per move event
    ValueTask RegisterPinchZoom(string elementId, Func<double, Task> handler);
    ValueTask UnregisterPinchZoom(string elementId);

    // Viewport
    ValueTask<ViewportSize> GetViewportSize();

    // Viewport listener — backs IResponsiveService (2.1.3). The returned
    // ViewportSize is the initial snapshot; subsequent changes are pushed
    // back to the registered ResponsiveService via [JSInvokable]
    // OnViewportChange. Returns null when no JS module is available (e.g.
    // bUnit loose-mode in tests) — callers must handle the null and keep
    // their default zero-state.
    ValueTask<ViewportSize?> RegisterViewportListener(Microsoft.JSInterop.DotNetObjectReference<ResponsiveService> dotnetRef);
    ValueTask UnregisterViewportListener();

    // Floating Position. Returns the side the content box ACTUALLY resolved to: a collision flip can move
    // a preferred-Top box below its trigger (etc.), so a directional-arrow consumer (Tooltip) reads this to
    // keep the arrow on the edge facing the trigger. Equals the requested side when no flip occurs.
    ValueTask<string> PositionFixed(string contentId, string referenceId, string align = "start", bool matchWidth = false, string side = "bottom");
    /// <summary>
    /// 3.12.x — extended overload with an explicit trigger→content gap in pixels
    /// (Tooltip <c>Offset</c>). The default implementation ignores the offset and
    /// falls back to the 5-arg overload (JS hardcoded 4px) so existing
    /// implementations keep compiling unchanged. Returns the resolved side (see above).
    /// </summary>
    ValueTask<string> PositionFixed(string contentId, string referenceId, string align, bool matchWidth, string side, int offset) =>
        PositionFixed(contentId, referenceId, align, matchWidth, side);
    /// <summary>
    /// round-14 — extended overload that ALSO reports LIVE collision flips: the synchronous return only
    /// covers the initial placement, but a later scroll/resize reposition can flip the box to a different
    /// side while the tooltip stays open, and a directional-arrow consumer needs to follow that too
    /// (Codex P2). <paramref name="onSideChanged"/> is invoked with the newly-resolved side whenever a
    /// LATER reposition changes it (never for the initial placement — that's the synchronous return).
    /// The default implementation ignores the callback and behaves like the 6-arg overload — only
    /// <c>ComponentInteropService</c> overrides this with real live notification; test doubles don't run
    /// real JS, so there is nothing to notify.
    /// </summary>
    ValueTask<string> PositionFixed(string contentId, string referenceId, string align, bool matchWidth, string side, int offset, Func<string, Task>? onSideChanged) =>
        PositionFixed(contentId, referenceId, align, matchWidth, side, offset);
    ValueTask UnpositionFixed(string contentId);

    /// <summary>
    /// Positions a fixed-position element at the viewport point (<paramref name="x"/>,
    /// <paramref name="y"/>) and clamps it inside the viewport (flipping up/left of the
    /// point if it would overflow). Used by ContextMenu, which opens at raw click
    /// coordinates with no anchor element. Default implementation is a no-op so
    /// existing implementers (and test doubles) keep compiling.
    /// </summary>
    ValueTask PositionAtPoint(string contentId, double x, double y) => ValueTask.CompletedTask;

    // Toolbar roving focus (Radix Toolbar keyboard model). Default no-ops so
    // existing implementers/test doubles keep compiling.

    /// <summary>Initialise a single-tab-stop roving tabindex over the toolbar's items.</summary>
    ValueTask InitToolbarRoving(string toolbarId) => ValueTask.CompletedTask;
    /// <summary>Move focus <paramref name="delta"/> items from the focused toolbar item (clamped, no wrap).</summary>
    ValueTask MoveToolbarFocus(string toolbarId, int delta) => ValueTask.CompletedTask;
    /// <summary>Focus the first (<paramref name="last"/>=false) or last toolbar item.</summary>
    ValueTask FocusToolbarEdge(string toolbarId, bool last) => ValueTask.CompletedTask;
    ValueTask<ElementRect?> GetElementRect(string elementId);
    ValueTask<double> GetElementDimension(string elementId, string dimension);
    ValueTask<double> GetScrollTop(string elementId);

    /// <summary>
    /// Attaches a non-passive touchmove guard so PullToRefresh can claim a
    /// downward drag at scrollTop 0 instead of the browser consuming it as
    /// native overscroll. No-op off touch devices. See <c>registerPullToRefresh</c>.
    /// </summary>
    ValueTask RegisterPullToRefresh(string elementId);
    ValueTask UnregisterPullToRefresh(string elementId);

    // Wheel pickers (DateWheelPicker / TimeWheelPicker) — read/write scrollTop on
    // an ElementReference. Used to detect the centre-aligned snap target and to
    // initially scroll each column to the active value on first render.
    ValueTask<double> WheelScrollTop(Microsoft.AspNetCore.Components.ElementReference element);
    ValueTask WheelScrollTo(Microsoft.AspNetCore.Components.ElementReference element, double top);

    // Pointer Capture (used by Splitter dividers)
    ValueTask SetPointerCaptureOnElement(string elementId, long pointerId);
    ValueTask ReleasePointerCaptureOnElement(string elementId, long pointerId);

    // Drawer Swipe
    ValueTask RegisterDrawerSwipe(string elementId, string direction, Func<Task> handler);
    ValueTask RegisterDrawerSwipe(string elementId, Func<Task> handler);
    /// <summary>
    /// 3.0.1 — extended overload that accepts swipe-to-close thresholds. Both
    /// parameters are optional; passing <c>null</c> falls back to the JS
    /// hardcoded defaults so existing callers keep working unchanged. Mirrors
    /// the <see cref="LumeoGestureOptions"/> bag.
    /// </summary>
    /// <param name="activationPx">Pixels of finger travel before the sheet starts following.</param>
    /// <param name="firePx">Pull-distance above which release triggers a dismiss.</param>
    ValueTask RegisterDrawerSwipe(string elementId, string direction, Func<Task> handler, int? activationPx, int? firePx) =>
        RegisterDrawerSwipe(elementId, direction, handler);
    /// <summary>
    /// 3.19 — adds <paramref name="velocity"/> (px/ms): a flick faster than this
    /// in the dismiss direction closes the drawer even below <paramref name="firePx"/>.
    /// Default impl ignores it so non-overriding implementations keep working.
    /// </summary>
    ValueTask RegisterDrawerSwipe(string elementId, string direction, Func<Task> handler, int? activationPx, int? firePx, double? velocity) =>
        RegisterDrawerSwipe(elementId, direction, handler, activationPx, firePx);
    ValueTask UnregisterDrawerSwipe(string elementId);

    // Drawer Snap Points (3.19) — vaul-style fractional resting heights.
    /// <summary>
    /// Registers a snap-point gesture: the drawer rests at one of
    /// <paramref name="snapPoints"/> (fractions 0&lt;f≤1), drags between them, and
    /// dismisses when flicked/dragged below the lowest. <paramref name="snapHandler"/>
    /// fires with the new index on each settle; <paramref name="dismissHandler"/>
    /// fires on close. Default impl falls back to plain swipe-dismiss.
    /// </summary>
    ValueTask RegisterDrawerSnap(string elementId, string direction, Func<Task<bool>> dismissHandler, Func<int, Task> snapHandler, IReadOnlyList<double> snapPoints, int activeIndex, bool dismissible, int? activationPx, int? firePx, double? velocity) =>
        RegisterDrawerSwipe(elementId, direction, () => dismissHandler(), activationPx, firePx);
    /// <summary>Programmatically move a snap-point drawer to <paramref name="index"/>.</summary>
    ValueTask SetDrawerSnap(string elementId, int index) => ValueTask.CompletedTask;
    ValueTask UnregisterDrawerSnap(string elementId) => UnregisterDrawerSwipe(elementId);

    // Tab Swipe — horizontal swipe switches between TabsContent panels
    ValueTask RegisterTabSwipe(string elementId, bool wrap, Func<string, Task> handler);
    /// <summary>
    /// 3.0.1 — extended overload that accepts horizontal swipe and vertical
    /// dead-zone thresholds. Passing <c>null</c> falls back to the JS
    /// hardcoded defaults so existing callers keep working unchanged.
    /// </summary>
    ValueTask RegisterTabSwipe(string elementId, bool wrap, Func<string, Task> handler, int? swipeThresholdPx, int? verticalDeadZonePx) =>
        RegisterTabSwipe(elementId, wrap, handler);
    ValueTask UnregisterTabSwipe(string elementId);

    // Sortable Touch (HTML5 Drag API doesn't fire on touch — separate touch path)
    ValueTask RegisterSortableTouch(string containerId, Func<int, int, Task> handler);
    ValueTask UnregisterSortableTouch(string containerId);

    // Carousel Swipe
    ValueTask RegisterCarouselSwipe(string elementId, string orientation, Func<string, Task> swipeHandler, Func<double, double, int, Task> scrollHandler);
    /// <summary>3.0.1 — overload with explicit swipe / vertical dead-zone thresholds (Carousel).</summary>
    ValueTask RegisterCarouselSwipe(string elementId, string orientation, Func<string, Task> swipeHandler, Func<double, double, int, Task> scrollHandler, int? swipeThresholdPx, int? verticalDeadZonePx) =>
        RegisterCarouselSwipe(elementId, orientation, swipeHandler, scrollHandler);
    ValueTask UnregisterCarouselSwipe(string elementId);
    ValueTask CarouselScrollTo(string elementId, int index, string behavior = "smooth");

    // Horizontal Swipe (Calendar month navigation)
    ValueTask RegisterHorizontalSwipe(string elementId, Func<string, Task> handler);
    /// <summary>3.0.1 — overload with explicit swipe / vertical dead-zone thresholds (Calendar).</summary>
    ValueTask RegisterHorizontalSwipe(string elementId, Func<string, Task> handler, int? swipeThresholdPx, int? verticalDeadZonePx) =>
        RegisterHorizontalSwipe(elementId, handler);
    ValueTask UnregisterHorizontalSwipe(string elementId);

    // Gallery Swipe (ImageGallery fullscreen prev/next, rc.52)
    ValueTask RegisterGallerySwipe(string elementId, Func<string, Task> handler);
    /// <summary>3.0.1 — overload with explicit swipe / vertical dead-zone thresholds (ImageGallery).</summary>
    ValueTask RegisterGallerySwipe(string elementId, Func<string, Task> handler, int? swipeThresholdPx, int? verticalDeadZonePx) =>
        RegisterGallerySwipe(elementId, handler);
    ValueTask UnregisterGallerySwipe(string elementId);

    // Resizable Handle
    ValueTask RegisterResizeHandle(string elementId, string direction, Func<double, Task> resizeHandler, Func<Task> resizeEndHandler);
    ValueTask UnregisterResizeHandle(string elementId);

    // Scrollspy
    ValueTask RegisterScrollspy(string containerId, int offset, bool smooth, Func<string?, Task> handler);
    ValueTask UnregisterScrollspy(string containerId);
    ValueTask ScrollspyScrollTo(string containerId, string sectionId, bool smooth);
    /// <summary>
    /// Click-scroll overload that honours the Scrollspy <c>Offset</c> (#246) so a
    /// programmatic jump lands at the same scroll position the observer treats
    /// as "active" (e.g. clearing a sticky header). The default implementation
    /// ignores the offset and forwards to the 3-arg overload so existing
    /// implementers / test doubles keep compiling unchanged.
    /// </summary>
    ValueTask ScrollspyScrollTo(string containerId, string sectionId, bool smooth, int offset) =>
        ScrollspyScrollTo(containerId, sectionId, smooth);

    // Tabs overflow scroll arrows (#239) — observes the scrollable tablist and
    // reports (canScrollStart, canScrollEnd) on scroll/resize so the list can
    // show/hide its overflow chevrons; TabsScrollBy nudges the list by a chunk.
    // Default no-ops so existing implementers / test doubles keep compiling.
    ValueTask RegisterTabsOverflow(string listId, Func<bool, bool, Task> handler) => ValueTask.CompletedTask;
    ValueTask UnregisterTabsOverflow(string listId) => ValueTask.CompletedTask;
    /// <summary>Scrolls the tablist by <paramref name="delta"/> px (smooth).
    /// Horizontal scrolls scrollLeft; vertical scrolls scrollTop.</summary>
    ValueTask TabsScrollBy(string listId, double delta, bool horizontal) => ValueTask.CompletedTask;

    // Toast Swipe
    ValueTask RegisterToastSwipe(string elementId, string toastId, Func<string, Task> handler);
    ValueTask UnregisterToastSwipe(string toastId, string elementId);

    // Auto Resize
    ValueTask SetupAutoResize(string elementId, int maxRows);
    ValueTask UnregisterAutoResize(string elementId);

    // OTP Paste
    ValueTask RegisterOtpPaste(string baseId, int length, Func<string, Task> handler);
    ValueTask UnregisterOtpPaste(string baseId, int length);

    // Selective keydown preventDefault — suppresses the browser default for
    // the listed keys only, synchronously in the native event dispatch.
    // Use instead of @onkeydown:preventDefault when some keys (e.g. Tab)
    // must keep their default, or when the suppression must not lag one
    // event behind a render-time bool (Splitter, Carousel, PromptInput).
    ValueTask RegisterPreventDefaultKeys(string elementId, IReadOnlyList<PreventDefaultKeyRule> rules);
    ValueTask UnregisterPreventDefaultKeys(string elementId);

    // DataGrid Column Resize — JS previews the drag directly in the DOM and invokes
    // commitHandler once with the final width on mouseup.
    ValueTask RegisterColumnResize(string handleId, double minWidth, double? maxWidth, Func<double, Task> commitHandler);
    /// <summary>
    /// Pointer-based overload that also reports whether the final width came from a
    /// double-click auto-fit-to-content rather than a drag (<paramref name="commitHandler"/>'s
    /// bool). Round-9 #4: the original 4-parameter member above stays the abstract
    /// contract byte-for-byte (a prior round widened ITS delegate type in place, which
    /// broke every external implementer / test double the moment they updated). This is
    /// an ADDITIVE default interface member instead — the default body delegates to the
    /// 4-parameter member with autoFit always <c>false</c> (a legacy handler has no way
    /// to distinguish the two), so existing implementers keep compiling unchanged.
    /// <see cref="Lumeo.Services.ComponentInteropService"/> overrides this overload
    /// directly to report the real autoFit flag.
    /// </summary>
    ValueTask RegisterColumnResize(string handleId, double minWidth, double? maxWidth, Func<double, bool, Task> commitHandler) =>
        RegisterColumnResize(handleId, minWidth, maxWidth, w => commitHandler(w, false));
    ValueTask UnregisterColumnResize(string handleId);
    /// <summary>Keyboard resize: nudges the column width by <paramref name="delta"/> px
    /// (JS clamps to min/max and re-commits through the registered handler). Additive
    /// DIM — no-op default so existing implementers / test doubles keep compiling;
    /// DataGrid's own keyboard resize simply has no effect against a legacy double.</summary>
    ValueTask NudgeColumnResize(string handleId, double delta) => ValueTask.CompletedTask;

    // DataGrid Column Reorder (pointer-based touch/pen) — one delegated listener per
    // grid; commitHandler(sourceColumnId, targetColumnId) fires once on release.
    // Additive DIMs (round-9 #4) — no-op default so existing implementers / test
    // doubles keep compiling; DataGrid's pointer reorder simply won't register
    // against a legacy double.
    ValueTask RegisterColumnReorder(string gridId, Func<string, string, Task> commitHandler) => ValueTask.CompletedTask;
    ValueTask UnregisterColumnReorder(string gridId) => ValueTask.CompletedTask;

    // DataGrid Column Reorder FLIP — capture column rects before reorder,
    // animate from old → new positions after Blazor's re-render.
    ValueTask CaptureColumnRects(string gridId);
    ValueTask AnimateColumnReorder(string gridId, int durationMs);

    /// <summary>Snaps every header/body cell back to identity WITHOUT capturing —
    /// the no-animation counterpart to <see cref="CaptureColumnRects"/> for a
    /// delayed reorder commit that gets REJECTED (columns changed during the
    /// settle window), so the transforms JS left in place never get an accept-path
    /// capture to clear them (round-8 #4). Additive DIM (round-9 #4) — no-op default
    /// so existing implementers / test doubles keep compiling.</summary>
    ValueTask ClearColumnReorderTransforms(string gridId) => ValueTask.CompletedTask;

    // DataGrid Row Reorder (pointer-based mouse/touch/pen, handle-only) — one
    // delegated listener per grid; commitHandler(sourceRowKey, targetRowKey)
    // fires once on release, keyed by stable row identity (not the plain DOM
    // index JS measured at drag start) so a mutation during the post-drop
    // settle delay can't move the wrong row. Only ever registered for flat,
    // non-virtualized grids. Additive DIMs (round-9 #4) — no-op default so
    // existing implementers / test doubles keep compiling.
    ValueTask RegisterRowReorder(string gridId, Func<string, string, Task> commitHandler) => ValueTask.CompletedTask;
    ValueTask UnregisterRowReorder(string gridId) => ValueTask.CompletedTask;

    // DataGrid Row Reorder FLIP — capture row rects (keyed by stable row
    // identity) before reorder, animate from old → new positions after
    // Blazor's re-render. Additive DIMs (round-9 #4) — no-op default so
    // existing implementers / test doubles keep compiling.
    ValueTask CaptureRowRects(string gridId) => ValueTask.CompletedTask;
    ValueTask AnimateRowReorder(string gridId, int durationMs) => ValueTask.CompletedTask;

    /// <summary>Snaps every row (and any expanded detail sibling) back to identity
    /// WITHOUT capturing — the no-animation counterpart to <see cref="CaptureRowRects"/>
    /// for a delayed reorder commit that gets REJECTED (backing rows changed during
    /// the settle window), so the transforms JS left in place never get an
    /// accept-path capture to clear them (round-8 #2). Additive DIM (round-9 #4) —
    /// no-op default so existing implementers / test doubles keep compiling.</summary>
    ValueTask ClearRowReorderTransforms(string gridId) => ValueTask.CompletedTask;

    // Tour
    ValueTask<ElementRect?> GetElementRectBySelector(string selector);
    /// <summary>Instantly scrolls the first element matching <paramref name="selector"/>
    /// into the viewport (block: center). Call BEFORE LockScroll — a locked body
    /// can't be scrolled, programmatically or otherwise.</summary>
    ValueTask ScrollSelectorIntoView(string selector);

    /// <summary>Scrolls the element with id <paramref name="elementId"/> into view
    /// within its nearest scroll container. Used by keyboard-navigated lists
    /// (e.g. the Command palette active item) to keep the highlighted row
    /// visible. <paramref name="block"/> maps to <c>scrollIntoView</c>'s block
    /// option ("nearest" by default so already-visible rows don't jump). Default
    /// implementation is a no-op so existing implementers / test doubles keep
    /// compiling.</summary>
    ValueTask ScrollIntoView(string elementId, string block = "nearest") => ValueTask.CompletedTask;

    // Affix
    ValueTask RegisterAffix(string elementId, int offsetTop, int? offsetBottom, string? target, Func<bool, Task> handler);
    ValueTask UnregisterAffix(string elementId);

    // Mention / Textarea Caret
    ValueTask<ComponentInteropService.TextareaCaretInfo> GetTextareaCaretPosition(string elementId);

    // InputMask caret (selectionStart of a text <input>) — read/restore so masked
    // edits insert/delete at the caret instead of jumping to the end.
    ValueTask<int> GetInputCaret(string elementId) => ValueTask.FromResult(0);
    ValueTask SetInputCaret(string elementId, int position) => ValueTask.CompletedTask;

    // InputMask value (the live el.value of a text <input>) — force-writes the
    // masked display straight to the DOM. Needed when a re-masked value equals
    // the PREVIOUS render's value (e.g. an invalid char was rejected): Blazor's
    // diff then emits no patch, so the browser keeps showing the rejected char
    // unless we push the value ourselves (#41).
    ValueTask SetInputValue(string elementId, string value) => ValueTask.CompletedTask;

    // Tabs (active indicator measurement for animated underline)
    ValueTask<ComponentInteropService.TabMeasurement?> TabsMeasure(string elementId);

    // BackToTop
    ValueTask RegisterBackToTop(string id, int threshold, Func<bool, Task> handler);
    ValueTask UnregisterBackToTop(string id);
    ValueTask ScrollToTop();

    // BackToTop with an optional container Target selector (#98). Default-implemented
    // so existing IComponentInteropService implementations keep compiling: they fall
    // back to the window-scoped overloads, ignoring the container target. The concrete
    // ComponentInteropService overrides these to thread the selector to JS.
    ValueTask RegisterBackToTop(string id, int threshold, Func<bool, Task> handler, string? target)
        => RegisterBackToTop(id, threshold, handler);
    ValueTask ScrollToTop(string? target) => ScrollToTop();

    // File Download
    ValueTask DownloadFile(string fileName, string contentBase64, string mimeType = "application/octet-stream");

    // Clipboard
    ValueTask CopyToClipboard(string text);

    // Press feedback (ripple click effect on Button, Card, Chip, BottomNavItem, ToggleGroupItem)
    ValueTask RippleAttachAsync(Microsoft.AspNetCore.Components.ElementReference element);
    ValueTask RippleDetachAsync(Microsoft.AspNetCore.Components.ElementReference element);

    /// <summary>
    /// Clears the <c>value</c> of a native <c>&lt;input type="file"&gt;</c> so that
    /// re-picking the SAME file fires the <c>change</c> event again. The browser
    /// suppresses <c>change</c> when the chosen path is identical to the input's
    /// current value, so UploadTrigger (a pure pick-trigger with no accumulating
    /// list to mask it) must reset the element after each pick. Default impl is a
    /// no-op so existing implementers / test doubles keep compiling unchanged (#70).
    /// </summary>
    ValueTask ResetFileInput(Microsoft.AspNetCore.Components.ElementReference element) => ValueTask.CompletedTask;

    /// <summary>
    /// Core-side <c>prefers-reduced-motion: reduce</c> query (mirrors the
    /// Lumeo.Motion helper) for core components that animate via Blazor/JS and
    /// can't be fully neutralised by a CSS <c>@media</c> block alone. Default
    /// returns <c>false</c> (motion allowed) so existing implementers/test
    /// doubles keep compiling unchanged.
    /// </summary>
    ValueTask<bool> PrefersReducedMotion() => ValueTask.FromResult(false);

    /// <summary>
    /// True when the currently-focused element is in the browser's <c>:focus-visible</c>
    /// state (keyboard/programmatic focus) rather than a plain <c>:focus</c> a mouse click
    /// also leaves behind. <see cref="Tooltip"/> uses this to gate opening on
    /// <c>focusin</c> so a clicked-then-abandoned trigger doesn't stay open forever — a
    /// native button keeps DOM focus after a mouse click with nothing to clear it, but
    /// <c>:focus-visible</c> is false for that case in supporting browsers. Default
    /// returns <c>true</c> (old behaviour: always open on focus) so existing
    /// implementers/test doubles — which have no real DOM to query — keep compiling and
    /// behaving unchanged.
    /// </summary>
    ValueTask<bool> IsActiveElementFocusVisible() => ValueTask.FromResult(true);

    /// <summary>
    /// Resolves a pointer's viewport coordinates (<paramref name="clientX"/>,
    /// <paramref name="clientY"/>) into coordinates relative to the element
    /// identified by <paramref name="hostElementId"/>. Used by TouchRipple so a
    /// ripple is centred on the click point even when the pointer lands on a
    /// nested child (where <c>OffsetX/OffsetY</c> would be relative to the
    /// child, not the ripple host). Default returns (0,0) for test doubles.
    /// </summary>
    ValueTask<RipplePoint> TouchRippleCoords(string hostElementId, double clientX, double clientY)
        => ValueTask.FromResult(new RipplePoint(0, 0));

    // HTMLMediaElement helpers (AudioPlayer, 3.1.0). Pass-through to play()/pause()
    // and a couple of property setters so Lumeo components never touch IJSRuntime
    // directly. play() rejects when autoplay is blocked — the JS side swallows
    // that, callers should rely on the element's "pause" event to reflect state.
    ValueTask PlayMedia(Microsoft.AspNetCore.Components.ElementReference element);
    ValueTask PauseMedia(Microsoft.AspNetCore.Components.ElementReference element);
    ValueTask SetMediaVolume(Microsoft.AspNetCore.Components.ElementReference element, double volume, bool muted);
    ValueTask SeekMedia(Microsoft.AspNetCore.Components.ElementReference element, double seconds);
    /// <summary>Sets the media element's <c>playbackRate</c> (clamped 0.25–4×).</summary>
    ValueTask SetPlaybackRate(Microsoft.AspNetCore.Components.ElementReference element, double rate) => ValueTask.CompletedTask;
    /// <summary>
    /// Reads the live <c>duration</c> and <c>currentTime</c> off an
    /// HTMLMediaElement. Required because Blazor's media event args don't
    /// expose these — they're properties of the element itself.
    /// </summary>
    ValueTask<MediaState> GetMediaState(Microsoft.AspNetCore.Components.ElementReference element);

    // Haptic feedback — best-effort, no-op on browsers without Vibration API (iOS Safari).
    ValueTask Vibrate(int milliseconds);

    // LocalStorage
    ValueTask SaveToLocalStorage(string key, string value);
    ValueTask<string?> LoadFromLocalStorage(string key);
    ValueTask RemoveFromLocalStorage(string key);

    // Motion primitives

    /// <summary>
    /// Queries the browser's <c>prefers-reduced-motion: reduce</c> media query
    /// via the Lumeo.Motion JS module. Lets a component branch in C# (skip a
    /// burst, snap to the end value) before scheduling any JS-driven animation
    /// that a CSS <c>@media</c> block can't reach. The default implementation
    /// returns <c>false</c> (motion allowed) so existing implementers and test
    /// doubles keep compiling and behave exactly as before.
    /// </summary>
    ValueTask<bool> MotionPrefersReducedMotion() => ValueTask.FromResult(false);

    /// <param name="separator">Thousands group separator (locale-aware; supplied by NumberTicker).</param>
    /// <param name="decimalSeparator">Decimal separator (locale-aware). Defaults to "." for back-compat.</param>
    ValueTask MotionTickNumber(string elementId, double from, double to, int durationMs, int decimals, string separator = ",", string decimalSeparator = ".");
    ValueTask MotionDisposeTicker(string elementId);
    ValueTask MotionRevealText(string elementId, int staggerMs, double threshold);
    ValueTask MotionBlurFade(string elementId, int delayMs, bool once, bool forceHidden = false);
    ValueTask MotionDisposeObserver(string elementId);

    // Motion: AnimatedBeam
    ValueTask MotionAnimatedBeam(string elementId, string fromId, string toId, object options);
    ValueTask MotionDisposeAnimatedBeam(string elementId);

    // Motion: Dock
    ValueTask MotionDock(string elementId, object options);
    ValueTask MotionDisposeDock(string elementId);

    // Motion: Confetti
    ValueTask MotionConfettiInit(string elementId);
    ValueTask MotionConfettiFire(string elementId, object options);
    ValueTask MotionDisposeConfetti(string elementId);

    // AI primitives
    ValueTask AiAutosize(string elementId, int maxPx);
    ValueTask AiObserveAutoScroll(string elementId);
    ValueTask AiDisposeAutoScroll(string elementId);
    ValueTask AiScrollToBottom(string elementId);

    /// <summary>
    /// Observes a message list's scroll position and reports (via
    /// <paramref name="dotNetRef"/>'s <c>OnScrollAwayChanged(bool)</c>) whether it is
    /// scrolled away from the bottom, driving the visibility of AgentMessageList's
    /// floating scroll-to-latest button. Generic in the .NET reference type so this
    /// core interop interface stays decoupled from the UI component. Default no-op so
    /// existing implementers / test doubles keep compiling (they can drive the callback
    /// directly instead of via real JS).
    /// </summary>
    ValueTask AiObserveScrollButton<[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicMethods)] T>(string elementId, Microsoft.JSInterop.DotNetObjectReference<T> dotNetRef) where T : class
        => ValueTask.CompletedTask;
    /// <summary>Tears down the scroll-button observer registered by
    /// <see cref="AiObserveScrollButton{T}"/>. Default no-op.</summary>
    ValueTask AiDisposeScrollButton(string elementId) => ValueTask.CompletedTask;

    // Scheduler (FullCalendar wrapper)
    Task<string> SchedulerInitAsync(Microsoft.AspNetCore.Components.ElementReference el, object dotNetRef, object options);
    Task SchedulerSetEventsAsync(string id, IEnumerable<object> events);
    Task SchedulerChangeViewAsync(string id, string view);
    Task SchedulerGotoDateAsync(string id, string dateIso);
    Task SchedulerPrevAsync(string id);
    Task SchedulerNextAsync(string id);
    Task SchedulerTodayAsync(string id);
    Task<string> SchedulerGetTitleAsync(string id);
    Task SchedulerDestroyAsync(string id);

    // Gantt (Frappe Gantt wrapper)
    Task<string> GanttInitAsync(Microsoft.AspNetCore.Components.ElementReference el, object dotNetRef, object options);
    Task GanttSetTasksAsync(string id, IEnumerable<object> tasks);
    Task GanttChangeViewModeAsync(string id, string mode);
    Task GanttDestroyAsync(string id);

    /// <summary>
    /// Pushes a partial options bag (any of readonly / todayHighlight / barHeight /
    /// columnWidth — and optionally tasks / viewMode) to a LIVE Gantt instance and
    /// re-renders it (gantt.refresh). The init-only options were previously captured
    /// once at GanttInitAsync, so flipping Readonly / TodayHighlight / BarHeight /
    /// ColumnWidth after init was silently ignored (battle-test wave 1 #2). Default
    /// implementation is a no-op so existing implementers / test doubles keep
    /// compiling and the non-Gantt paths are unaffected.
    /// </summary>
    Task GanttRefreshAsync(string id, object options) => Task.CompletedTask;

    /// <summary>
    /// Centers <paramref name="targetX"/> (a pixel offset within the timeline's own
    /// scrollable content) in <paramref name="el"/>'s viewport — GanttV3's own tiny
    /// scroll slice (gantt-v3.js), used to mirror v2's init-time
    /// center-on-today behavior (gantt-v2.js's <c>tryScroll</c>) so the initial
    /// viewport shows task data instead of the empty padding columns
    /// <c>Gantt3.ComputeInitialRange</c> adds before the first task. Default no-op so
    /// existing implementers / test doubles keep compiling.
    /// </summary>
    Task GanttV3ScrollToXAsync(Microsoft.AspNetCore.Components.ElementReference el, double targetX) => Task.CompletedTask;

    /// <summary>
    /// Registers GanttV3's pointer drag engine (design spec Phase 2, T1 — gantt-v3.js's
    /// <c>ganttV3.registerDrag</c>) on <paramref name="el"/> — the SAME scroll-host
    /// element <see cref="GanttV3ScrollToXAsync"/> targets. A single delegated
    /// <c>pointerdown</c> listener handles move/resize-left/resize-right for every
    /// <c>[data-task-id]</c> bar underneath, so re-registering after a Virtualize
    /// recycle is never needed. Calling this again for an already-registered
    /// <paramref name="el"/> updates the stored <paramref name="dotNetRef"/>/
    /// <paramref name="options"/> in place (idempotent) rather than attaching a
    /// second listener — <c>GanttTimeline</c> relies on this to re-push
    /// <c>columnWidth</c>/<c>pixelsPerDay</c> whenever the view mode or a
    /// <c>ColumnWidth</c> override changes, without an explicit unregister first.
    /// <c>GanttTimeline.Readonly</c> gates the CALL SITE, not this method — a
    /// readonly timeline never calls this at all, so there is no listener to gate
    /// internally. Generic in the .NET reference type (mirrors
    /// <see cref="AttachOverlayExitEnd{T}"/>/<see cref="AiObserveScrollButton{T}"/>)
    /// so this core interop interface stays decoupled from the GanttTimeline UI
    /// component. Default no-op DIM so existing implementers/test doubles keep
    /// compiling; <see cref="Lumeo.Services.ComponentInteropService"/> overrides
    /// both with the real registration.
    /// </summary>
    Task GanttV3RegisterDragAsync<[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicMethods)] T>(Microsoft.AspNetCore.Components.ElementReference el, DotNetObjectReference<T> dotNetRef, object options) where T : class
        => Task.CompletedTask;

    /// <summary>Tears down the drag engine registered by <see cref="GanttV3RegisterDragAsync{T}"/> — removes the delegated listener and releases any drag in flight. Default no-op.</summary>
    Task GanttV3UnregisterDragAsync(Microsoft.AspNetCore.Components.ElementReference el) => Task.CompletedTask;

    // Toolbar overflow observer — registers a ResizeObserver on the toolbar
    // element and invokes the handler with (fittingCount, totalCount) whenever
    // the number of items that fit before the "..." overflow trigger changes.
    ValueTask RegisterToolbarOverflow(string elementId, Func<int, int, Task> handler);
    ValueTask UnregisterToolbarOverflow(string elementId);

    // Rich Text Editor (TipTap wrapper)
    ValueTask<string> RichTextInitAsync<[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicMethods)] T>(
        Microsoft.AspNetCore.Components.ElementReference elementRef,
        DotNetObjectReference<T> dotNetRef,
        object options) where T : class;
    ValueTask RichTextSetContentAsync(string id, string? html);
    ValueTask RichTextCommandAsync(string id, string name, params object?[]? args);
    ValueTask<Interop.RichTextActiveState?> RichTextGetActiveAsync(string id);
    ValueTask RichTextSetDisabledAsync(string id, bool disabled);
    ValueTask RichTextDestroyAsync(string id);
    ValueTask<string?> RichTextPromptLinkAsync(string? initial);
    // Additive — default no-op so existing IComponentInteropService implementations / test fakes don't
    // need to change. ComponentInteropService overrides it with the real setAttribute interop.
    ValueTask RichTextSetAriaAttributesAsync(string id, bool ariaInvalid, string? ariaDescribedBy) => ValueTask.CompletedTask;

    // SignaturePad — canvas-based handwritten signature capture (3.1.0).
    // Ships its own tiny JS module (signature-pad.js) loaded lazily on first
    // use so apps that never render a SignaturePad don't pay the import cost.
    // Generic in the .NET object-reference type so this core interop interface stays decoupled from
    // the SignaturePad UI component (T is inferred from the call site as SignaturePad). This keeps
    // the service layer free of UI-component references so it can be vendored standalone.
    ValueTask SignaturePadInit<[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicMethods)] T>(string elementId, object options, DotNetObjectReference<T> dotNetRef) where T : class;
    ValueTask SignaturePadClear(string elementId);
    ValueTask<string?> SignaturePadDataUrl(string elementId, string mimeType);
    ValueTask SignaturePadSetStrokeStyle(string elementId, string color, double width);
    ValueTask SignaturePadSetDisabled(string elementId, bool disabled);
    ValueTask SignaturePadLoadDataUrl(string elementId, string? dataUrl);
    ValueTask SignaturePadDestroy(string elementId);
}
