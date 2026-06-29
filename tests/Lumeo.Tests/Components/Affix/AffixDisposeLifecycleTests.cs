using Bunit;
using Lumeo.Services;
using Lumeo.Services.Interop;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Xunit;

namespace Lumeo.Tests.Components.Affix;

/// <summary>
/// Triage #96 (medium, lifecycle) — "Dispose during in-flight registration leaks the JS
/// scroll/resize listeners and the affixHandlers Map entry (_registered still false)".
///
/// The component registers its sticky watcher asynchronously in OnAfterRenderAsync
/// (<c>await Interop.RegisterAffix(...)</c>) and only sets its private <c>_registered</c>
/// flag AFTER that await resolves. The original teardown gated on that flag
/// (<c>if (_registered) { … UnregisterAffix … }</c>). So a dispose that lands while the
/// registration is still in flight saw <c>_registered == false</c> and skipped teardown
/// entirely — even though the JS side had already attached its scroll/resize listeners and
/// created the <c>affixHandlers</c> Map entry. Those leaked.
///
/// bUnit can't drive the real JS watcher, so the testable seam is the .NET wiring: teardown
/// must be unconditional/idempotent (UnregisterAffix is a safe no-op for an unknown id) so an
/// in-flight dispose still tears the leak down. We reproduce "registration in flight" with a
/// <see cref="GatedAffixInterop"/> whose <c>RegisterAffix</c> blocks on a TaskCompletionSource
/// the test releases manually, letting us dispose the component BEFORE the await resolves.
/// </summary>
public class AffixDisposeLifecycleTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly GatedAffixInterop _interop = new();

    public AffixDisposeLifecycleTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public async Task Dispose_During_In_Flight_Registration_Still_Unregisters()
    {
        // Hold RegisterAffix so the first render's registration never completes synchronously.
        _interop.GateRegistration = true;

        var cut = _ctx.Render<Lumeo.Affix>(p => p.AddChildContent("Sticky"));
        var id = cut.Find("div").GetAttribute("id");
        Assert.False(string.IsNullOrWhiteSpace(id));

        // RegisterAffix was entered but is parked on the gate; _registered is still false.
        Assert.Single(_interop.AffixRegistrations);
        Assert.Empty(_interop.AffixUnregistrations);

        // Dispose WHILE registration is in flight. The old (buggy) DisposeAsync gated on
        // _registered (still false here) and skipped UnregisterAffix, leaking the listeners.
        await cut.Instance.DisposeAsync();

        // Now let the parked RegisterAffix resolve — the component must notice it was disposed
        // mid-flight and unregister the just-wired listeners.
        _interop.ReleaseRegistration();

        // The leak must be torn down: an UnregisterAffix for this id must have been recorded
        // (either from DisposeAsync itself or from the in-flight reconcile after the await).
        Assert.Contains(id, _interop.AffixUnregistrations);
    }

    [Fact]
    public async Task Double_Dispose_Does_Not_Throw()
    {
        var cut = _ctx.Render<Lumeo.Affix>(p => p.AddChildContent("Sticky"));

        var ex = await Record.ExceptionAsync(async () =>
        {
            await cut.Instance.DisposeAsync();
            await cut.Instance.DisposeAsync();
        });

        Assert.Null(ex);
    }

    [Fact]
    public async Task Affix_Callback_After_Dispose_Does_Not_Throw()
    {
        var fireCount = 0;
        var cut = _ctx.Render<Lumeo.Affix>(p => p
            .Add(a => a.OnChange, _ => fireCount++)
            .AddChildContent("Sticky"));

        var registration = Assert.Single(_interop.AffixRegistrations);

        await cut.Instance.DisposeAsync();

        // The JS watcher can report one more stick/unstick between dispose and full teardown.
        // Invoking the captured handler after dispose must be a guarded no-op, never a throw
        // (e.g. an ObjectDisposedException from StateHasChanged on a torn-down component).
        var ex = await Record.ExceptionAsync(() => registration.Handler(true));

        Assert.Null(ex);
        Assert.Equal(0, fireCount); // post-dispose callback is suppressed
    }

    /// <summary>
    /// A test interop double that records Affix register/unregister calls (like
    /// <see cref="TrackingInteropService"/>) but can PARK <c>RegisterAffix</c> on a gate so the
    /// test can dispose the component while the registration is still awaiting. Every other
    /// member forwards to an inner <see cref="TrackingInteropService"/> so the rest of the
    /// interface behaves exactly as the shared no-op tracker — only the Affix path is special.
    /// </summary>
    private sealed class GatedAffixInterop : IComponentInteropService
    {
        private readonly TrackingInteropService _inner = new();
        private readonly List<(string ElementId, int OffsetTop, int? OffsetBottom, string? Target, Func<bool, Task> Handler)> _registrations = new();
        private readonly List<string> _unregistrations = new();
        private TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>When true, RegisterAffix records the call then parks on the gate.</summary>
        public bool GateRegistration { get; set; }

        public IReadOnlyList<(string ElementId, int OffsetTop, int? OffsetBottom, string? Target, Func<bool, Task> Handler)> AffixRegistrations => _registrations;
        public IReadOnlyList<string> AffixUnregistrations => _unregistrations;

        public void ReleaseRegistration() => _gate.TrySetResult();

        public async ValueTask RegisterAffix(string elementId, int offsetTop, int? offsetBottom, string? target, Func<bool, Task> handler)
        {
            _registrations.Add((elementId, offsetTop, offsetBottom, target, handler));
            if (GateRegistration)
                await _gate.Task;
        }

        public ValueTask UnregisterAffix(string elementId)
        {
            _unregistrations.Add(elementId);
            return ValueTask.CompletedTask;
        }

        // ---- Everything else forwards to the shared no-op tracker ----
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
        public ValueTask PositionFixed(string contentId, string referenceId, string align = "start", bool matchWidth = false, string side = "bottom") => _inner.PositionFixed(contentId, referenceId, align, matchWidth, side);
        public ValueTask UnpositionFixed(string contentId) => _inner.UnpositionFixed(contentId);
        public ValueTask PositionAtPoint(string contentId, double x, double y) => _inner.PositionAtPoint(contentId, x, y);
        public ValueTask InitToolbarRoving(string toolbarId) => _inner.InitToolbarRoving(toolbarId);
        public ValueTask MoveToolbarFocus(string toolbarId, int delta) => _inner.MoveToolbarFocus(toolbarId, delta);
        public ValueTask FocusToolbarEdge(string toolbarId, bool last) => _inner.FocusToolbarEdge(toolbarId, last);
        public ValueTask<ElementRect?> GetElementRect(string elementId) => _inner.GetElementRect(elementId);
        public ValueTask<double> GetElementDimension(string elementId, string dimension) => _inner.GetElementDimension(elementId, dimension);
        public ValueTask<double> GetScrollTop(string elementId) => _inner.GetScrollTop(elementId);
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
        public ValueTask RegisterColumnResize(string handleId, double minWidth, double? maxWidth, Func<double, Task> commitHandler) => _inner.RegisterColumnResize(handleId, minWidth, maxWidth, commitHandler);
        public ValueTask UnregisterColumnResize(string handleId) => _inner.UnregisterColumnResize(handleId);
        public ValueTask CaptureColumnRects(string gridId) => _inner.CaptureColumnRects(gridId);
        public ValueTask AnimateColumnReorder(string gridId, int durationMs) => _inner.AnimateColumnReorder(gridId, durationMs);
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
