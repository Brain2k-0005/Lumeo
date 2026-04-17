using Lumeo.Services.Interop;
using Microsoft.JSInterop;

namespace Lumeo.Services;

public sealed class ComponentInteropService : IAsyncDisposable, IDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private IJSObjectReference? _module;
    private DotNetObjectReference<ComponentInteropService>? _selfRef;

    // Adapters
    private readonly ClickOutsideInterop _clickOutside = new();
    private readonly FloatingPositionInterop _floatingPosition = new();
    private readonly FocusInterop _focus = new();
    private readonly SwipeInterop _swipe = new();
    private readonly ResizeInterop _resize = new();
    private readonly ScrollInterop _scroll = new();
    private readonly UtilityInterop _utility = new();

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

    // --- Click Outside ---

    public async ValueTask RegisterClickOutside(string elementId, string? triggerElementId, Func<Task> handler)
    {
        var module = await GetModuleAsync();
        await _clickOutside.Register(module, GetSelfRef(), elementId, triggerElementId, handler);
    }

    public async ValueTask UnregisterClickOutside(string elementId)
    {
        var module = await GetModuleAsync();
        await _clickOutside.Unregister(module, elementId);
    }

    [JSInvokable]
    public async Task OnClickOutside(string elementId) => await _clickOutside.OnCallback(elementId);

    // --- Focus / Scroll Lock ---

    public async ValueTask FocusElement(string elementId)
    {
        var module = await GetModuleAsync();
        await _focus.FocusElement(module, elementId);
    }

    public async ValueTask FocusMenuItemByIndex(string containerId, int index)
    {
        var module = await GetModuleAsync();
        await _focus.FocusMenuItemByIndex(module, containerId, index);
    }

    public async ValueTask<int> GetMenuItemCount(string containerId)
    {
        var module = await GetModuleAsync();
        return await _focus.GetMenuItemCount(module, containerId);
    }

    public async ValueTask LockScroll()
    {
        var module = await GetModuleAsync();
        await _focus.LockScroll(module);
    }

    public async ValueTask UnlockScroll()
    {
        var module = await GetModuleAsync();
        await _focus.UnlockScroll(module);
    }

    public async ValueTask SetupFocusTrap(string elementId)
    {
        var module = await GetModuleAsync();
        await _focus.SetupFocusTrap(module, elementId);
    }

    public async ValueTask RemoveFocusTrap(string elementId)
    {
        var module = await GetModuleAsync();
        await _focus.RemoveFocusTrap(module, elementId);
    }

    // --- ColorPicker SV Drag ---

    private readonly Dictionary<string, Func<double, double, Task>> _svDragHandlers = new();

    public async ValueTask RegisterSvDrag(string elementId, Func<double, double, Task> handler)
    {
        var module = await GetModuleAsync();
        _svDragHandlers[elementId] = handler;
        await module.InvokeVoidAsync("registerSvDrag", elementId, GetSelfRef());
    }

    public async ValueTask UnregisterSvDrag(string elementId)
    {
        _svDragHandlers.Remove(elementId);
        try
        {
            var module = await GetModuleAsync();
            await module.InvokeVoidAsync("unregisterSvDrag", elementId);
        }
        catch (JSDisconnectedException) { }
    }

    [JSInvokable]
    public Task OnSvDrag(string elementId, double s, double v)
    {
        if (_svDragHandlers.TryGetValue(elementId, out var handler))
            return handler(s, v);
        return Task.CompletedTask;
    }

    // --- Floating Position ---

    public async ValueTask PositionFixed(string contentId, string referenceId, string align = "start", bool matchWidth = false, string side = "bottom")
    {
        var module = await GetModuleAsync();
        await _floatingPosition.PositionFixed(module, contentId, referenceId, align, matchWidth, side);
    }

    public async ValueTask UnpositionFixed(string contentId)
    {
        var module = await GetModuleAsync();
        await _floatingPosition.UnpositionFixed(module, contentId);
    }

    // --- Element Rect ---

    public async ValueTask<ElementRect?> GetElementRect(string elementId)
    {
        var module = await GetModuleAsync();
        return await _floatingPosition.GetElementRect(module, elementId);
    }

    public async ValueTask<double> GetElementDimension(string elementId, string dimension)
    {
        var module = await GetModuleAsync();
        return await _floatingPosition.GetElementDimension(module, elementId, dimension);
    }

    // --- Drawer Swipe ---

    public async ValueTask RegisterDrawerSwipe(string elementId, string direction, Func<Task> handler)
    {
        var module = await GetModuleAsync();
        await _swipe.RegisterDrawerSwipe(module, GetSelfRef(), elementId, direction, handler);
    }

    public async ValueTask RegisterDrawerSwipe(string elementId, Func<Task> handler)
    {
        var module = await GetModuleAsync();
        await _swipe.RegisterDrawerSwipe(module, GetSelfRef(), elementId, handler);
    }

    public async ValueTask UnregisterDrawerSwipe(string elementId)
    {
        var module = await GetModuleAsync();
        await _swipe.UnregisterDrawerSwipe(module, elementId);
    }

    [JSInvokable]
    public async Task OnSwipeDismiss(string elementId) => await _swipe.OnSwipeDismiss(elementId);

    // --- Carousel Swipe ---

    public async ValueTask RegisterCarouselSwipe(string elementId, string orientation, Func<string, Task> swipeHandler, Func<double, double, Task> scrollHandler)
    {
        var module = await GetModuleAsync();
        await _swipe.RegisterCarouselSwipe(module, GetSelfRef(), elementId, orientation, swipeHandler, scrollHandler);
    }

    public async ValueTask UnregisterCarouselSwipe(string elementId)
    {
        var module = await GetModuleAsync();
        await _swipe.UnregisterCarouselSwipe(module, elementId);
    }

    public async ValueTask CarouselScrollTo(string elementId, int index, string behavior = "smooth")
    {
        var module = await GetModuleAsync();
        await _swipe.CarouselScrollTo(module, elementId, index, behavior);
    }

    [JSInvokable]
    public async Task OnSwipe(string elementId, string direction) => await _swipe.OnSwipe(elementId, direction);

    [JSInvokable]
    public async Task OnScrollPosition(string elementId, double scrollPos, double maxScroll)
        => await _swipe.OnScrollPosition(elementId, scrollPos, maxScroll);

    // --- Resizable Handle ---

    public async ValueTask RegisterResizeHandle(string elementId, string direction, Func<double, Task> resizeHandler, Func<Task> resizeEndHandler)
    {
        var module = await GetModuleAsync();
        await _resize.RegisterResizeHandle(module, GetSelfRef(), elementId, direction, resizeHandler, resizeEndHandler);
    }

    public async ValueTask UnregisterResizeHandle(string elementId)
    {
        var module = await GetModuleAsync();
        await _resize.UnregisterResizeHandle(module, elementId);
    }

    [JSInvokable]
    public async Task OnResize(string elementId, double delta) => await _resize.OnResize(elementId, delta);

    [JSInvokable]
    public async Task OnResizeEnd(string elementId) => await _resize.OnResizeEnd(elementId);

    // --- Scrollspy ---

    public async ValueTask RegisterScrollspy(string containerId, int offset, bool smooth, Func<string?, Task> handler)
    {
        var module = await GetModuleAsync();
        await _scroll.RegisterScrollspy(module, GetSelfRef(), containerId, offset, smooth, handler);
    }

    public async ValueTask UnregisterScrollspy(string containerId)
    {
        var module = await GetModuleAsync();
        await _scroll.UnregisterScrollspy(module, containerId);
    }

    public async ValueTask ScrollspyScrollTo(string containerId, string sectionId, bool smooth)
    {
        var module = await GetModuleAsync();
        await _scroll.ScrollspyScrollTo(module, containerId, sectionId, smooth);
    }

    [JSInvokable]
    public async Task OnScrollspyUpdate(string containerId, string? activeId)
        => await _scroll.OnScrollspyUpdate(containerId, activeId);

    // --- Toast Swipe ---

    public async ValueTask RegisterToastSwipe(string elementId, string toastId, Func<string, Task> handler)
    {
        var module = await GetModuleAsync();
        await _swipe.RegisterToastSwipe(module, GetSelfRef(), elementId, toastId, handler);
    }

    public async ValueTask UnregisterToastSwipe(string toastId, string elementId)
    {
        var module = await GetModuleAsync();
        await _swipe.UnregisterToastSwipe(module, toastId, elementId);
    }

    [JSInvokable]
    public async Task OnToastSwipeDismiss(string toastId) => await _swipe.OnToastSwipeDismiss(toastId);

    // --- Auto Resize ---

    public async ValueTask SetupAutoResize(string elementId, int maxRows)
    {
        var module = await GetModuleAsync();
        await _utility.SetupAutoResize(module, elementId, maxRows);
    }

    // --- OTP Paste ---

    public async ValueTask RegisterOtpPaste(string baseId, int length, Func<string, Task> handler)
    {
        var module = await GetModuleAsync();
        await _utility.RegisterOtpPaste(module, GetSelfRef(), baseId, length, handler);
    }

    public async ValueTask UnregisterOtpPaste(string baseId, int length)
    {
        var module = await GetModuleAsync();
        await _utility.UnregisterOtpPaste(module, baseId, length);
    }

    [JSInvokable]
    public async Task OnOtpPaste(string baseId, string digits) => await _utility.OnOtpPaste(baseId, digits);

    // --- DataGrid Column Resize ---

    public async ValueTask RegisterColumnResize(string handleId, Func<double, Task> resizeHandler, Func<Task> resizeEndHandler)
    {
        var module = await GetModuleAsync();
        await _resize.RegisterColumnResize(module, GetSelfRef(), handleId, resizeHandler, resizeEndHandler);
    }

    public async ValueTask UnregisterColumnResize(string handleId)
    {
        var module = await GetModuleAsync();
        await _resize.UnregisterColumnResize(module, handleId);
    }

    [JSInvokable]
    public async Task OnColumnResize(string handleId, double delta) => await _resize.OnColumnResize(handleId, delta);

    [JSInvokable]
    public async Task OnColumnResizeEnd(string handleId) => await _resize.OnColumnResizeEnd(handleId);

    // --- Tour: Element Rect By Selector ---

    public async ValueTask<ElementRect?> GetElementRectBySelector(string selector)
    {
        var module = await GetModuleAsync();
        return await _floatingPosition.GetElementRectBySelector(module, selector);
    }

    // --- Affix ---

    public async ValueTask RegisterAffix(string elementId, int offsetTop, int? offsetBottom, string? target, Func<bool, Task> handler)
    {
        var module = await GetModuleAsync();
        await _scroll.RegisterAffix(module, GetSelfRef(), elementId, offsetTop, offsetBottom, target, handler);
    }

    public async ValueTask UnregisterAffix(string elementId)
    {
        var module = await GetModuleAsync();
        await _scroll.UnregisterAffix(module, elementId);
    }

    [JSInvokable]
    public async Task OnAffixChanged(string elementId, bool isFixed) => await _scroll.OnAffixChanged(elementId, isFixed);

    // --- Mention: Textarea Caret Position ---

    public async ValueTask<TextareaCaretInfo> GetTextareaCaretPosition(string elementId)
    {
        var module = await GetModuleAsync();
        return await _utility.GetTextareaCaretPosition(module, elementId);
    }

    public record TextareaCaretInfo(double Top, double Left, int SelectionStart);

    // --- BackToTop ---

    public async ValueTask RegisterBackToTop(string id, int threshold, Func<bool, Task> handler)
    {
        var module = await GetModuleAsync();
        await _scroll.RegisterBackToTop(module, GetSelfRef(), id, threshold, handler);
    }

    public async ValueTask UnregisterBackToTop(string id)
    {
        var module = await GetModuleAsync();
        await _scroll.UnregisterBackToTop(module, id);
    }

    public async ValueTask ScrollToTop()
    {
        var module = await GetModuleAsync();
        await _scroll.ScrollToTop(module);
    }

    [JSInvokable]
    public async Task OnScrollVisibilityChanged(string id, bool visible) => await _scroll.OnScrollVisibilityChanged(id, visible);

    // --- File Download ---

    public async ValueTask DownloadFile(string fileName, string contentBase64, string mimeType = "application/octet-stream")
    {
        var module = await GetModuleAsync();
        await _utility.DownloadFile(module, fileName, contentBase64, mimeType);
    }

    // --- Clipboard ---

    public async ValueTask CopyToClipboard(string text)
    {
        var module = await GetModuleAsync();
        await _utility.CopyToClipboard(module, text);
    }

    // --- LocalStorage ---

    public async ValueTask SaveToLocalStorage(string key, string value)
    {
        var module = await GetModuleAsync();
        await _utility.SaveToLocalStorage(module, key, value);
    }

    public async ValueTask<string?> LoadFromLocalStorage(string key)
    {
        var module = await GetModuleAsync();
        return await _utility.LoadFromLocalStorage(module, key);
    }

    public async ValueTask RemoveFromLocalStorage(string key)
    {
        var module = await GetModuleAsync();
        await _utility.RemoveFromLocalStorage(module, key);
    }

    public record ElementRect(double X, double Y, double Width, double Height);

    public void Dispose()
    {
        _clickOutside.Clear();
        _swipe.Clear();
        _resize.Clear();
        _scroll.Clear();
        _utility.Clear();
        _selfRef?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var id in _clickOutside.RegisteredIds.ToList())
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

        _clickOutside.Clear();
        _swipe.Clear();
        _resize.Clear();
        _scroll.Clear();
        _utility.Clear();

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
