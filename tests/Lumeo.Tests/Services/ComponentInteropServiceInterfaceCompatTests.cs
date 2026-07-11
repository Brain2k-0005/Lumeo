using System.Collections.Generic;
using Lumeo.Services;
using Lumeo.Services.Interop;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Xunit;

namespace Lumeo.Tests.Services;

/// <summary>
/// Round-9 P2 — additive-evolution guard for <see cref="IComponentInteropService"/>'s
/// DataGrid column-resize/reorder + row-reorder surface.
///
/// A prior round widened <c>RegisterColumnResize</c>'s existing abstract member in
/// place (its <c>commitHandler</c> delegate grew an <c>autoFit</c> bool parameter)
/// and added the whole pointer-based column/row reorder surface
/// (<c>RegisterColumnReorder</c>, <c>UnregisterColumnReorder</c>,
/// <c>NudgeColumnResize</c>, <c>ClearColumnReorderTransforms</c>,
/// <c>RegisterRowReorder</c>, <c>UnregisterRowReorder</c>, <c>CaptureRowRects</c>,
/// <c>AnimateRowReorder</c>, <c>ClearRowReorderTransforms</c>) as new ABSTRACT
/// members — breaking any external <see cref="IComponentInteropService"/>
/// implementer/test double the moment it updated, even one that never touches
/// DataGrid. The fix restores <c>RegisterColumnResize</c>'s original 4-parameter
/// (non-autoFit) shape as the abstract contract and exposes every new member
/// (including the autoFit-aware <c>RegisterColumnResize</c> overload) as an
/// additive default interface member (mirrors <see cref="IKeyboardShortcutService"/>'s
/// DIM convention — see <c>KeyboardShortcutServiceInterfaceCompatTests</c>).
///
/// This test pins that contract: a "legacy" implementer that only knows the
/// pre-round-8 shape (old <c>RegisterColumnResize</c>, plus the column-reorder
/// FLIP pair that predates this whole feature) must (a) still satisfy the
/// interface — if any of the new members were abstract this file would not
/// compile — and (b) transparently answer every new member through its DIM
/// default (no-op, or delegating to the legacy member) without throwing.
/// </summary>
public class ComponentInteropServiceInterfaceCompatTests
{
    // Implements ONLY the members that existed before round-8/9's DataGrid
    // resize+reorder additions — no knowledge of autoFit, pointer reorder, or
    // row reorder. Every other interface member forwards to an inner
    // TrackingInteropService (like AffixDisposeLifecycleTests' GatedAffixInterop)
    // so the rest of the surface behaves like the shared no-op tracker.
    private sealed class LegacyPreReorderInterop : IComponentInteropService
    {
        private readonly TrackingInteropService _inner = new();

        public int RegisterColumnResizeCallCount;

        // The interface's restored original (non-autoFit) abstract member —
        // the only column-resize shape this "legacy" double ever knew about.
        public ValueTask RegisterColumnResize(string handleId, double minWidth, double? maxWidth, Func<double, Task> commitHandler)
        {
            RegisterColumnResizeCallCount++;
            _ = commitHandler; // captured only to prove wiring; not invoked by this test
            return ValueTask.CompletedTask;
        }
        public ValueTask UnregisterColumnResize(string handleId) => ValueTask.CompletedTask;

        // Pre-existing column-reorder FLIP pair (predates round-8's pointer engine).
        public ValueTask CaptureColumnRects(string gridId) => ValueTask.CompletedTask;
        public ValueTask AnimateColumnReorder(string gridId, int durationMs) => ValueTask.CompletedTask;

        // ---- Everything else forwards to the shared no-op tracker ----
        public ValueTask RegisterClickOutside(string elementId, string? triggerElementId, Func<Task> handler) => _inner.RegisterClickOutside(elementId, triggerElementId, handler);
        public ValueTask UnregisterClickOutside(string elementId) => _inner.UnregisterClickOutside(elementId);
        public ValueTask FocusElement(string elementId) => _inner.FocusElement(elementId);
        public ValueTask FocusMenuItemByIndex(string containerId, int index) => _inner.FocusMenuItemByIndex(containerId, index);
        public ValueTask<int> GetMenuItemCount(string containerId) => _inner.GetMenuItemCount(containerId);
        public ValueTask LockScroll() => _inner.LockScroll();
        public ValueTask UnlockScroll() => _inner.UnlockScroll();
        public ValueTask SetHtmlClass(string className, bool active) => _inner.SetHtmlClass(className, active);
        public ValueTask SetupFocusTrap(string elementId, string? initialFocusSelector = null) => _inner.SetupFocusTrap(elementId, initialFocusSelector);
        public ValueTask RemoveFocusTrap(string elementId) => _inner.RemoveFocusTrap(elementId);
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
        public ValueTask<ElementRect?> GetElementRectBySelector(string selector) => _inner.GetElementRectBySelector(selector);
        public ValueTask ScrollSelectorIntoView(string selector) => _inner.ScrollSelectorIntoView(selector);
        public ValueTask RegisterAffix(string elementId, int offsetTop, int? offsetBottom, string? target, Func<bool, Task> handler) => _inner.RegisterAffix(elementId, offsetTop, offsetBottom, target, handler);
        public ValueTask UnregisterAffix(string elementId) => _inner.UnregisterAffix(elementId);
        public ValueTask<ComponentInteropService.TextareaCaretInfo> GetTextareaCaretPosition(string elementId) => _inner.GetTextareaCaretPosition(elementId);
        public ValueTask<ComponentInteropService.TabMeasurement?> TabsMeasure(string elementId) => _inner.TabsMeasure(elementId);
        public ValueTask RegisterBackToTop(string id, int threshold, Func<bool, Task> handler) => _inner.RegisterBackToTop(id, threshold, handler);
        public ValueTask UnregisterBackToTop(string id) => _inner.UnregisterBackToTop(id);
        public ValueTask ScrollToTop() => _inner.ScrollToTop();
        public ValueTask DownloadFile(string fileName, string contentBase64, string mimeType = "application/octet-stream") => _inner.DownloadFile(fileName, contentBase64, mimeType);
        public ValueTask CopyToClipboard(string text) => _inner.CopyToClipboard(text);
        public ValueTask RippleAttachAsync(ElementReference element) => _inner.RippleAttachAsync(element);
        public ValueTask RippleDetachAsync(ElementReference element) => _inner.RippleDetachAsync(element);
        public ValueTask PlayMedia(ElementReference element) => _inner.PlayMedia(element);
        public ValueTask PauseMedia(ElementReference element) => _inner.PauseMedia(element);
        public ValueTask SetMediaVolume(ElementReference element, double volume, bool muted) => _inner.SetMediaVolume(element, volume, muted);
        public ValueTask SeekMedia(ElementReference element, double seconds) => _inner.SeekMedia(element, seconds);
        public ValueTask<MediaState> GetMediaState(ElementReference element) => _inner.GetMediaState(element);
        public ValueTask Vibrate(int milliseconds) => _inner.Vibrate(milliseconds);
        public ValueTask SaveToLocalStorage(string key, string value) => _inner.SaveToLocalStorage(key, value);
        public ValueTask<string?> LoadFromLocalStorage(string key) => _inner.LoadFromLocalStorage(key);
        public ValueTask RemoveFromLocalStorage(string key) => _inner.RemoveFromLocalStorage(key);
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

        public void Dispose() => _inner.Dispose();
        public ValueTask DisposeAsync() => _inner.DisposeAsync();
    }

    [Fact]
    public async Task Legacy_Implementor_Satisfies_Interface_And_Routes_New_Members_Through_DIMs()
    {
        // If any of round-8/9's new DataGrid resize/reorder members were abstract,
        // this class would not compile at all — the strongest form of the guard.
        IComponentInteropService svc = new LegacyPreReorderInterop();
        var legacy = (LegacyPreReorderInterop)svc;

        // Original 4-parameter RegisterColumnResize (unchanged public surface) —
        // still the abstract member this legacy double implements.
        await svc.RegisterColumnResize("handle-1", 50, 400, _ => Task.CompletedTask);
        Assert.Equal(1, legacy.RegisterColumnResizeCallCount);

        // The autoFit-aware 4-parameter overload is an ADDITIVE default interface
        // member. The legacy double never implemented it, yet the call still
        // resolves — the DIM delegates to the legacy 3-arg-delegate member above
        // (autoFit dropped), so RegisterColumnResizeCallCount increments again.
        await svc.RegisterColumnResize("handle-2", 50, 400, (_, _) => Task.CompletedTask);
        Assert.Equal(2, legacy.RegisterColumnResizeCallCount);

        // Every new pointer-reorder / row-reorder member answers via its no-op DIM
        // default instead of throwing NotImplementedException / failing to compile.
        var ex = await Record.ExceptionAsync(async () =>
        {
            await svc.NudgeColumnResize("handle-1", 5);
            await svc.RegisterColumnReorder("grid-1", (_, _) => Task.CompletedTask);
            await svc.UnregisterColumnReorder("grid-1");
            await svc.ClearColumnReorderTransforms("grid-1");
            await svc.RegisterRowReorder("grid-1", (_, _) => Task.CompletedTask);
            await svc.UnregisterRowReorder("grid-1");
            await svc.CaptureRowRects("grid-1");
            await svc.AnimateRowReorder("grid-1", 180);
            await svc.ClearRowReorderTransforms("grid-1");
        });
        Assert.Null(ex);
    }
}
