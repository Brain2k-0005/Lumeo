using Microsoft.JSInterop;
using Lumeo.Services;

namespace Lumeo.Services.Interop;

internal sealed class FloatingPositionInterop
{
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

    public async ValueTask UnpositionFixed(IJSObjectReference module, string contentId)
    {
        await module.InvokeVoidAsync("unpositionFixed", contentId);
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
