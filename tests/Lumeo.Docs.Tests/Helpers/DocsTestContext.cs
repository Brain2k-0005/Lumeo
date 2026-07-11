using Bunit;
using Lumeo.Docs.Services;
using Lumeo.Services;
using Lumeo.Services.Interop;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Lumeo.Docs.Tests.Helpers;

public static class DocsTestContextExtensions
{
    public static void AddDocsServices(this BunitContext ctx)
    {
        ctx.Services.AddSingleton<IComponentInteropService, NoopInteropService>();
        ctx.Services.AddSingleton<NavConfigService>();
        ctx.Services.AddSingleton<RegistryService>();
        ctx.Services.AddSingleton<IconService>();
        // DynamicIcon (used across nearly every component page) resolves its glyph through the
        // shared, data-driven resolver. In bUnit only Lucide is loaded, so the resolver degrades
        // any non-Lucide active pack to Lucide — every page still renders a real icon.
        ctx.Services.AddSingleton<DynamicIconResolver>();
        // Catalog page injects Lumeo.Services.OverlayService for the mobile
        // filter sheet. Tests never open the sheet, but bUnit's DI fails fast
        // if the dependency isn't registered. Singleton is fine here — the
        // OverlayService itself is just an event broker, no per-scope state.
        ctx.Services.AddSingleton<OverlayService>();
    }
}

internal sealed class NoopInteropService : IComponentInteropService
{
    // IAsyncDisposable
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // IDisposable
    public void Dispose() { }

    // Click Outside
    public ValueTask RegisterClickOutside(string elementId, string? triggerElementId, Func<Task> handler) => ValueTask.CompletedTask;
    public ValueTask UnregisterClickOutside(string elementId) => ValueTask.CompletedTask;

    // Focus / Scroll Lock
    public ValueTask FocusElement(string elementId) => ValueTask.CompletedTask;
    public ValueTask FocusMenuItemByIndex(string containerId, int index) => ValueTask.CompletedTask;
    public ValueTask<int> GetMenuItemCount(string containerId) => ValueTask.FromResult(0);
    public ValueTask LockScroll() => ValueTask.CompletedTask;
    public ValueTask UnlockScroll() => ValueTask.CompletedTask;
    public ValueTask AttachOverlaySlideEnd(string elementId) => ValueTask.CompletedTask;
    public ValueTask SetHtmlClass(string className, bool active) => ValueTask.CompletedTask;
    public ValueTask SetupFocusTrap(string elementId, string? initialFocusSelector = null) => ValueTask.CompletedTask;
    public ValueTask RemoveFocusTrap(string elementId) => ValueTask.CompletedTask;
    public ValueTask SaveFocus(string key) => ValueTask.CompletedTask;
    public ValueTask RestoreFocus(string key) => ValueTask.CompletedTask;

    // ColorPicker SV Drag
    public ValueTask RegisterSvDrag(string elementId, Func<double, double, Task> handler) => ValueTask.CompletedTask;
    public ValueTask UnregisterSvDrag(string elementId) => ValueTask.CompletedTask;

    // Viewport
    public ValueTask<ViewportSize> GetViewportSize() => ValueTask.FromResult(new ViewportSize(1920, 1080));
    // 2.1.3: viewport listener no-ops — docs tests never drive resize events.
    public ValueTask<ViewportSize?> RegisterViewportListener(DotNetObjectReference<ResponsiveService> dotnetRef) => ValueTask.FromResult<ViewportSize?>(new ViewportSize(1920, 1080));
    public ValueTask UnregisterViewportListener() => ValueTask.CompletedTask;

    // Floating Position
    public ValueTask<string> PositionFixed(string contentId, string referenceId, string align = "start", bool matchWidth = false, string side = "bottom") => ValueTask.FromResult(side);
    public ValueTask UnpositionFixed(string contentId) => ValueTask.CompletedTask;
    public ValueTask<ElementRect?> GetElementRect(string elementId) => ValueTask.FromResult<ElementRect?>(null);
    public ValueTask<double> GetElementDimension(string elementId, string dimension) => ValueTask.FromResult(0.0);

    // Pointer Capture
    public ValueTask SetPointerCaptureOnElement(string elementId, long pointerId) => ValueTask.CompletedTask;
    public ValueTask ReleasePointerCaptureOnElement(string elementId, long pointerId) => ValueTask.CompletedTask;

    // Drawer Swipe
    public ValueTask RegisterDrawerSwipe(string elementId, string direction, Func<Task> handler) => ValueTask.CompletedTask;
    public ValueTask RegisterDrawerSwipe(string elementId, Func<Task> handler) => ValueTask.CompletedTask;
    public ValueTask UnregisterDrawerSwipe(string elementId) => ValueTask.CompletedTask;

    // Sortable Touch
    public ValueTask RegisterSortableTouch(string containerId, Func<int, int, Task> handler) => ValueTask.CompletedTask;
    public ValueTask UnregisterSortableTouch(string containerId) => ValueTask.CompletedTask;

    // Carousel Swipe
    public ValueTask RegisterCarouselSwipe(string elementId, string orientation, Func<string, Task> swipeHandler, Func<double, double, int, Task> scrollHandler) => ValueTask.CompletedTask;
    public ValueTask UnregisterCarouselSwipe(string elementId) => ValueTask.CompletedTask;
    public ValueTask CarouselScrollTo(string elementId, int index, string behavior = "smooth") => ValueTask.CompletedTask;

    // Horizontal Swipe (Calendar month navigation)
    public ValueTask RegisterHorizontalSwipe(string elementId, Func<string, Task> handler) => ValueTask.CompletedTask;
    public ValueTask UnregisterHorizontalSwipe(string elementId) => ValueTask.CompletedTask;

    // Gallery Swipe (ImageGallery fullscreen prev/next, rc.52)
    public ValueTask RegisterGallerySwipe(string elementId, Func<string, Task> handler) => ValueTask.CompletedTask;
    public ValueTask UnregisterGallerySwipe(string elementId) => ValueTask.CompletedTask;

    // Tab Swipe (rc.52)
    public ValueTask RegisterTabSwipe(string elementId, bool wrap, Func<string, Task> handler) => ValueTask.CompletedTask;
    public ValueTask UnregisterTabSwipe(string elementId) => ValueTask.CompletedTask;

    // Resizable Handle
    public ValueTask RegisterResizeHandle(string elementId, string direction, Func<double, Task> resizeHandler, Func<Task> resizeEndHandler) => ValueTask.CompletedTask;
    public ValueTask UnregisterResizeHandle(string elementId) => ValueTask.CompletedTask;

    // Scrollspy
    public ValueTask RegisterScrollspy(string containerId, int offset, bool smooth, Func<string?, Task> handler) => ValueTask.CompletedTask;
    public ValueTask UnregisterScrollspy(string containerId) => ValueTask.CompletedTask;
    public ValueTask ScrollspyScrollTo(string containerId, string sectionId, bool smooth) => ValueTask.CompletedTask;

    // Toast Swipe
    public ValueTask RegisterToastSwipe(string elementId, string toastId, Func<string, Task> handler) => ValueTask.CompletedTask;
    public ValueTask UnregisterToastSwipe(string toastId, string elementId) => ValueTask.CompletedTask;

    // Auto Resize
    public ValueTask SetupAutoResize(string elementId, int maxRows) => ValueTask.CompletedTask;
    public ValueTask UnregisterAutoResize(string elementId) => ValueTask.CompletedTask;

    // OTP Paste
    public ValueTask RegisterOtpPaste(string baseId, int length, Func<string, Task> handler) => ValueTask.CompletedTask;
    public ValueTask UnregisterOtpPaste(string baseId, int length) => ValueTask.CompletedTask;

    // Selective keydown preventDefault
    public ValueTask RegisterPreventDefaultKeys(string elementId, IReadOnlyList<PreventDefaultKeyRule> rules) => ValueTask.CompletedTask;
    public ValueTask UnregisterPreventDefaultKeys(string elementId) => ValueTask.CompletedTask;

    // Tour scroll-into-view
    public ValueTask ScrollSelectorIntoView(string selector) => ValueTask.CompletedTask;

    // DataGrid Column Resize
    public ValueTask RegisterColumnResize(string handleId, double minWidth, double? maxWidth, Func<double, bool, Task> commitHandler) => ValueTask.CompletedTask;
    public ValueTask UnregisterColumnResize(string handleId) => ValueTask.CompletedTask;
    public ValueTask NudgeColumnResize(string handleId, double delta) => ValueTask.CompletedTask;
    public ValueTask RegisterColumnReorder(string gridId, Func<string, string, Task> commitHandler) => ValueTask.CompletedTask;
    public ValueTask UnregisterColumnReorder(string gridId) => ValueTask.CompletedTask;

    // DataGrid Column Reorder FLIP
    public ValueTask CaptureColumnRects(string gridId) => ValueTask.CompletedTask;
    public ValueTask AnimateColumnReorder(string gridId, int durationMs) => ValueTask.CompletedTask;
    public ValueTask ClearColumnReorderTransforms(string gridId) => ValueTask.CompletedTask;

    // DataGrid Row Reorder
    public ValueTask RegisterRowReorder(string gridId, Func<string, string, Task> commitHandler) => ValueTask.CompletedTask;
    public ValueTask UnregisterRowReorder(string gridId) => ValueTask.CompletedTask;

    // DataGrid Row Reorder FLIP
    public ValueTask CaptureRowRects(string gridId) => ValueTask.CompletedTask;
    public ValueTask AnimateRowReorder(string gridId, int durationMs) => ValueTask.CompletedTask;
    public ValueTask ClearRowReorderTransforms(string gridId) => ValueTask.CompletedTask;

    // Tour
    public ValueTask<ElementRect?> GetElementRectBySelector(string selector) => ValueTask.FromResult<ElementRect?>(null);

    // Affix
    public ValueTask RegisterAffix(string elementId, int offsetTop, int? offsetBottom, string? target, Func<bool, Task> handler) => ValueTask.CompletedTask;
    public ValueTask UnregisterAffix(string elementId) => ValueTask.CompletedTask;

    // Mention / Textarea Caret
    public ValueTask<ComponentInteropService.TextareaCaretInfo> GetTextareaCaretPosition(string elementId)
        => ValueTask.FromResult(new ComponentInteropService.TextareaCaretInfo(0, 0, 0));

    // BackToTop
    public ValueTask RegisterBackToTop(string id, int threshold, Func<bool, Task> handler) => ValueTask.CompletedTask;
    public ValueTask UnregisterBackToTop(string id) => ValueTask.CompletedTask;
    public ValueTask ScrollToTop() => ValueTask.CompletedTask;

    // File Download
    public ValueTask DownloadFile(string fileName, string contentBase64, string mimeType = "application/octet-stream") => ValueTask.CompletedTask;

    // Clipboard
    public ValueTask CopyToClipboard(string text) => ValueTask.CompletedTask;

    // Press feedback (ripple click effect)
    public ValueTask RippleAttachAsync(ElementReference element) => ValueTask.CompletedTask;
    public ValueTask RippleDetachAsync(ElementReference element) => ValueTask.CompletedTask;

    // Haptic feedback
    public ValueTask Vibrate(int milliseconds) => ValueTask.CompletedTask;

    // Pinch zoom (rc.49)
    public ValueTask RegisterPinchZoom(string elementId, Func<double, Task> handler) => ValueTask.CompletedTask;
    public ValueTask UnregisterPinchZoom(string elementId) => ValueTask.CompletedTask;

    // Scroll utilities (rc.49 — wheel pickers, getScrollTop)
    public ValueTask<double> GetScrollTop(string elementId) => ValueTask.FromResult(0.0);
    public ValueTask RegisterPullToRefresh(string elementId) => ValueTask.CompletedTask;
    public ValueTask UnregisterPullToRefresh(string elementId) => ValueTask.CompletedTask;
    public ValueTask<double> WheelScrollTop(ElementReference element) => ValueTask.FromResult(0.0);
    public ValueTask WheelScrollTo(ElementReference element, double top) => ValueTask.CompletedTask;

    // LocalStorage
    public ValueTask SaveToLocalStorage(string key, string value) => ValueTask.CompletedTask;
    public ValueTask<string?> LoadFromLocalStorage(string key) => ValueTask.FromResult<string?>(null);
    public ValueTask RemoveFromLocalStorage(string key) => ValueTask.CompletedTask;

    // Motion primitives
    public ValueTask MotionTickNumber(string elementId, double from, double to, int durationMs, int decimals, string separator = ",", string decimalSeparator = ".") => ValueTask.CompletedTask;
    public ValueTask MotionDisposeTicker(string elementId) => ValueTask.CompletedTask;
    public ValueTask MotionRevealText(string elementId, int staggerMs, double threshold) => ValueTask.CompletedTask;
    public ValueTask MotionBlurFade(string elementId, int delayMs, bool once, bool forceHidden = false) => ValueTask.CompletedTask;
    public ValueTask MotionDisposeObserver(string elementId) => ValueTask.CompletedTask;

    // Motion: AnimatedBeam
    public ValueTask MotionAnimatedBeam(string elementId, string fromId, string toId, object options) => ValueTask.CompletedTask;
    public ValueTask MotionDisposeAnimatedBeam(string elementId) => ValueTask.CompletedTask;

    // Motion: Dock
    public ValueTask MotionDock(string elementId, object options) => ValueTask.CompletedTask;
    public ValueTask MotionDisposeDock(string elementId) => ValueTask.CompletedTask;

    // Motion: Confetti
    public ValueTask MotionConfettiInit(string elementId) => ValueTask.CompletedTask;
    public ValueTask MotionConfettiFire(string elementId, object options) => ValueTask.CompletedTask;
    public ValueTask MotionDisposeConfetti(string elementId) => ValueTask.CompletedTask;

    // AI primitives
    public ValueTask AiAutosize(string elementId, int maxPx) => ValueTask.CompletedTask;
    public ValueTask AiObserveAutoScroll(string elementId) => ValueTask.CompletedTask;
    public ValueTask AiDisposeAutoScroll(string elementId) => ValueTask.CompletedTask;
    public ValueTask AiScrollToBottom(string elementId) => ValueTask.CompletedTask;

    // Scheduler
    public Task<string> SchedulerInitAsync(ElementReference el, object dotNetRef, object options) => Task.FromResult(string.Empty);
    public Task SchedulerSetEventsAsync(string id, IEnumerable<object> events) => Task.CompletedTask;
    public Task SchedulerChangeViewAsync(string id, string view) => Task.CompletedTask;
    public Task SchedulerGotoDateAsync(string id, string dateIso) => Task.CompletedTask;
    public Task SchedulerPrevAsync(string id) => Task.CompletedTask;
    public Task SchedulerNextAsync(string id) => Task.CompletedTask;
    public Task SchedulerTodayAsync(string id) => Task.CompletedTask;
    public Task<string> SchedulerGetTitleAsync(string id) => Task.FromResult(string.Empty);
    public Task SchedulerDestroyAsync(string id) => Task.CompletedTask;

    // Gantt
    public Task<string> GanttInitAsync(ElementReference el, object dotNetRef, object options) => Task.FromResult(string.Empty);
    public Task GanttSetTasksAsync(string id, IEnumerable<object> tasks) => Task.CompletedTask;
    public Task GanttChangeViewModeAsync(string id, string mode) => Task.CompletedTask;
    public Task GanttDestroyAsync(string id) => Task.CompletedTask;

    // Rich Text Editor
    public ValueTask<string> RichTextInitAsync<T>(ElementReference elementRef, DotNetObjectReference<T> dotNetRef, object options) where T : class
        => ValueTask.FromResult(string.Empty);
    public ValueTask RichTextSetContentAsync(string id, string? html) => ValueTask.CompletedTask;
    public ValueTask RichTextCommandAsync(string id, string name, params object?[]? args) => ValueTask.CompletedTask;
    public ValueTask<RichTextActiveState?> RichTextGetActiveAsync(string id) => ValueTask.FromResult<RichTextActiveState?>(null);
    public ValueTask RichTextSetDisabledAsync(string id, bool disabled) => ValueTask.CompletedTask;
    public ValueTask RichTextDestroyAsync(string id) => ValueTask.CompletedTask;
    public ValueTask<string?> RichTextPromptLinkAsync(string? initial) => ValueTask.FromResult<string?>(null);

    public ValueTask<Lumeo.Services.ComponentInteropService.TabMeasurement?> TabsMeasure(string elementId)
        => ValueTask.FromResult<Lumeo.Services.ComponentInteropService.TabMeasurement?>(null);
    public ValueTask RegisterToolbarOverflow(string elementId, Func<int, int, Task> handler) => ValueTask.CompletedTask;
    public ValueTask UnregisterToolbarOverflow(string elementId) => ValueTask.CompletedTask;

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
    public ValueTask SignaturePadLoadDataUrl(string elementId, string? dataUrl) => ValueTask.CompletedTask;
    public ValueTask SignaturePadDestroy(string elementId) => ValueTask.CompletedTask;
}
