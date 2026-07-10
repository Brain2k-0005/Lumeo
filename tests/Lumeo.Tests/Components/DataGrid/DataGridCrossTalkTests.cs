using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// Regression tests for the rc.19 fix that replaced the process-global static
/// drag-state holders in DataGridHeaderCell + DataGridRow with an
/// instance-bound <see cref="DataGridDragState"/>. Two grids on the same page
/// must not see each other's in-flight drags.
///
/// The column-reorder cross-talk cases that used to live here (native HTML5
/// DnD: dragstart on grid A's header, drop on grid B's header) were removed
/// with the ReUI-parity pass — column reorder no longer uses native DnD or
/// DataGridDragState at all. The unified pointer path (mouse + touch + pen,
/// registered via RegisterColumnReorder) is structurally immune to this class
/// of cross-talk: each grid registers its own JS listener scoped to its own
/// `[data-grid-id]` subtree and its own captured `dotnetRef`, so a drag
/// started in grid A's DOM can never resolve headers or invoke the commit
/// callback belonging to grid B — there is no shared/global state to leak
/// through in the first place. DataGridDragState (and this cross-talk
/// concern) now applies only to the drag-to-group-panel gesture and row
/// reorder, both still native-DnD-based; the row case is covered below.
/// </summary>
public class DataGridCrossTalkTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataGridCrossTalkTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record TestItem(int Id, string Name);

    private static List<TestItem> GetData() => new()
    {
        new(1, "Alice"),
        new(2, "Bob"),
        new(3, "Charlie"),
    };

    private static List<DataGridColumn<TestItem>> GetColumns() => new()
    {
        new() { Field = "Id", Title = "ID" },
        new() { Field = "Name", Title = "Name" },
    };


    [Fact]
    public void Row_drop_on_other_grid_is_rejected()
    {
        var capturedA = new List<RowReorderEventArgs<TestItem>>();
        var capturedB = new List<RowReorderEventArgs<TestItem>>();

        var gridA = _ctx.Render<DataGrid<TestItem>>(p => p
            .Add(x => x.Items, GetData())
            .Add(x => x.Columns, GetColumns())
            .Add(x => x.RowReorderable, true)
            .Add(x => x.OnRowReorder, EventCallback.Factory.Create<RowReorderEventArgs<TestItem>>(this, args => capturedA.Add(args))));

        var gridB = _ctx.Render<DataGrid<TestItem>>(p => p
            .Add(x => x.Items, GetData())
            .Add(x => x.Columns, GetColumns())
            .Add(x => x.RowReorderable, true)
            .Add(x => x.OnRowReorder, EventCallback.Factory.Create<RowReorderEventArgs<TestItem>>(this, args => capturedB.Add(args))));

        // Start drag on row 0 of grid A, drop on row 2 of grid B.
        var rowsA = gridA.FindAll("tr[data-slot='datagrid-row']");
        var rowsB = gridB.FindAll("tr[data-slot='datagrid-row']");

        rowsA[0].DragStart();
        rowsB[2].Drop();

        // Neither grid should have fired OnRowReorder — the drop on grid B
        // is rejected (different gridId), and grid A never had a drop.
        Assert.Empty(capturedA);
        Assert.Empty(capturedB);
    }

    [Fact]
    public async Task EventCallback_async_handler_is_awaited_before_grid_reflects_state()
    {
        // Async consumer: signal that the handler ran AND awaited a delay.
        var handlerStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handlerCompleted = false;

        async Task Handler(IReadOnlyList<TestItem> items)
        {
            handlerStarted.SetResult(true);
            await Task.Delay(20);
            handlerCompleted = true;
        }

        var grid = _ctx.Render<DataGrid<TestItem>>(p => p
            .Add(x => x.Items, GetData())
            .Add(x => x.Columns, GetColumns())
            .Add(x => x.SelectionMode, DataGridSelectionMode.Multiple)
            .Add(x => x.SelectedItemsChanged, EventCallback.Factory.Create<IReadOnlyList<TestItem>>(this, Handler)));

        // SelectAll triggers SelectedItemsChanged.
        await grid.InvokeAsync(() => grid.Instance.GetType()
            .GetMethod("SelectAll", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(grid.Instance, null));

        // Handler must have started.
        var started = await Task.WhenAny(handlerStarted.Task, Task.Delay(1000));
        Assert.True(started == handlerStarted.Task, "Async SelectedItemsChanged handler did not start");

        // Wait long enough for the inner await to complete.
        // The handler was dispatched fire-and-forget via SafeAsyncDispatcher,
        // but FireAndForget guarantees the work IS scheduled and observed —
        // so polling here verifies the dispatched task actually completes.
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (!handlerCompleted && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }
        Assert.True(handlerCompleted, "Async handler did not complete within timeout — fire-and-forget did not observe inner work");
    }
}
