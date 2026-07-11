using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// PR-353 round-13 #1/#2/#3 — the resize and row-reorder pointer-commit paths now
/// route through an AWAITED internal commit (<see cref="Lumeo.DataGrid{TItem}.CommitColumnWidthAsync"/>,
/// <see cref="Lumeo.DataGrid{TItem}.MoveRowAsync"/>) instead of the public fire-and-forget
/// API (<c>UpdateColumnWidth</c>, <c>MoveRow</c>). Before this, the JS interop commit
/// promise — and the cross-engine drag arbiter token release chained off it — could
/// resolve while an async <c>OnColumnResize</c>/<c>OnRowReorder</c> consumer handler was
/// still persisting in the background, so a SLOWER earlier commit's persistence could
/// land after a FASTER later commit's, permanently reverting the stored value.
///
/// These tests exercise the awaited internal methods directly (the exact entry points
/// DataGridHeaderCell's commitHandler / ReorderRowByKeyAsync call) — JS is not under
/// test here (that's the Playwright harness's job); this proves the C# half of the
/// contract: the method's own returned Task does not complete until the consumer
/// handler does, so two properly-sequenced (awaited) commits can never have their
/// persistence interleave.
/// </summary>
public class DataGridAwaitedCommitRaceTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataGridAwaitedCommitRaceTests() => _ctx.AddLumeoServices();

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Row(int Id, string Name);

    private static List<Row> Data() => new() { new(1, "Alice"), new(2, "Bob"), new(3, "Charlie") };

    private static List<DataGridColumn<Row>> Columns(bool resizable) => new()
    {
        new() { Field = "Id", Title = "ID", Resizable = resizable },
    };

    // --- Resize (#1) ---

    [Fact]
    public async Task CommitColumnWidthAsync_Does_Not_Complete_Until_OnColumnResize_Handler_Does()
    {
        var tcs = new TaskCompletionSource();
        var handlerCompleted = false;
        var col = new DataGridColumn<Row> { Field = "Id", Title = "ID", Resizable = true };

        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.Columns, new List<DataGridColumn<Row>> { col })
            .Add(g => g.OnColumnResize, EventCallback.Factory.Create<ColumnResizeEventArgs>(this, async _ =>
            {
                await tcs.Task;
                handlerCompleted = true;
            })));

        // Block-bodied lambda (not expression-bodied) so this can ONLY resolve to
        // InvokeAsync(Action), not InvokeAsync(Func&lt;Task&gt;) — an expression-bodied
        // `() => commitTask = CommitColumnWidthAsync(...)` has a natural type of Task
        // and bUnit's overload resolution prefers Func&lt;Task&gt; for that shape, which
        // would AWAIT the still-pending commit task internally before InvokeAsync
        // itself returns — deadlocking here since tcs.SetResult() (below) never runs.
        Task commitTask = Task.CompletedTask;
        await cut.InvokeAsync(() => { commitTask = cut.Instance.CommitColumnWidthAsync(col.Id, 250); });

        // The consumer handler is still awaiting `tcs` — the commit's own Task must
        // still be pending too, proving it isn't dispatched fire-and-forget.
        Assert.False(commitTask.IsCompleted, "CommitColumnWidthAsync completed before its awaited OnColumnResize handler did.");
        Assert.False(handlerCompleted);

        tcs.SetResult();
        await commitTask;

        Assert.True(handlerCompleted);
    }

    [Fact]
    public async Task ResizeCommit_Awaited_Serializes_So_A_Slow_First_Persist_Cannot_Trail_A_Later_Commit()
    {
        var order = new List<double>();
        var col = new DataGridColumn<Row> { Field = "Id", Title = "ID", Resizable = true };

        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.Columns, new List<DataGridColumn<Row>> { col })
            .Add(g => g.OnColumnResize, EventCallback.Factory.Create<ColumnResizeEventArgs>(this, async args =>
            {
                // The FIRST commit's handler is deliberately the slower one — under
                // the old fire-and-forget dispatch this would finish AFTER the second
                // (faster) commit's handler and silently revert the persisted width.
                await Task.Delay(args.Width == 100 ? 60 : 5);
                order.Add(args.Width);
            })));

        await cut.InvokeAsync(async () =>
        {
            await cut.Instance.CommitColumnWidthAsync(col.Id, 100);
            await cut.Instance.CommitColumnWidthAsync(col.Id, 200);
        });

        // Persisted in COMMIT order, not completion-speed order: width=100's handler
        // had already fully finished before width=200's commit was even dispatched,
        // because the caller awaited between them (exactly what the JS arbiter's
        // held-until-settled token enforces end-to-end in production).
        Assert.Equal(new List<double> { 100, 200 }, order);
        Assert.Equal(200, col.Width);
    }

    // --- Row reorder (#2) ---

    [Fact]
    public async Task MoveRowAsync_Does_Not_Complete_Until_OnRowReorder_Handler_Does()
    {
        var tcs = new TaskCompletionSource();
        var handlerCompleted = false;

        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.Columns, Columns(resizable: false))
            .Add(g => g.RowReorderable, true)
            .Add(g => g.OnRowReorder, EventCallback.Factory.Create<RowReorderEventArgs<Row>>(this, async _ =>
            {
                await tcs.Task;
                handlerCompleted = true;
            })));

        // Block-bodied lambda — see CommitColumnWidthAsync_Does_Not_Complete_Until_
        // OnColumnResize_Handler_Does's remarks for why an expression-bodied one here
        // would deadlock.
        Task moveTask = Task.CompletedTask;
        await cut.InvokeAsync(() => { moveTask = cut.Instance.MoveRowAsync(0, 2); });

        Assert.False(moveTask.IsCompleted, "MoveRowAsync completed before its awaited OnRowReorder handler did.");
        Assert.False(handlerCompleted);

        tcs.SetResult();
        await moveTask;

        Assert.True(handlerCompleted);
    }

    [Fact]
    public async Task RowMoveCommit_Awaited_Serializes_So_A_Slow_First_Persist_Cannot_Trail_A_Later_Commit()
    {
        var order = new List<int>();

        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.Columns, Columns(resizable: false))
            .Add(g => g.RowReorderable, true)
            .Add(g => g.OnRowReorder, EventCallback.Factory.Create<RowReorderEventArgs<Row>>(this, async args =>
            {
                // First move (Alice -> slot 2) is deliberately the slower persist.
                await Task.Delay(args.NewIndex == 2 ? 60 : 5);
                order.Add(args.NewIndex);
            })));

        await cut.InvokeAsync(async () =>
        {
            await cut.Instance.MoveRowAsync(0, 2);
            await cut.Instance.MoveRowAsync(0, 1);
        });

        Assert.Equal(new List<int> { 2, 1 }, order);
    }

    // --- Resize-handle keyboard preventDefault (#3) ---

    [Fact]
    public void ResizeHandle_Registers_PreventDefault_For_Its_Own_Arrow_Keys()
    {
        // With the resize handle focused, plain ArrowLeft/ArrowRight resize the
        // column (HandleResizeKeyDown) — but @onkeydown:stopPropagation on the handle
        // keeps those keydowns from ever reaching the table-level
        // RegisterPreventDefaultKeys listener (registered on the grid/table for
        // header/cell nav), so without a scoped registration of its own the browser's
        // default page/grid scroll still fires on every keyboard resize step.
        var col = new DataGridColumn<Row> { Field = "Id", Title = "ID", Resizable = true };

        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.Columns, new List<DataGridColumn<Row>> { col }));

        var handleId = cut.Find("[data-slot='datagrid-resize-handle']").GetAttribute("id")!;

        var reg = Assert.Single(_ctx.JSInterop.Invocations,
            i => i.Identifier == "registerPreventDefaultKeys" && (string)i.Arguments[0]! == handleId);
        var rules = (IReadOnlyList<PreventDefaultKeyRule>)reg.Arguments[1]!;
        var keys = rules.Select(r => r.Key).ToList();

        Assert.Contains("ArrowLeft", keys);
        Assert.Contains("ArrowRight", keys);
    }

    [Fact]
    public async Task ResizeHandle_PreventDefault_Listener_Is_Torn_Down_On_Dispose()
    {
        // The listener is registered by DataGridHeaderCell (a CHILD of DataGrid), whose
        // own DisposeAsync unregisters it — DataGrid's own DisposeAsync doesn't cascade
        // to children when called directly, so this needs a dedicated context torn down
        // via the renderer (mirrors DataGridKeyboardPreventDefaultTests' table-level
        // dispose test, one level down the tree).
        using var ctx = new BunitContext();
        ctx.AddLumeoServices();

        var col = new DataGridColumn<Row> { Field = "Id", Title = "ID", Resizable = true };
        var cut = ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.Columns, new List<DataGridColumn<Row>> { col }));

        var handleId = cut.Find("[data-slot='datagrid-resize-handle']").GetAttribute("id")!;

        var unregBefore = ctx.JSInterop.Invocations.Count(i =>
            i.Identifier == "unregisterPreventDefaultKeys" && (string)i.Arguments[0]! == handleId);

        await ctx.DisposeAsync();

        var unregAfter = ctx.JSInterop.Invocations.Count(i =>
            i.Identifier == "unregisterPreventDefaultKeys" && (string)i.Arguments[0]! == handleId);

        Assert.True(unregAfter > unregBefore,
            "Disposing the grid must unregister the resize handle's own preventDefault key listener.");
    }
}
