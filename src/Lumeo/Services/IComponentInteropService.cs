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
    ValueTask SetupFocusTrap(string elementId);
    ValueTask RemoveFocusTrap(string elementId);

    // ColorPicker SV Drag
    ValueTask RegisterSvDrag(string elementId, Func<double, double, Task> handler);
    ValueTask UnregisterSvDrag(string elementId);

    // Floating Position
    ValueTask PositionFixed(string contentId, string referenceId, string align = "start", bool matchWidth = false, string side = "bottom");
    ValueTask UnpositionFixed(string contentId);
    ValueTask<ElementRect?> GetElementRect(string elementId);
    ValueTask<double> GetElementDimension(string elementId, string dimension);

    // Pointer Capture (used by Splitter dividers)
    ValueTask SetPointerCaptureOnElement(string elementId, long pointerId);
    ValueTask ReleasePointerCaptureOnElement(string elementId, long pointerId);

    // Drawer Swipe
    ValueTask RegisterDrawerSwipe(string elementId, string direction, Func<Task> handler);
    ValueTask RegisterDrawerSwipe(string elementId, Func<Task> handler);
    ValueTask UnregisterDrawerSwipe(string elementId);

    // Carousel Swipe
    ValueTask RegisterCarouselSwipe(string elementId, string orientation, Func<string, Task> swipeHandler, Func<double, double, Task> scrollHandler);
    ValueTask UnregisterCarouselSwipe(string elementId);
    ValueTask CarouselScrollTo(string elementId, int index, string behavior = "smooth");

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

    // OTP Paste
    ValueTask RegisterOtpPaste(string baseId, int length, Func<string, Task> handler);
    ValueTask UnregisterOtpPaste(string baseId, int length);

    // DataGrid Column Resize
    ValueTask RegisterColumnResize(string handleId, Func<double, Task> resizeHandler, Func<Task> resizeEndHandler);
    ValueTask UnregisterColumnResize(string handleId);

    // Tour
    ValueTask<ElementRect?> GetElementRectBySelector(string selector);

    // Affix
    ValueTask RegisterAffix(string elementId, int offsetTop, int? offsetBottom, string? target, Func<bool, Task> handler);
    ValueTask UnregisterAffix(string elementId);

    // Mention / Textarea Caret
    ValueTask<ComponentInteropService.TextareaCaretInfo> GetTextareaCaretPosition(string elementId);

    // BackToTop
    ValueTask RegisterBackToTop(string id, int threshold, Func<bool, Task> handler);
    ValueTask UnregisterBackToTop(string id);
    ValueTask ScrollToTop();

    // File Download
    ValueTask DownloadFile(string fileName, string contentBase64, string mimeType = "application/octet-stream");

    // Clipboard
    ValueTask CopyToClipboard(string text);

    // LocalStorage
    ValueTask SaveToLocalStorage(string key, string value);
    ValueTask<string?> LoadFromLocalStorage(string key);
    ValueTask RemoveFromLocalStorage(string key);

    // Motion primitives
    ValueTask MotionTickNumber(string elementId, double from, double to, int durationMs, int decimals);
    ValueTask MotionDisposeTicker(string elementId);
    ValueTask MotionRevealText(string elementId, int staggerMs, double threshold);
    ValueTask MotionBlurFade(string elementId, int delayMs, bool once);
    ValueTask MotionDisposeObserver(string elementId);

    // AI primitives
    ValueTask AiAutosize(string elementId, int maxPx);
    ValueTask AiObserveAutoScroll(string elementId);
    ValueTask AiDisposeAutoScroll(string elementId);
    ValueTask AiScrollToBottom(string elementId);
}
