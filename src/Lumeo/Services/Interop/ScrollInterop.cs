using Microsoft.JSInterop;

namespace Lumeo.Services.Interop;

internal sealed class ScrollInterop
{
    private readonly Dictionary<string, Func<string?, Task>> _scrollspyHandlers = new();
    private readonly Dictionary<string, Func<bool, Task>> _backToTopHandlers = new();
    private readonly Dictionary<string, Func<bool, Task>> _affixHandlers = new();
    private readonly Dictionary<string, Func<bool, bool, Task>> _tabsOverflowHandlers = new();

    // --- Scrollspy ---

    public async ValueTask RegisterScrollspy(
        IJSObjectReference module,
        DotNetObjectReference<ComponentInteropService> selfRef,
        string containerId,
        int offset,
        bool smooth,
        Func<string?, Task> handler)
    {
        _scrollspyHandlers[containerId] = handler;
        await module.InvokeVoidAsync("registerScrollspy", containerId, offset, smooth, selfRef);
    }

    public async ValueTask UnregisterScrollspy(IJSObjectReference module, string containerId)
    {
        _scrollspyHandlers.Remove(containerId);
        await module.InvokeVoidAsync("unregisterScrollspy", containerId);
    }

    public async ValueTask ScrollspyScrollTo(
        IJSObjectReference module,
        string containerId,
        string sectionId,
        bool smooth,
        int offset)
    {
        await module.InvokeVoidAsync("scrollspyScrollTo", containerId, sectionId, smooth, offset);
    }

    public async Task OnScrollspyUpdate(string containerId, string? activeId)
    {
        if (_scrollspyHandlers.TryGetValue(containerId, out var handler))
        {
            await handler(activeId);
        }
    }

    // --- Tabs overflow scroll arrows ---

    public async ValueTask RegisterTabsOverflow(
        IJSObjectReference module,
        DotNetObjectReference<ComponentInteropService> selfRef,
        string listId,
        Func<bool, bool, Task> handler)
    {
        _tabsOverflowHandlers[listId] = handler;
        await module.InvokeVoidAsync("registerTabsOverflow", listId, selfRef);
    }

    public async ValueTask UnregisterTabsOverflow(IJSObjectReference module, string listId)
    {
        _tabsOverflowHandlers.Remove(listId);
        await module.InvokeVoidAsync("unregisterTabsOverflow", listId);
    }

    public async ValueTask TabsScrollBy(IJSObjectReference module, string listId, double delta, bool horizontal)
    {
        await module.InvokeVoidAsync("tabsScrollBy", listId, delta, horizontal);
    }

    public async Task OnTabsOverflowChange(string listId, bool canScrollStart, bool canScrollEnd)
    {
        if (_tabsOverflowHandlers.TryGetValue(listId, out var handler))
        {
            await handler(canScrollStart, canScrollEnd);
        }
    }

    // --- BackToTop ---

    public async ValueTask RegisterBackToTop(
        IJSObjectReference module,
        DotNetObjectReference<ComponentInteropService> selfRef,
        string id,
        int threshold,
        Func<bool, Task> handler)
    {
        _backToTopHandlers[id] = handler;
        await module.InvokeVoidAsync("registerBackToTop", id, selfRef, threshold);
    }

    public async ValueTask UnregisterBackToTop(IJSObjectReference module, string id)
    {
        _backToTopHandlers.Remove(id);
        await module.InvokeVoidAsync("unregisterBackToTop", id);
    }

    public async ValueTask ScrollToTop(IJSObjectReference module)
    {
        await module.InvokeVoidAsync("scrollToTop");
    }

    public async Task OnScrollVisibilityChanged(string id, bool visible)
    {
        if (_backToTopHandlers.TryGetValue(id, out var handler))
        {
            await handler(visible);
        }
    }

    // --- Affix ---

    public async ValueTask RegisterAffix(
        IJSObjectReference module,
        DotNetObjectReference<ComponentInteropService> selfRef,
        string elementId,
        int offsetTop,
        int? offsetBottom,
        string? target,
        Func<bool, Task> handler)
    {
        _affixHandlers[elementId] = handler;
        await module.InvokeVoidAsync("registerAffix", elementId, offsetTop, offsetBottom, target, selfRef);
    }

    public async ValueTask UnregisterAffix(IJSObjectReference module, string elementId)
    {
        _affixHandlers.Remove(elementId);
        await module.InvokeVoidAsync("unregisterAffix", elementId);
    }

    public async Task OnAffixChanged(string elementId, bool isFixed)
    {
        if (_affixHandlers.TryGetValue(elementId, out var handler))
        {
            await handler(isFixed);
        }
    }

    public void Clear()
    {
        _scrollspyHandlers.Clear();
        _backToTopHandlers.Clear();
        _affixHandlers.Clear();
        _tabsOverflowHandlers.Clear();
    }
}
