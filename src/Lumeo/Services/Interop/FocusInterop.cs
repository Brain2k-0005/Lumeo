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

    public async ValueTask LockScroll(IJSObjectReference module)
    {
        await module.InvokeVoidAsync("lockScroll");
    }

    public async ValueTask UnlockScroll(IJSObjectReference module)
    {
        await module.InvokeVoidAsync("unlockScroll");
    }

    public async ValueTask SetupFocusTrap(IJSObjectReference module, string elementId)
    {
        await module.InvokeVoidAsync("setupFocusTrap", elementId);
    }

    public async ValueTask RemoveFocusTrap(IJSObjectReference module, string elementId)
    {
        await module.InvokeVoidAsync("removeFocusTrap", elementId);
    }
}
