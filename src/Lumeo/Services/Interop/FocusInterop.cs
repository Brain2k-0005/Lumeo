using Microsoft.JSInterop;

namespace Lumeo.Services.Interop;

internal sealed class FocusInterop
{
    public async ValueTask FocusElement(IJSObjectReference module, string elementId)
    {
        await module.InvokeVoidAsync("focusElementById", elementId);
    }

    public async ValueTask FocusMenuItemByIndex(IJSObjectReference module, string containerId, int index)
    {
        await module.InvokeVoidAsync("focusMenuItemByIndex", containerId, index);
    }

    public async ValueTask<int> GetMenuItemCount(IJSObjectReference module, string containerId)
    {
        return await module.InvokeAsync<int>("getMenuItemCount", containerId);
    }

    public async ValueTask<string[]> GetOrderedDescendantIds(IJSObjectReference module, string containerId, string selector)
    {
        return await module.InvokeAsync<string[]>("getOrderedDescendantIds", containerId, selector);
    }

    public async ValueTask<int> FocusMenuItemByTypeahead(IJSObjectReference module, string containerId, string query, int currentIndex)
    {
        return await module.InvokeAsync<int>("focusMenuItemByTypeahead", containerId, query, currentIndex);
    }

    public async ValueTask LockScroll(IJSObjectReference module)
    {
        await module.InvokeVoidAsync("lockScroll");
    }

    public async ValueTask UnlockScroll(IJSObjectReference module)
    {
        await module.InvokeVoidAsync("unlockScroll");
    }

    public async ValueTask SetupFocusTrap(IJSObjectReference module, string elementId, string? initialFocusSelector = null)
    {
        await module.InvokeVoidAsync("setupFocusTrap", elementId, initialFocusSelector);
    }

    public async ValueTask RemoveFocusTrap(IJSObjectReference module, string elementId)
    {
        await module.InvokeVoidAsync("removeFocusTrap", elementId);
    }
}
