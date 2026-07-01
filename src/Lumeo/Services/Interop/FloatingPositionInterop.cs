using Microsoft.JSInterop;
using Lumeo.Services;

namespace Lumeo.Services.Interop;

internal sealed class FloatingPositionInterop
{
    // Keyed by contentId: the live-flip callback registered for an OPEN positioned surface. JS invokes
    // ComponentInteropService.OnPositionSideChanged(contentId, side) on every scroll/resize reposition that
    // changes the resolved side; this dictionary routes that back to the right caller's callback. Cleared
    // in UnpositionFixed so a stray late JS call after teardown finds nothing to dispatch to.
    private readonly Dictionary<string, Func<string, Task>> _sideChangeHandlers = new();

    public async ValueTask<string> PositionFixed(
        IJSObjectReference module,
        string contentId,
        string referenceId,
        string align = "start",
        bool matchWidth = false,
        string side = "bottom",
        int offset = 4)
    {
        // The JS returns the side the box ACTUALLY resolved to (a collision flip can move a preferred-Top
        // box below its trigger, etc.). Fall back to the requested side if an older/stale cached script
        // returns null/undefined, so a directional-arrow consumer still gets a sensible value.
        var resolved = await module.InvokeAsync<string?>("positionFixed", contentId, referenceId, align, matchWidth, side, offset);
        return string.IsNullOrEmpty(resolved) ? side : resolved!;
    }

    // round-14 — extended overload that also reports LIVE collision flips (a later scroll/resize
    // reposition, not just the initial placement) via onSideChanged. Passes selfRef so the JS scroll/
    // resize listener can call back into ComponentInteropService.OnPositionSideChanged for the lifetime of
    // this registration; UnpositionFixed below removes the handler.
    public async ValueTask<string> PositionFixed(
        IJSObjectReference module,
        DotNetObjectReference<ComponentInteropService> selfRef,
        string contentId,
        string referenceId,
        string align,
        bool matchWidth,
        string side,
        int offset,
        Func<string, Task>? onSideChanged)
    {
        if (onSideChanged is null)
        {
            _sideChangeHandlers.Remove(contentId);
            return await PositionFixed(module, contentId, referenceId, align, matchWidth, side, offset);
        }
        _sideChangeHandlers[contentId] = onSideChanged;
        var resolved = await module.InvokeAsync<string?>("positionFixed", contentId, referenceId, align, matchWidth, side, offset, selfRef);
        return string.IsNullOrEmpty(resolved) ? side : resolved!;
    }

    public async ValueTask UnpositionFixed(IJSObjectReference module, string contentId)
    {
        _sideChangeHandlers.Remove(contentId);
        await module.InvokeVoidAsync("unpositionFixed", contentId);
    }

    // Dispatches a JS-reported live side change (scroll/resize flip) to the registered caller, if any —
    // a no-op if the surface was already unpositioned (the dictionary entry is gone) or never registered
    // a live callback in the first place.
    public async Task OnSideChanged(string contentId, string side)
    {
        if (_sideChangeHandlers.TryGetValue(contentId, out var handler))
            await handler(side);
    }

    public async ValueTask PositionAtPoint(IJSObjectReference module, string contentId, double x, double y)
    {
        await module.InvokeVoidAsync("positionAtPoint", contentId, x, y);
    }

    public async ValueTask<ElementRect?> GetElementRect(
        IJSObjectReference module,
        string elementId)
    {
        return await module.InvokeAsync<ElementRect?>("getElementRect", elementId);
    }

    public async ValueTask<ElementRect?> GetElementRectBySelector(
        IJSObjectReference module,
        string selector)
    {
        return await module.InvokeAsync<ElementRect?>("getElementRectBySelector", selector);
    }

    public async ValueTask ScrollSelectorIntoView(
        IJSObjectReference module,
        string selector)
    {
        await module.InvokeVoidAsync("scrollSelectorIntoView", selector);
    }

    public async ValueTask ScrollIntoView(
        IJSObjectReference module,
        string elementId,
        string block)
    {
        await module.InvokeVoidAsync("scrollIntoViewById", elementId, block);
    }

    public async ValueTask<double> GetElementDimension(
        IJSObjectReference module,
        string elementId,
        string dimension)
    {
        return await module.InvokeAsync<double>("getElementDimension", elementId, dimension);
    }
}
