using Lumeo.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Lumeo.Tests.Helpers;

/// <summary>
/// A test-only IComponentInteropService implementation where every method is a
/// no-op except Vibrate(), RegisterTabSwipe(), and RegisterHorizontalSwipe(),
/// which record each call so tests can assert lifecycle behaviour.
/// </summary>
public sealed class TrackingInteropService : IComponentInteropService
{
    private readonly List<int> _vibrateArgs = new();
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

    public ValueTask Vibrate(int milliseconds)
    {
        _vibrateArgs.Add(milliseconds);
        return ValueTask.CompletedTask;
    }

    // ---- All remaining members are silent no-ops ----

    public ValueTask RegisterClickOutside(string elementId, string? triggerElementId, Func<Task> handler) => ValueTask.CompletedTask;
    public ValueTask UnregisterClickOutside(string elementId) => ValueTask.CompletedTask;
    public ValueTask FocusElement(string elementId) => ValueTask.CompletedTask;
    public ValueTask FocusMenuItemByIndex(string containerId, int index) => ValueTask.CompletedTask;
    public ValueTask<int> GetMenuItemCount(string containerId) => ValueTask.FromResult(0);
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
    public ValueTask WheelScrollTo(ElementReference element, double top) => ValueTask.CompletedTask;
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
    public ValueTask RegisterOtpPaste(string baseId, int length, Func<string, Task> handler) => ValueTask.CompletedTask;
    public ValueTask UnregisterOtpPaste(string baseId, int length) => ValueTask.CompletedTask;
    public ValueTask RegisterColumnResize(string handleId, double minWidth, double? maxWidth, Func<double, Task> commitHandler) => ValueTask.CompletedTask;
    public ValueTask UnregisterColumnResize(string handleId) => ValueTask.CompletedTask;
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

    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
