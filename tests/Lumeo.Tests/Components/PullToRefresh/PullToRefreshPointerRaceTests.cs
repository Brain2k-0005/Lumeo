using Bunit;
using Lumeo.Services;
using Lumeo.Services.Interop;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Xunit;

namespace Lumeo.Tests.Components.PullToRefresh;

/// <summary>
/// Battle-test #116 (medium, lifecycle) — "Fast tap wedges the component:
/// pointerup during the GetScrollTop await latches _activePointerId forever".
///
/// HandlePointerDown gates the pull on <c>scrollTop == 0</c> by awaiting
/// <c>Interop.GetScrollTop</c>. The buggy version set <c>_activePointerId</c>
/// only AFTER that await resolved. A fast tap fires pointerup while the await is
/// still in flight; because HandlePointerUp gates on <c>_activePointerId</c>
/// (still null at that instant), the up is silently dropped. The await then
/// resumes and latches <c>_activePointerId</c> — but no further pointerup will
/// ever arrive for that pointer, so the id stays set forever and every later
/// pointerdown bails at <c>if (_activePointerId.HasValue) return;</c>. The
/// component is wedged: no subsequent pull can ever fire OnRefresh.
///
/// The fix claims <c>_activePointerId</c> synchronously BEFORE the await (so the
/// racing up observes and clears the gesture) and re-checks after the await that
/// the gesture is still ours before committing.
///
/// bUnit can't drive real touch, but it CAN park an async event handler: we gate
/// <see cref="GatedScrollTopInterop.GetScrollTop"/> on a TaskCompletionSource so
/// pointerdown's handler suspends mid-await. We then fire the racing pointerup,
/// release the gate, and assert the component is NOT wedged — a fresh full pull
/// still fires OnRefresh. Without the fix the second pull's pointerdown bails and
/// OnRefresh never fires, so this test fails; with the fix it passes.
/// </summary>
public class PullToRefreshPointerRaceTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly GatedScrollTopInterop _interop = new();

    public PullToRefreshPointerRaceTests()
    {
        _ctx.AddLumeoServices();
        // Last interface registration wins, so PullToRefresh resolves the spy.
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public async Task PointerUp_Racing_The_ScrollTop_Await_Does_Not_Wedge_The_Component()
    {
        var fired = 0;
        var cb = EventCallback.Factory.Create(this, () => fired++);
        var cut = _ctx.Render<Lumeo.PullToRefresh>(p => p
            .Add(c => c.OnRefresh, cb)
            .Add(c => c.ThresholdPx, 80)
            .AddChildContent("<p>content</p>"));

        var root = cut.Find("div[id^='ptr']");

        // --- The fast-tap race ---
        // Park HandlePointerDown's GetScrollTop await, then fire pointerup for the
        // SAME pointer while the down is still suspended.
        _interop.GateScrollTop = true;
        var down = root.PointerDownAsync(new PointerEventArgs { PointerId = 1, ClientY = 0 });
        _interop.WaitForScrollTopEntered();

        // pointerup arrives while the down is parked mid-await.
        await root.PointerUpAsync(new PointerEventArgs { PointerId = 1, ClientY = 0 });

        // Now let the parked GetScrollTop resolve so the down handler completes.
        _interop.ReleaseScrollTop();
        await down;

        // --- The component must still be usable (not wedged) ---
        // A fresh full pull past the threshold must fire OnRefresh exactly once.
        // If _activePointerId latched during the race, this pointerdown bails at
        // the `if (_activePointerId.HasValue) return;` guard and nothing fires.
        root.PointerDown(new PointerEventArgs { PointerId = 2, ClientY = 0 });
        root.PointerMove(new PointerEventArgs { PointerId = 2, ClientY = 200 });
        await root.PointerUpAsync(new PointerEventArgs { PointerId = 2, ClientY = 200 });

        Assert.Equal(1, fired);
    }

    /// <summary>
    /// A test interop double (modelled on Affix's GatedAffixInterop) that can PARK
    /// <c>GetScrollTop</c> on a gate so the test can fire a racing pointerup while
    /// PullToRefresh's pointerdown is still suspended on that await. Every other
    /// member forwards to an inner <see cref="TrackingInteropService"/>.
    /// </summary>
    private sealed class GatedScrollTopInterop : IComponentInteropService
    {
        private readonly TrackingInteropService _inner = new();
        private TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>When true, GetScrollTop signals it was entered then parks on the gate.</summary>
        public bool GateScrollTop { get; set; }

        /// <summary>Blocks until a gated GetScrollTop call has actually been entered.</summary>
        public void WaitForScrollTopEntered() => _entered.Task.GetAwaiter().GetResult();

        /// <summary>Releases the parked GetScrollTop and disarms the gate.</summary>
        public void ReleaseScrollTop()
        {
            GateScrollTop = false;
            _gate.TrySetResult();
        }

        public async ValueTask<double> GetScrollTop(string elementId)
        {
            if (GateScrollTop)
            {
                _entered.TrySetResult();
                await _gate.Task;
            }
            // scrollTop == 0 keeps the pull gate open (same as the no-op tracker).
            return 0.0;
        }

        // ---- Everything else forwards to the shared no-op tracker ----
        public ValueTask RegisterAffix(string elementId, int offsetTop, int? offsetBottom, string? target, Func<bool, Task> handler) => _inner.RegisterAffix(elementId, offsetTop, offsetBottom, target, handler);
        public ValueTask UnregisterAffix(string elementId) => _inner.UnregisterAffix(elementId);
        public ValueTask RegisterClickOutside(string elementId, string? triggerElementId, Func<Task> handler) => _inner.RegisterClickOutside(elementId, triggerElementId, handler);
        public ValueTask UnregisterClickOutside(string elementId) => _inner.UnregisterClickOutside(elementId);
        public ValueTask FocusElement(string elementId) => _inner.FocusElement(elementId);
        public ValueTask FocusMenuItemByIndex(string containerId, int index) => _inner.FocusMenuItemByIndex(containerId, index);
        public ValueTask<int> GetMenuItemCount(string containerId) => _inner.GetMenuItemCount(containerId);
        public ValueTask<int> FocusMenuItemByTypeahead(string containerId, string query, int currentIndex) => _inner.FocusMenuItemByTypeahead(containerId, query, currentIndex);
        public ValueTask LockScroll() => _inner.LockScroll();
        public ValueTask UnlockScroll() => _inner.UnlockScroll();
        public ValueTask SetHtmlClass(string className, bool active) => _inner.SetHtmlClass(className, active);
        public ValueTask SetupFocusTrap(string elementId, string? initialFocusSelector = null) => _inner.SetupFocusTrap(elementId, initialFocusSelector);
        public ValueTask RemoveFocusTrap(string elementId) => _inner.RemoveFocusTrap(elementId);
        public ValueTask SaveFocus(string key) => _inner.SaveFocus(key);
        public ValueTask RestoreFocus(string key) => _inner.RestoreFocus(key);
        public ValueTask AttachOverlaySlideEnd(string elementId) => _inner.AttachOverlaySlideEnd(elementId);
        public ValueTask RegisterSvDrag(string elementId, Func<double, double, Task> handler) => _inner.RegisterSvDrag(elementId, handler);
        public ValueTask UnregisterSvDrag(string elementId) => _inner.UnregisterSvDrag(elementId);
        public ValueTask RegisterPinchZoom(string elementId, Func<double, Task> handler) => _inner.RegisterPinchZoom(elementId, handler);
        public ValueTask UnregisterPinchZoom(string elementId) => _inner.UnregisterPinchZoom(elementId);
        public ValueTask<ViewportSize> GetViewportSize() => _inner.GetViewportSize();
        public ValueTask<ViewportSize?> RegisterViewportListener(DotNetObjectReference<ResponsiveService> dotnetRef) => _inner.RegisterViewportListener(dotnetRef);
        public ValueTask UnregisterViewportListener() => _inner.UnregisterViewportListener();
        public ValueTask<string> PositionFixed(string contentId, string referenceId, string align = "start", bool matchWidth = false, string side = "bottom") => _inner.PositionFixed(contentId, referenceId, align, matchWidth, side);
        public ValueTask UnpositionFixed(string contentId) => _inner.UnpositionFixed(contentId);
        public ValueTask PositionAtPoint(string contentId, double x, double y) => _inner.PositionAtPoint(contentId, x, y);
        public ValueTask InitToolbarRoving(string toolbarId) => _inner.InitToolbarRoving(toolbarId);
        public ValueTask MoveToolbarFocus(string toolbarId, int delta) => _inner.MoveToolbarFocus(toolbarId, delta);
        public ValueTask FocusToolbarEdge(string toolbarId, bool last) => _inner.FocusToolbarEdge(toolbarId, last);
        public ValueTask<ElementRect?> GetElementRect(string elementId) => _inner.GetElementRect(elementId);
        public ValueTask<double> GetElementDimension(string elementId, string dimension) => _inner.GetElementDimension(elementId, dimension);
        public ValueTask RegisterPullToRefresh(string elementId) => _inner.RegisterPullToRefresh(elementId);
        public ValueTask UnregisterPullToRefresh(string elementId) => _inner.UnregisterPullToRefresh(elementId);
        public ValueTask<double> WheelScrollTop(ElementReference element) => _inner.WheelScrollTop(element);
        public ValueTask WheelScrollTo(ElementReference element, double top) => _inner.WheelScrollTo(element, top);
        public ValueTask SetPointerCaptureOnElement(string elementId, long pointerId) => _inner.SetPointerCaptureOnElement(elementId, pointerId);
        public ValueTask ReleasePointerCaptureOnElement(string elementId, long pointerId) => _inner.ReleasePointerCaptureOnElement(elementId, pointerId);
        public ValueTask RegisterDrawerSwipe(string elementId, string direction, Func<Task> handler) => _inner.RegisterDrawerSwipe(elementId, direction, handler);
        public ValueTask RegisterDrawerSwipe(string elementId, Func<Task> handler) => _inner.RegisterDrawerSwipe(elementId, handler);
        public ValueTask UnregisterDrawerSwipe(string elementId) => _inner.UnregisterDrawerSwipe(elementId);
        public ValueTask RegisterTabSwipe(string elementId, bool wrap, Func<string, Task> handler) => _inner.RegisterTabSwipe(elementId, wrap, handler);
        public ValueTask UnregisterTabSwipe(string elementId) => _inner.UnregisterTabSwipe(elementId);
        public ValueTask RegisterSortableTouch(string containerId, Func<int, int, Task> handler) => _inner.RegisterSortableTouch(containerId, handler);
        public ValueTask UnregisterSortableTouch(string containerId) => _inner.UnregisterSortableTouch(containerId);
        public ValueTask RegisterCarouselSwipe(string elementId, string orientation, Func<string, Task> swipeHandler, Func<double, double, int, Task> scrollHandler) => _inner.RegisterCarouselSwipe(elementId, orientation, swipeHandler, scrollHandler);
        public ValueTask UnregisterCarouselSwipe(string elementId) => _inner.UnregisterCarouselSwipe(elementId);
        public ValueTask CarouselScrollTo(string elementId, int index, string behavior = "smooth") => _inner.CarouselScrollTo(elementId, index, behavior);
        public ValueTask RegisterHorizontalSwipe(string elementId, Func<string, Task> handler) => _inner.RegisterHorizontalSwipe(elementId, handler);
        public ValueTask UnregisterHorizontalSwipe(string elementId) => _inner.UnregisterHorizontalSwipe(elementId);
        public ValueTask RegisterGallerySwipe(string elementId, Func<string, Task> handler) => _inner.RegisterGallerySwipe(elementId, handler);
        public ValueTask UnregisterGallerySwipe(string elementId) => _inner.UnregisterGallerySwipe(elementId);
        public ValueTask RegisterResizeHandle(string elementId, string direction, Func<double, Task> resizeHandler, Func<Task> resizeEndHandler) => _inner.RegisterResizeHandle(elementId, direction, resizeHandler, resizeEndHandler);
        public ValueTask UnregisterResizeHandle(string elementId) => _inner.UnregisterResizeHandle(elementId);
        public ValueTask RegisterScrollspy(string containerId, int offset, bool smooth, Func<string?, Task> handler) => _inner.RegisterScrollspy(containerId, offset, smooth, handler);
        public ValueTask UnregisterScrollspy(string containerId) => _inner.UnregisterScrollspy(containerId);
        public ValueTask ScrollspyScrollTo(string containerId, string sectionId, bool smooth) => _inner.ScrollspyScrollTo(containerId, sectionId, smooth);
        public ValueTask RegisterToastSwipe(string elementId, string toastId, Func<string, Task> handler) => _inner.RegisterToastSwipe(elementId, toastId, handler);
        public ValueTask UnregisterToastSwipe(string toastId, string elementId) => _inner.UnregisterToastSwipe(toastId, elementId);
        public ValueTask SetupAutoResize(string elementId, int maxRows) => _inner.SetupAutoResize(elementId, maxRows);
        public ValueTask UnregisterAutoResize(string elementId) => _inner.UnregisterAutoResize(elementId);
        public ValueTask RegisterOtpPaste(string baseId, int length, Func<string, Task> handler) => _inner.RegisterOtpPaste(baseId, length, handler);
        public ValueTask UnregisterOtpPaste(string baseId, int length) => _inner.UnregisterOtpPaste(baseId, length);
        public ValueTask RegisterPreventDefaultKeys(string elementId, IReadOnlyList<PreventDefaultKeyRule> rules) => _inner.RegisterPreventDefaultKeys(elementId, rules);
        public ValueTask UnregisterPreventDefaultKeys(string elementId) => _inner.UnregisterPreventDefaultKeys(elementId);
        public ValueTask RegisterColumnResize(string handleId, double minWidth, double? maxWidth, Func<double, bool, Task> commitHandler) => _inner.RegisterColumnResize(handleId, minWidth, maxWidth, commitHandler);
        public ValueTask NudgeColumnResize(string handleId, double delta) => _inner.NudgeColumnResize(handleId, delta);
        public ValueTask RegisterColumnReorder(string gridId, Func<string, string, Task> commitHandler) => _inner.RegisterColumnReorder(gridId, commitHandler);
        public ValueTask UnregisterColumnReorder(string gridId) => _inner.UnregisterColumnReorder(gridId);
        public ValueTask UnregisterColumnResize(string handleId) => _inner.UnregisterColumnResize(handleId);
        public ValueTask CaptureColumnRects(string gridId) => _inner.CaptureColumnRects(gridId);
        public ValueTask AnimateColumnReorder(string gridId, int durationMs) => _inner.AnimateColumnReorder(gridId, durationMs);
        public ValueTask RegisterRowReorder(string gridId, Func<int, int, Task> commitHandler) => _inner.RegisterRowReorder(gridId, commitHandler);
        public ValueTask UnregisterRowReorder(string gridId) => _inner.UnregisterRowReorder(gridId);
        public ValueTask CaptureRowRects(string gridId) => _inner.CaptureRowRects(gridId);
        public ValueTask AnimateRowReorder(string gridId, int durationMs) => _inner.AnimateRowReorder(gridId, durationMs);
        public ValueTask<ElementRect?> GetElementRectBySelector(string selector) => _inner.GetElementRectBySelector(selector);
        public ValueTask ScrollSelectorIntoView(string selector) => _inner.ScrollSelectorIntoView(selector);
        public ValueTask ScrollIntoView(string elementId, string block = "nearest") => _inner.ScrollIntoView(elementId, block);
        public ValueTask<ComponentInteropService.TextareaCaretInfo> GetTextareaCaretPosition(string elementId) => _inner.GetTextareaCaretPosition(elementId);
        public ValueTask<int> GetInputCaret(string elementId) => _inner.GetInputCaret(elementId);
        public ValueTask SetInputCaret(string elementId, int position) => _inner.SetInputCaret(elementId, position);
        public ValueTask SetInputValue(string elementId, string value) => _inner.SetInputValue(elementId, value);
        public ValueTask<ComponentInteropService.TabMeasurement?> TabsMeasure(string elementId) => _inner.TabsMeasure(elementId);
        public ValueTask RegisterBackToTop(string id, int threshold, Func<bool, Task> handler) => _inner.RegisterBackToTop(id, threshold, handler);
        public ValueTask UnregisterBackToTop(string id) => _inner.UnregisterBackToTop(id);
        public ValueTask ScrollToTop() => _inner.ScrollToTop();
        public ValueTask DownloadFile(string fileName, string contentBase64, string mimeType = "application/octet-stream") => _inner.DownloadFile(fileName, contentBase64, mimeType);
        public ValueTask CopyToClipboard(string text) => _inner.CopyToClipboard(text);
        public ValueTask RippleAttachAsync(ElementReference element) => _inner.RippleAttachAsync(element);
        public ValueTask RippleDetachAsync(ElementReference element) => _inner.RippleDetachAsync(element);
        public ValueTask ResetFileInput(ElementReference element) => _inner.ResetFileInput(element);
        public ValueTask<bool> PrefersReducedMotion() => _inner.PrefersReducedMotion();
        public ValueTask<RipplePoint> TouchRippleCoords(string hostElementId, double clientX, double clientY) => _inner.TouchRippleCoords(hostElementId, clientX, clientY);
        public ValueTask PlayMedia(ElementReference element) => _inner.PlayMedia(element);
        public ValueTask PauseMedia(ElementReference element) => _inner.PauseMedia(element);
        public ValueTask SetMediaVolume(ElementReference element, double volume, bool muted) => _inner.SetMediaVolume(element, volume, muted);
        public ValueTask SeekMedia(ElementReference element, double seconds) => _inner.SeekMedia(element, seconds);
        public ValueTask<MediaState> GetMediaState(ElementReference element) => _inner.GetMediaState(element);
        public ValueTask Vibrate(int milliseconds) => _inner.Vibrate(milliseconds);
        public ValueTask SaveToLocalStorage(string key, string value) => _inner.SaveToLocalStorage(key, value);
        public ValueTask<string?> LoadFromLocalStorage(string key) => _inner.LoadFromLocalStorage(key);
        public ValueTask RemoveFromLocalStorage(string key) => _inner.RemoveFromLocalStorage(key);
        public ValueTask<bool> MotionPrefersReducedMotion() => _inner.MotionPrefersReducedMotion();
        public ValueTask MotionTickNumber(string elementId, double from, double to, int durationMs, int decimals, string separator = ",", string decimalSeparator = ".") => _inner.MotionTickNumber(elementId, from, to, durationMs, decimals, separator, decimalSeparator);
        public ValueTask MotionDisposeTicker(string elementId) => _inner.MotionDisposeTicker(elementId);
        public ValueTask MotionRevealText(string elementId, int staggerMs, double threshold) => _inner.MotionRevealText(elementId, staggerMs, threshold);
        public ValueTask MotionBlurFade(string elementId, int delayMs, bool once, bool forceHidden = false) => _inner.MotionBlurFade(elementId, delayMs, once, forceHidden);
        public ValueTask MotionDisposeObserver(string elementId) => _inner.MotionDisposeObserver(elementId);
        public ValueTask MotionAnimatedBeam(string elementId, string fromId, string toId, object options) => _inner.MotionAnimatedBeam(elementId, fromId, toId, options);
        public ValueTask MotionDisposeAnimatedBeam(string elementId) => _inner.MotionDisposeAnimatedBeam(elementId);
        public ValueTask MotionDock(string elementId, object options) => _inner.MotionDock(elementId, options);
        public ValueTask MotionDisposeDock(string elementId) => _inner.MotionDisposeDock(elementId);
        public ValueTask MotionConfettiInit(string elementId) => _inner.MotionConfettiInit(elementId);
        public ValueTask MotionConfettiFire(string elementId, object options) => _inner.MotionConfettiFire(elementId, options);
        public ValueTask MotionDisposeConfetti(string elementId) => _inner.MotionDisposeConfetti(elementId);
        public ValueTask AiAutosize(string elementId, int maxPx) => _inner.AiAutosize(elementId, maxPx);
        public ValueTask AiObserveAutoScroll(string elementId) => _inner.AiObserveAutoScroll(elementId);
        public ValueTask AiDisposeAutoScroll(string elementId) => _inner.AiDisposeAutoScroll(elementId);
        public ValueTask AiScrollToBottom(string elementId) => _inner.AiScrollToBottom(elementId);
        public Task<string> SchedulerInitAsync(ElementReference el, object dotNetRef, object options) => _inner.SchedulerInitAsync(el, dotNetRef, options);
        public Task SchedulerSetEventsAsync(string id, IEnumerable<object> events) => _inner.SchedulerSetEventsAsync(id, events);
        public Task SchedulerChangeViewAsync(string id, string view) => _inner.SchedulerChangeViewAsync(id, view);
        public Task SchedulerGotoDateAsync(string id, string dateIso) => _inner.SchedulerGotoDateAsync(id, dateIso);
        public Task SchedulerPrevAsync(string id) => _inner.SchedulerPrevAsync(id);
        public Task SchedulerNextAsync(string id) => _inner.SchedulerNextAsync(id);
        public Task SchedulerTodayAsync(string id) => _inner.SchedulerTodayAsync(id);
        public Task<string> SchedulerGetTitleAsync(string id) => _inner.SchedulerGetTitleAsync(id);
        public Task SchedulerDestroyAsync(string id) => _inner.SchedulerDestroyAsync(id);
        public Task<string> GanttInitAsync(ElementReference el, object dotNetRef, object options) => _inner.GanttInitAsync(el, dotNetRef, options);
        public Task GanttSetTasksAsync(string id, IEnumerable<object> tasks) => _inner.GanttSetTasksAsync(id, tasks);
        public Task GanttChangeViewModeAsync(string id, string mode) => _inner.GanttChangeViewModeAsync(id, mode);
        public Task GanttDestroyAsync(string id) => _inner.GanttDestroyAsync(id);
        public ValueTask RegisterToolbarOverflow(string elementId, Func<int, int, Task> handler) => _inner.RegisterToolbarOverflow(elementId, handler);
        public ValueTask UnregisterToolbarOverflow(string elementId) => _inner.UnregisterToolbarOverflow(elementId);
        public ValueTask<string> RichTextInitAsync<T>(ElementReference elementRef, DotNetObjectReference<T> dotNetRef, object options) where T : class => _inner.RichTextInitAsync(elementRef, dotNetRef, options);
        public ValueTask RichTextSetContentAsync(string id, string? html) => _inner.RichTextSetContentAsync(id, html);
        public ValueTask RichTextCommandAsync(string id, string name, params object?[]? args) => _inner.RichTextCommandAsync(id, name, args);
        public ValueTask<RichTextActiveState?> RichTextGetActiveAsync(string id) => _inner.RichTextGetActiveAsync(id);
        public ValueTask RichTextSetDisabledAsync(string id, bool disabled) => _inner.RichTextSetDisabledAsync(id, disabled);
        public ValueTask RichTextDestroyAsync(string id) => _inner.RichTextDestroyAsync(id);
        public ValueTask<string?> RichTextPromptLinkAsync(string? initial) => _inner.RichTextPromptLinkAsync(initial);
        public ValueTask RichTextSetAriaAttributesAsync(string id, bool ariaInvalid, string? ariaDescribedBy) => _inner.RichTextSetAriaAttributesAsync(id, ariaInvalid, ariaDescribedBy);
        public ValueTask SignaturePadInit<T>(string elementId, object options, DotNetObjectReference<T> dotNetRef) where T : class => _inner.SignaturePadInit(elementId, options, dotNetRef);
        public ValueTask SignaturePadClear(string elementId) => _inner.SignaturePadClear(elementId);
        public ValueTask<string?> SignaturePadDataUrl(string elementId, string mimeType) => _inner.SignaturePadDataUrl(elementId, mimeType);
        public ValueTask SignaturePadSetStrokeStyle(string elementId, string color, double width) => _inner.SignaturePadSetStrokeStyle(elementId, color, width);
        public ValueTask SignaturePadSetDisabled(string elementId, bool disabled) => _inner.SignaturePadSetDisabled(elementId, disabled);
        public ValueTask SignaturePadLoadDataUrl(string elementId, string? dataUrl) => _inner.SignaturePadLoadDataUrl(elementId, dataUrl);
        public ValueTask SignaturePadDestroy(string elementId) => _inner.SignaturePadDestroy(elementId);

        public void Dispose() { _gate.TrySetResult(); _inner.Dispose(); }
        public ValueTask DisposeAsync() { _gate.TrySetResult(); return _inner.DisposeAsync(); }
    }
}
