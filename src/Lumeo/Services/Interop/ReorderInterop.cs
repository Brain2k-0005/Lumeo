using Microsoft.JSInterop;

namespace Lumeo.Services.Interop;

/// <summary>
/// JS interop for the DataGrid's pointer-based (touch/pen) column reorder. Each
/// grid registers one delegated pointer listener keyed by its grid id; on release
/// JS invokes the stored commit handler once with the dragged column id and the
/// drop-target column id. All per-move work lives in JS — .NET only receives the
/// single discrete commit — honouring the drag hot-path performance law.
/// </summary>
internal sealed class ReorderInterop
{
    // gridId -> (sourceColumnId, targetColumnId) => Task
    private readonly Dictionary<string, Func<string, string, Task>> _commitHandlers = new();

    public async ValueTask RegisterColumnReorder(
        IJSObjectReference module,
        DotNetObjectReference<ComponentInteropService> selfRef,
        string gridId,
        Func<string, string, Task> commitHandler)
    {
        _commitHandlers[gridId] = commitHandler;
        await module.InvokeVoidAsync("registerColumnReorder", gridId, selfRef);
    }

    public async ValueTask UnregisterColumnReorder(IJSObjectReference module, string gridId)
    {
        _commitHandlers.Remove(gridId);
        await module.InvokeVoidAsync("unregisterColumnReorder", gridId);
    }

    public async Task OnColumnReorderCommit(string gridId, string sourceColumnId, string targetColumnId)
    {
        if (_commitHandlers.TryGetValue(gridId, out var handler))
        {
            await handler(sourceColumnId, targetColumnId);
        }
    }

    // gridId -> (sourceRowKey, targetRowKey) => Task — vertical mirror of the
    // column commit-handler map above, for the pointer-based row-reorder engine.
    // Keyed by stable row identity (DataGridRowKeys, the same value backing
    // Blazor's own @key and the FLIP capture) rather than the plain DOM indices
    // JS measured at drag start: the commit is delayed until after the 180ms
    // settle animation, and if Items/_displayedItems changes underneath that
    // window (server refresh, filter, sort), stale indices would move whatever
    // rows currently occupy those slots instead of the ones the user actually
    // dragged. Resolving by key at commit time (DataGrid.ReorderRowByKeyAsync)
    // closes that window — mirrors how ReorderColumnByIdAsync already resolves
    // column ids fresh at call time (Codex round-5 #6).
    private readonly Dictionary<string, Func<string, string, Task>> _rowCommitHandlers = new();

    public async ValueTask RegisterRowReorder(
        IJSObjectReference module,
        DotNetObjectReference<ComponentInteropService> selfRef,
        string gridId,
        Func<string, string, Task> commitHandler)
    {
        _rowCommitHandlers[gridId] = commitHandler;
        await module.InvokeVoidAsync("registerRowReorder", gridId, selfRef);
    }

    public async ValueTask UnregisterRowReorder(IJSObjectReference module, string gridId)
    {
        _rowCommitHandlers.Remove(gridId);
        await module.InvokeVoidAsync("unregisterRowReorder", gridId);
    }

    public async Task OnRowReorderCommit(string gridId, string sourceRowKey, string targetRowKey)
    {
        if (_rowCommitHandlers.TryGetValue(gridId, out var handler))
        {
            await handler(sourceRowKey, targetRowKey);
        }
    }

    public void Clear()
    {
        _commitHandlers.Clear();
        _rowCommitHandlers.Clear();
    }
}
