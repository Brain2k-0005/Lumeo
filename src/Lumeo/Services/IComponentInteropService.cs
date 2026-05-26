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
    ValueTask LockScroll();
    ValueTask UnlockScroll();
    /// <summary>Toggles a class on <c>document.documentElement</c>. Useful for
    /// global modes (e.g. hiding floating chrome while a DataGrid is fullscreen).</summary>
    ValueTask SetHtmlClass(string className, bool active);
    ValueTask SetupFocusTrap(string elementId);
    ValueTask RemoveFocusTrap(string elementId);

    /// <summary>Registers a native animationend listener that filters strictly on
    /// the slide-in animation name and, on completion, sets the element's inline
    /// <c>transform: none</c>. Bypasses Blazor's event roundtrip so the cleanup
    /// runs synchronously from the browser's animation pipeline. Used by Sheet,
    /// Drawer and any future slide-in overlay to defeat the
    /// <c>animation-fill-mode: both</c> identity-matrix transform trap that
    /// would otherwise establish a containing block for fixed-positioned
    /// descendants (Select / DatePicker / Combobox popovers).</summary>
    ValueTask AttachOverlaySlideEnd(string elementId);

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

    // Floating Position
    ValueTask PositionFixed(string contentId, string referenceId, string align = "start", bool matchWidth = false, string side = "bottom");
    ValueTask UnpositionFixed(string contentId);
    ValueTask<ElementRect?> GetElementRect(string elementId);
    ValueTask<double> GetElementDimension(string elementId, string dimension);
    ValueTask<double> GetScrollTop(string elementId);

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
    ValueTask UnregisterDrawerSwipe(string elementId);

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

    // Toast Swipe
    ValueTask RegisterToastSwipe(string elementId, string toastId, Func<string, Task> handler);
    ValueTask UnregisterToastSwipe(string toastId, string elementId);

    // Auto Resize
    ValueTask SetupAutoResize(string elementId, int maxRows);
    ValueTask UnregisterAutoResize(string elementId);

    // OTP Paste
    ValueTask RegisterOtpPaste(string baseId, int length, Func<string, Task> handler);
    ValueTask UnregisterOtpPaste(string baseId, int length);

    // DataGrid Column Resize — JS previews the drag directly in the DOM and invokes
    // commitHandler once with the final width on mouseup.
    ValueTask RegisterColumnResize(string handleId, double minWidth, double? maxWidth, Func<double, Task> commitHandler);
    ValueTask UnregisterColumnResize(string handleId);

    // DataGrid Column Reorder FLIP — capture column rects before reorder,
    // animate from old → new positions after Blazor's re-render.
    ValueTask CaptureColumnRects(string gridId);
    ValueTask AnimateColumnReorder(string gridId, int durationMs);

    // Tour
    ValueTask<ElementRect?> GetElementRectBySelector(string selector);

    // Affix
    ValueTask RegisterAffix(string elementId, int offsetTop, int? offsetBottom, string? target, Func<bool, Task> handler);
    ValueTask UnregisterAffix(string elementId);

    // Mention / Textarea Caret
    ValueTask<ComponentInteropService.TextareaCaretInfo> GetTextareaCaretPosition(string elementId);

    // Tabs (active indicator measurement for animated underline)
    ValueTask<ComponentInteropService.TabMeasurement?> TabsMeasure(string elementId);

    // BackToTop
    ValueTask RegisterBackToTop(string id, int threshold, Func<bool, Task> handler);
    ValueTask UnregisterBackToTop(string id);
    ValueTask ScrollToTop();

    // File Download
    ValueTask DownloadFile(string fileName, string contentBase64, string mimeType = "application/octet-stream");

    // Clipboard
    ValueTask CopyToClipboard(string text);

    // Press feedback (ripple click effect on Button, Card, Chip, BottomNavItem, ToggleGroupItem)
    ValueTask RippleAttachAsync(Microsoft.AspNetCore.Components.ElementReference element);
    ValueTask RippleDetachAsync(Microsoft.AspNetCore.Components.ElementReference element);

    // HTMLMediaElement helpers (AudioPlayer, 3.1.0). Pass-through to play()/pause()
    // and a couple of property setters so Lumeo components never touch IJSRuntime
    // directly. play() rejects when autoplay is blocked — the JS side swallows
    // that, callers should rely on the element's "pause" event to reflect state.
    ValueTask PlayMedia(Microsoft.AspNetCore.Components.ElementReference element);
    ValueTask PauseMedia(Microsoft.AspNetCore.Components.ElementReference element);
    ValueTask SetMediaVolume(Microsoft.AspNetCore.Components.ElementReference element, double volume, bool muted);
    ValueTask SeekMedia(Microsoft.AspNetCore.Components.ElementReference element, double seconds);
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
    ValueTask MotionTickNumber(string elementId, double from, double to, int durationMs, int decimals, string separator = ",");
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

    // Toolbar overflow observer — registers a ResizeObserver on the toolbar
    // element and invokes the handler with (fittingCount, totalCount) whenever
    // the number of items that fit before the "..." overflow trigger changes.
    ValueTask RegisterToolbarOverflow(string elementId, Func<int, int, Task> handler);
    ValueTask UnregisterToolbarOverflow(string elementId);

    // Rich Text Editor (TipTap wrapper)
    ValueTask<string> RichTextInitAsync<T>(
        Microsoft.AspNetCore.Components.ElementReference elementRef,
        DotNetObjectReference<T> dotNetRef,
        object options) where T : class;
    ValueTask RichTextSetContentAsync(string id, string? html);
    ValueTask RichTextCommandAsync(string id, string name, params object?[]? args);
    ValueTask<Interop.RichTextActiveState?> RichTextGetActiveAsync(string id);
    ValueTask RichTextSetDisabledAsync(string id, bool disabled);
    ValueTask RichTextDestroyAsync(string id);
    ValueTask<string?> RichTextPromptLinkAsync(string? initial);

    // SignaturePad — canvas-based handwritten signature capture (3.1.0).
    // Ships its own tiny JS module (signature-pad.js) loaded lazily on first
    // use so apps that never render a SignaturePad don't pay the import cost.
    ValueTask SignaturePadInit(string elementId, object options, DotNetObjectReference<SignaturePad> dotNetRef);
    ValueTask SignaturePadClear(string elementId);
    ValueTask<string?> SignaturePadDataUrl(string elementId, string mimeType);
    ValueTask SignaturePadSetStrokeStyle(string elementId, string color, double width);
    ValueTask SignaturePadSetDisabled(string elementId, bool disabled);
    ValueTask SignaturePadLoadDataUrl(string elementId, string? dataUrl);
    ValueTask SignaturePadDestroy(string elementId);
}
