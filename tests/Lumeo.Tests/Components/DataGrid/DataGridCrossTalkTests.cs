using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// Regression tests for the rc.19 fix that replaced the process-global static
/// drag-state holder in DataGridHeaderCell with an instance-bound drag-state
/// class (since removed entirely — see below).
///
/// The column-reorder AND row-reorder cross-talk cases that used to live here
/// (native HTML5 DnD: dragstart on grid A's header/row, drop on grid B's) were
/// removed with the ReUI-parity pass — neither column nor row reorder uses
/// native DnD any more. rc.42 finished the job: drag-to-group (the last native-
/// DnD gesture, and the last consumer of that instance-bound drag-state class)
/// was folded into the SAME unified pointer engine, and the drag-state class
/// itself (DataGridDragState) was deleted — there is no C# drag state left to
/// hold at all. The unified pointer paths (mouse + touch + pen, registered via
/// RegisterColumnReorder / RegisterRowReorder) are structurally immune to this
/// class of cross-talk: each grid registers its own JS listener scoped to its
/// own `[data-grid-id]` subtree and its own captured `dotnetRef`, so a drag
/// started in grid A's DOM can never resolve headers/rows or invoke the commit
/// callback belonging to grid B — there is no shared/global state to leak
/// through in the first place (see DataGridReorderConstraintTests /
/// DataGridRowReorderTests / DataGridReorderGroupPanelUnificationTests for the
/// pointer commit-path coverage that replaced these).
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
