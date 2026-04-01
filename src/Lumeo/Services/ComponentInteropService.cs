using Microsoft.JSInterop;

namespace Lumeo.Services;

public sealed class ComponentInteropService : IAsyncDisposable, IDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private IJSObjectReference? _module;
    private DotNetObjectReference<ComponentInteropService>? _selfRef;
    private readonly Dictionary<string, Func<Task>> _clickOutsideHandlers = new();
    private readonly Dictionary<string, Func<Task>> _drawerSwipeHandlers = new();
    private readonly Dictionary<string, Func<string, Task>> _carouselSwipeHandlers = new();
    private readonly Dictionary<string, Func<double, double, Task>> _carouselScrollHandlers = new();
    private readonly Dictionary<string, Func<double, Task>> _resizeHandlers = new();
    private readonly Dictionary<string, Func<Task>> _resizeEndHandlers = new();
    private readonly Dictionary<string, Func<string?, Task>> _scrollspyHandlers = new();
    private readonly Dictionary<string, Func<string, Task>> _toastSwipeHandlers = new();

    public ComponentInteropService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    private async Task<IJSObjectReference> GetModuleAsync()
    {
        _module ??= await _jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/Lumeo/js/components.js");
        return _module;
    }

    private DotNetObjectReference<ComponentInteropService> GetSelfRef()
    {
        _selfRef ??= DotNetObjectReference.Create(this);
        return _selfRef;
    }

    public async ValueTask RegisterClickOutside(string elementId, string? triggerElementId, Func<Task> handler)
    {
        _clickOutsideHandlers[elementId] = handler;
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("registerClickOutside", elementId, triggerElementId, GetSelfRef());
    }

    public async ValueTask UnregisterClickOutside(string elementId)
    {
        _clickOutsideHandlers.Remove(elementId);
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("unregisterClickOutside", elementId);
    }

    [JSInvokable]
    public async Task OnClickOutside(string elementId)
    {
        if (_clickOutsideHandlers.TryGetValue(elementId, out var handler))
        {
            await handler();
        }
    }

    public async ValueTask FocusElement(string elementId)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("focusElementById", elementId);
    }

    public async ValueTask FocusMenuItemByIndex(string containerId, int index)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("focusMenuItemByIndex", containerId, index);
    }

    public async ValueTask<int> GetMenuItemCount(string containerId)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<int>("getMenuItemCount", containerId);
    }

    public async ValueTask LockScroll()
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("lockScroll");
    }

    public async ValueTask UnlockScroll()
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("unlockScroll");
    }

    public async ValueTask SetupFocusTrap(string elementId)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("setupFocusTrap", elementId);
    }

    public async ValueTask RemoveFocusTrap(string elementId)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("removeFocusTrap", elementId);
    }

    // --- Floating Position ---

    public async ValueTask PositionFixed(string contentId, string referenceId, string align = "start", bool matchWidth = false, string side = "bottom")
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("positionFixed", contentId, referenceId, align, matchWidth, side);
    }

    public async ValueTask UnpositionFixed(string contentId)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("unpositionFixed", contentId);
    }

    // --- Element Rect ---

    public async ValueTask<ElementRect?> GetElementRect(string elementId)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<ElementRect?>("getElementRect", elementId);
    }

    public async ValueTask<double> GetElementDimension(string elementId, string dimension)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<double>("getElementDimension", elementId, dimension);
    }

    // --- Drawer Swipe ---

    public async ValueTask RegisterDrawerSwipe(string elementId, string direction, Func<Task> handler)
    {
        _drawerSwipeHandlers[elementId] = handler;
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("registerDrawerSwipe", elementId, direction, GetSelfRef());
    }

    public async ValueTask RegisterDrawerSwipe(string elementId, Func<Task> handler)
    {
        _drawerSwipeHandlers[elementId] = handler;
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("registerDrawerSwipe", elementId, "down", GetSelfRef());
    }

    public async ValueTask UnregisterDrawerSwipe(string elementId)
    {
        _drawerSwipeHandlers.Remove(elementId);
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("unregisterDrawerSwipe", elementId);
    }

    [JSInvokable]
    public async Task OnSwipeDismiss(string elementId)
    {
        if (_drawerSwipeHandlers.TryGetValue(elementId, out var handler))
        {
            await handler();
        }
    }

    // --- Carousel Swipe ---

    public async ValueTask RegisterCarouselSwipe(string elementId, string orientation, Func<string, Task> swipeHandler, Func<double, double, Task> scrollHandler)
    {
        _carouselSwipeHandlers[elementId] = swipeHandler;
        _carouselScrollHandlers[elementId] = scrollHandler;
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("registerCarouselSwipe", elementId, orientation, GetSelfRef());
    }

    public async ValueTask UnregisterCarouselSwipe(string elementId)
    {
        _carouselSwipeHandlers.Remove(elementId);
        _carouselScrollHandlers.Remove(elementId);
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("unregisterCarouselSwipe", elementId);
    }

    public async ValueTask CarouselScrollTo(string elementId, int index, string behavior = "smooth")
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("carouselScrollTo", elementId, index, behavior);
    }

    [JSInvokable]
    public async Task OnSwipe(string elementId, string direction)
    {
        if (_carouselSwipeHandlers.TryGetValue(elementId, out var handler))
        {
            await handler(direction);
        }
    }

    [JSInvokable]
    public async Task OnScrollPosition(string elementId, double scrollPos, double maxScroll)
    {
        if (_carouselScrollHandlers.TryGetValue(elementId, out var handler))
        {
            await handler(scrollPos, maxScroll);
        }
    }

    // --- Resizable Handle ---

    public async ValueTask RegisterResizeHandle(string elementId, string direction, Func<double, Task> resizeHandler, Func<Task> resizeEndHandler)
    {
        _resizeHandlers[elementId] = resizeHandler;
        _resizeEndHandlers[elementId] = resizeEndHandler;
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("registerResizeHandle", elementId, direction, GetSelfRef());
    }

    public async ValueTask UnregisterResizeHandle(string elementId)
    {
        _resizeHandlers.Remove(elementId);
        _resizeEndHandlers.Remove(elementId);
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("unregisterResizeHandle", elementId);
    }

    [JSInvokable]
    public async Task OnResize(string elementId, double delta)
    {
        if (_resizeHandlers.TryGetValue(elementId, out var handler))
        {
            await handler(delta);
        }
    }

    [JSInvokable]
    public async Task OnResizeEnd(string elementId)
    {
        if (_resizeEndHandlers.TryGetValue(elementId, out var handler))
        {
            await handler();
        }
    }

    // --- Scrollspy ---

    public async ValueTask RegisterScrollspy(string containerId, int offset, bool smooth, Func<string?, Task> handler)
    {
        _scrollspyHandlers[containerId] = handler;
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("registerScrollspy", containerId, offset, smooth, GetSelfRef());
    }

    public async ValueTask UnregisterScrollspy(string containerId)
    {
        _scrollspyHandlers.Remove(containerId);
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("unregisterScrollspy", containerId);
    }

    public async ValueTask ScrollspyScrollTo(string containerId, string sectionId, bool smooth)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("scrollspyScrollTo", containerId, sectionId, smooth);
    }

    [JSInvokable]
    public async Task OnScrollspyUpdate(string containerId, string? activeId)
    {
        if (_scrollspyHandlers.TryGetValue(containerId, out var handler))
        {
            await handler(activeId);
        }
    }

    // --- Toast Swipe ---

    public async ValueTask RegisterToastSwipe(string elementId, string toastId, Func<string, Task> handler)
    {
        _toastSwipeHandlers[toastId] = handler;
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("registerToastSwipe", elementId, toastId, GetSelfRef());
    }

    public async ValueTask UnregisterToastSwipe(string elementId)
    {
        _toastSwipeHandlers.Remove(elementId);
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("unregisterToastSwipe", elementId);
    }

    [JSInvokable]
    public async Task OnToastSwipeDismiss(string toastId)
    {
        if (_toastSwipeHandlers.TryGetValue(toastId, out var handler))
        {
            await handler(toastId);
        }
    }

    // --- Auto Resize ---

    public async ValueTask SetupAutoResize(string elementId, int maxRows)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("setupAutoResize", elementId, maxRows);
    }

    // --- OTP Paste ---

    private readonly Dictionary<string, Func<string, Task>> _otpPasteHandlers = new();

    public async ValueTask RegisterOtpPaste(string baseId, int length, Func<string, Task> handler)
    {
        _otpPasteHandlers[baseId] = handler;
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("registerOtpPaste", baseId, length, GetSelfRef());
    }

    public async ValueTask UnregisterOtpPaste(string baseId, int length)
    {
        _otpPasteHandlers.Remove(baseId);
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("unregisterOtpPaste", baseId, length);
    }

    [JSInvokable]
    public async Task OnOtpPaste(string baseId, string digits)
    {
        if (_otpPasteHandlers.TryGetValue(baseId, out var handler))
        {
            await handler(digits);
        }
    }

    // --- DataGrid Column Resize ---

    private readonly Dictionary<string, Func<double, Task>> _columnResizeHandlers = new();
    private readonly Dictionary<string, Func<Task>> _columnResizeEndHandlers = new();

    public async ValueTask RegisterColumnResize(string handleId, Func<double, Task> resizeHandler, Func<Task> resizeEndHandler)
    {
        _columnResizeHandlers[handleId] = resizeHandler;
        _columnResizeEndHandlers[handleId] = resizeEndHandler;
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("registerColumnResize", handleId, GetSelfRef());
    }

    public async ValueTask UnregisterColumnResize(string handleId)
    {
        _columnResizeHandlers.Remove(handleId);
        _columnResizeEndHandlers.Remove(handleId);
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("unregisterColumnResize", handleId);
    }

    [JSInvokable]
    public async Task OnColumnResize(string handleId, double delta)
    {
        if (_columnResizeHandlers.TryGetValue(handleId, out var handler))
            await handler(delta);
    }

    [JSInvokable]
    public async Task OnColumnResizeEnd(string handleId)
    {
        if (_columnResizeEndHandlers.TryGetValue(handleId, out var handler))
            await handler();
    }

    // --- Tour: Element Rect By Selector ---

    public async ValueTask<ElementRect?> GetElementRectBySelector(string selector)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<ElementRect?>("getElementRectBySelector", selector);
    }

    // --- Affix ---

    private readonly Dictionary<string, Func<bool, Task>> _affixHandlers = new();

    public async ValueTask RegisterAffix(string elementId, int offsetTop, int? offsetBottom, string? target, Func<bool, Task> handler)
    {
        _affixHandlers[elementId] = handler;
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("registerAffix", elementId, offsetTop, offsetBottom, target, GetSelfRef());
    }

    public async ValueTask UnregisterAffix(string elementId)
    {
        _affixHandlers.Remove(elementId);
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("unregisterAffix", elementId);
    }

    [JSInvokable]
    public async Task OnAffixChanged(string elementId, bool isFixed)
    {
        if (_affixHandlers.TryGetValue(elementId, out var handler))
        {
            await handler(isFixed);
        }
    }

    // --- Mention: Textarea Caret Position ---

    public async ValueTask<TextareaCaretInfo> GetTextareaCaretPosition(string elementId)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<TextareaCaretInfo>("getTextareaCaretPosition", elementId);
    }

    public record TextareaCaretInfo(double Top, double Left, int SelectionStart);

    // --- BackToTop ---

    private readonly Dictionary<string, Func<bool, Task>> _backToTopHandlers = new();

    public async ValueTask RegisterBackToTop(string id, int threshold, Func<bool, Task> handler)
    {
        _backToTopHandlers[id] = handler;
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("registerBackToTop", id, GetSelfRef(), threshold);
    }

    public async ValueTask UnregisterBackToTop(string id)
    {
        _backToTopHandlers.Remove(id);
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("unregisterBackToTop", id);
    }

    public async ValueTask ScrollToTop()
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("scrollToTop");
    }

    [JSInvokable]
    public async Task OnScrollVisibilityChanged(string id, bool visible)
    {
        if (_backToTopHandlers.TryGetValue(id, out var handler))
        {
            await handler(visible);
        }
    }

    // --- File Download ---

    public async ValueTask DownloadFile(string fileName, string contentBase64, string mimeType = "application/octet-stream")
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("downloadFile", fileName, contentBase64, mimeType);
    }

    // --- Clipboard ---

    public async ValueTask CopyToClipboard(string text)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("copyToClipboard", text);
    }

    // --- LocalStorage ---

    public async ValueTask SaveToLocalStorage(string key, string value)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("saveToLocalStorage", key, value);
    }

    public async ValueTask<string?> LoadFromLocalStorage(string key)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<string?>("loadFromLocalStorage", key);
    }

    public async ValueTask RemoveFromLocalStorage(string key)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("removeFromLocalStorage", key);
    }

    public record ElementRect(double X, double Y, double Width, double Height);

    public void Dispose()
    {
        _clickOutsideHandlers.Clear();
        _drawerSwipeHandlers.Clear();
        _carouselSwipeHandlers.Clear();
        _carouselScrollHandlers.Clear();
        _resizeHandlers.Clear();
        _resizeEndHandlers.Clear();
        _scrollspyHandlers.Clear();
        _toastSwipeHandlers.Clear();
        _otpPasteHandlers.Clear();
        _columnResizeHandlers.Clear();
        _columnResizeEndHandlers.Clear();
        _affixHandlers.Clear();
        _backToTopHandlers.Clear();
        _selfRef?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var id in _clickOutsideHandlers.Keys.ToList())
        {
            try
            {
                if (_module is not null)
                {
                    await _module.InvokeVoidAsync("unregisterClickOutside", id);
                }
            }
            catch (JSDisconnectedException)
            {
                // Circuit disconnected, safe to ignore
            }
        }

        _clickOutsideHandlers.Clear();
        _drawerSwipeHandlers.Clear();
        _carouselSwipeHandlers.Clear();
        _carouselScrollHandlers.Clear();
        _resizeHandlers.Clear();
        _resizeEndHandlers.Clear();
        _scrollspyHandlers.Clear();
        _toastSwipeHandlers.Clear();
        _otpPasteHandlers.Clear();
        _columnResizeHandlers.Clear();
        _columnResizeEndHandlers.Clear();
        _affixHandlers.Clear();
        _backToTopHandlers.Clear();

        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // Circuit disconnected, safe to ignore
            }
        }

        _selfRef?.Dispose();
    }
}
