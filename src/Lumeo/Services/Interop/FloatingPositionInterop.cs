using Microsoft.JSInterop;
using Lumeo.Services;

namespace Lumeo.Services.Interop;

internal sealed class FloatingPositionInterop
{
    public async ValueTask PositionFixed(
        IJSObjectReference module,
        string contentId,
        string referenceId,
        string align = "start",
        bool matchWidth = false,
        string side = "bottom")
    {
        await module.InvokeVoidAsync("positionFixed", contentId, referenceId, align, matchWidth, side);
    }

    public async ValueTask UnpositionFixed(IJSObjectReference module, string contentId)
    {
        await module.InvokeVoidAsync("unpositionFixed", contentId);
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

    public async ValueTask<double> GetElementDimension(
        IJSObjectReference module,
        string elementId,
        string dimension)
    {
        return await module.InvokeAsync<double>("getElementDimension", elementId, dimension);
    }
}
