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

    public void Clear() => _commitHandlers.Clear();
}
