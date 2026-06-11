using Lumeo.Services;
using Lumeo.Services.Interop;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Xunit;

namespace Lumeo.Tests.Services;

/// <summary>
/// Tests for <see cref="ResponsiveService"/> and the <see cref="ViewportInfo"/>
/// pure mapping helper (2.1.3). The service uses JS interop to listen for resize
/// events, but the breakpoint mapping and event-firing logic can be exercised
/// entirely through the <c>[JSInvokable] OnViewportChange</c> entry point with
/// an in-file no-op interop stub — no Bunit context required.
/// </summary>
public class ResponsiveServiceTests
{
    // ------------------------------------------------------------------
    // Pure mapping: ViewportInfo.FromWidth
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(0, Breakpoint.Xs)]
    [InlineData(639, Breakpoint.Xs)]
    [InlineData(640, Breakpoint.Sm)]
    [InlineData(767, Breakpoint.Sm)]
    [InlineData(768, Breakpoint.Md)]
    [InlineData(1023, Breakpoint.Md)]
    [InlineData(1024, Breakpoint.Lg)]
    [InlineData(1279, Breakpoint.Lg)]
    [InlineData(1280, Breakpoint.Xl)]
    [InlineData(1535, Breakpoint.Xl)]
    [InlineData(1536, Breakpoint.Xxl)]
    [InlineData(4000, Breakpoint.Xxl)]
    public void ViewportInfo_FromWidth_Maps_All_Breakpoints(double width, Breakpoint expected)
    {
        Assert.Equal(expected, ViewportInfo.FromWidth(width));
    }

    // ------------------------------------------------------------------
    // OnViewportChange: updates state and fires event
    // ------------------------------------------------------------------

    [Fact]
    public void OnViewportChange_Updates_Width_Height_And_Current()
    {
        var svc = new ResponsiveService(new NoOpInterop());
        svc.OnViewportChange(500, 800);

        Assert.Equal(500, svc.Width);
        Assert.Equal(800, svc.Height);
        Assert.Equal(Breakpoint.Xs, svc.Current);
        Assert.True(svc.IsMobile);
        Assert.False(svc.IsTablet);
        Assert.False(svc.IsDesktop);
    }

    [Fact]
    public void OnViewportChange_Fires_ViewportChanged_Event_On_Resize()
    {
        var svc = new ResponsiveService(new NoOpInterop());
        var fired = new List<ViewportInfo>();
        svc.ViewportChanged += vp => fired.Add(vp);

        svc.OnViewportChange(1200, 900);
        svc.OnViewportChange(800, 600);

        Assert.Equal(2, fired.Count);
        Assert.Equal(1200, fired[0].Width);
        Assert.Equal(900, fired[0].Height);
        Assert.Equal(Breakpoint.Lg, fired[0].Current);
        Assert.Equal(800, fired[1].Width);
        Assert.Equal(600, fired[1].Height);
        Assert.Equal(Breakpoint.Md, fired[1].Current);
    }

    [Fact]
    public void OnViewportChange_Does_Not_Fire_When_Size_Unchanged()
    {
        var svc = new ResponsiveService(new NoOpInterop());
        var fireCount = 0;
        svc.ViewportChanged += _ => fireCount++;

        svc.OnViewportChange(1024, 768);
        svc.OnViewportChange(1024, 768);

        Assert.Equal(1, fireCount);
    }

    [Theory]
    [InlineData(500)]
    [InlineData(700)]
    [InlineData(900)]
    [InlineData(1100)]
    [InlineData(1300)]
    [InlineData(1600)]
    public void IsMobile_IsTablet_IsDesktop_Are_Mutually_Exclusive(double width)
    {
        var svc = new ResponsiveService(new NoOpInterop());
        svc.OnViewportChange(width, 800);

        var flags = new[] { svc.IsMobile, svc.IsTablet, svc.IsDesktop };
        Assert.Equal(1, flags.Count(f => f));
    }

    // ------------------------------------------------------------------
    // Minimal in-file IComponentInteropService stub.
    //
    // ResponsiveService only touches RegisterViewportListener (returns the
    // initial size synchronously) and UnregisterViewportListener (cleanup).
    // The OnViewportChange entry point under test does NOT route through the
    // interop, so every other member can be a no-op. We deliberately don't
    // pull in TrackingInteropService here — this test class doesn't need
    // BunitContext at all, so the stub keeps the test self-contained.
    // ------------------------------------------------------------------
    private sealed class NoOpInterop : IComponentInteropService
    {
        public ValueTask<ViewportSize?> RegisterViewportListener(DotNetObjectReference<ResponsiveService> dotnetRef)
            => ValueTask.FromResult<ViewportSize?>(new ViewportSize(0, 0));
        public ValueTask UnregisterViewportListener() => ValueTask.CompletedTask;
        public ValueTask<ViewportSize> GetViewportSize() => ValueTask.FromResult(new ViewportSize(0, 0));

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
        public ValueTask PositionFixed(string contentId, string referenceId, string align = "start", bool matchWidth = false, string side = "bottom") => ValueTask.CompletedTask;
        public ValueTask UnpositionFixed(string contentId) => ValueTask.CompletedTask;
        public ValueTask<ElementRect?> GetElementRect(string elementId) => ValueTask.FromResult<ElementRect?>(null);
        public ValueTask<double> GetElementDimension(string elementId, string dimension) => ValueTask.FromResult(0d);
        public ValueTask<double> GetScrollTop(string elementId) => ValueTask.FromResult(0d);
        public ValueTask<double> WheelScrollTop(ElementReference element) => ValueTask.FromResult(0d);
        public ValueTask WheelScrollTo(ElementReference element, double top) => ValueTask.CompletedTask;
        public ValueTask SetPointerCaptureOnElement(string elementId, long pointerId) => ValueTask.CompletedTask;
        public ValueTask ReleasePointerCaptureOnElement(string elementId, long pointerId) => ValueTask.CompletedTask;
        public ValueTask RegisterDrawerSwipe(string elementId, string direction, Func<Task> handler) => ValueTask.CompletedTask;
        public ValueTask RegisterDrawerSwipe(string elementId, Func<Task> handler) => ValueTask.CompletedTask;
        public ValueTask UnregisterDrawerSwipe(string elementId) => ValueTask.CompletedTask;
        public ValueTask RegisterTabSwipe(string elementId, bool wrap, Func<string, Task> handler) => ValueTask.CompletedTask;
        public ValueTask UnregisterTabSwipe(string elementId) => ValueTask.CompletedTask;
        public ValueTask RegisterSortableTouch(string containerId, Func<int, int, Task> handler) => ValueTask.CompletedTask;
        public ValueTask UnregisterSortableTouch(string containerId) => ValueTask.CompletedTask;
        public ValueTask RegisterCarouselSwipe(string elementId, string orientation, Func<string, Task> swipeHandler, Func<double, double, int, Task> scrollHandler) => ValueTask.CompletedTask;
        public ValueTask UnregisterCarouselSwipe(string elementId) => ValueTask.CompletedTask;
        public ValueTask CarouselScrollTo(string elementId, int index, string behavior = "smooth") => ValueTask.CompletedTask;
        public ValueTask RegisterHorizontalSwipe(string elementId, Func<string, Task> handler) => ValueTask.CompletedTask;
        public ValueTask UnregisterHorizontalSwipe(string elementId) => ValueTask.CompletedTask;
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
        public ValueTask RegisterColumnResize(string handleId, double minWidth, double? maxWidth, Func<double, Task> commitHandler) => ValueTask.CompletedTask;
        public ValueTask UnregisterColumnResize(string handleId) => ValueTask.CompletedTask;
        public ValueTask CaptureColumnRects(string gridId) => ValueTask.CompletedTask;
        public ValueTask AnimateColumnReorder(string gridId, int durationMs) => ValueTask.CompletedTask;
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
        public ValueTask Vibrate(int milliseconds) => ValueTask.CompletedTask;
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
        public ValueTask<Lumeo.Services.MediaState> GetMediaState(Microsoft.AspNetCore.Components.ElementReference element) => ValueTask.FromResult(new Lumeo.Services.MediaState(0, 0));
        public ValueTask SignaturePadInit(string elementId, object options, Microsoft.JSInterop.DotNetObjectReference<Lumeo.SignaturePad> dotNetRef) => ValueTask.CompletedTask;
        public ValueTask SignaturePadClear(string elementId) => ValueTask.CompletedTask;
        public ValueTask<string?> SignaturePadDataUrl(string elementId, string mimeType) => ValueTask.FromResult<string?>(null);
        public ValueTask SignaturePadSetStrokeStyle(string elementId, string color, double width) => ValueTask.CompletedTask;
        public ValueTask SignaturePadSetDisabled(string elementId, bool disabled) => ValueTask.CompletedTask;
        public ValueTask SignaturePadLoadDataUrl(string elementId, string? dataUrl) => ValueTask.CompletedTask;
        public ValueTask SignaturePadDestroy(string elementId) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public void Dispose() { }
    }
}
