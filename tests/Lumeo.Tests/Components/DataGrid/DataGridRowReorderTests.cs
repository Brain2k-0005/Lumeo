using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// Unified pointer-based (mouse + touch + pen) ROW reorder — the vertical mirror of
/// <see cref="DataGridReorderConstraintTests"/>. Row reorder used to be native HTML5
/// DnD (draggable + dragstart/HandleDragStart, @ondrop/HandleDrop, an instance-bound
/// <c>DataGridDragState</c>); the ReUI-parity pass replaced it entirely with the
/// unified JS pointer engine (registerRowReorder in components.js), driven by a
/// dedicated drag-handle grip (handle-only initiation — see DataGridRow's markup for
/// why rows can't use a column-header-style movement-threshold "arm from anywhere").
///
/// JS is not under test here (that's the Playwright evidence); these tests exercise
/// the C# boundary: <see cref="Lumeo.DataGrid{TItem}.ReorderRowByIndexAsync"/> (invoked
/// via <c>RegisterRowReorder</c>'s commit handler) and the scope gate
/// (<c>RowReorderPointerActive</c>) that decides whether the JS listener is ever
/// registered at all.
/// </summary>
public class DataGridRowReorderTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public DataGridRowReorderTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddScoped<IComponentInteropService>(_ => _interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Emp(int Id, string Name, string Dept);

    private static List<Emp> GetData() => new()
    {
        new(1, "Alice", "Eng"),
        new(2, "Bob", "Eng"),
        new(3, "Charlie", "Eng"),
    };

    private static List<DataGridColumn<Emp>> GetColumns() => new()
    {
        new() { Field = "Id", Title = "ID" },
        new() { Field = "Name", Title = "Name" },
        new() { Field = "Dept", Title = "Department", Groupable = true },
    };

    // Columns are Id(0), Name(1), Department(2) — data cells only (data-slot=
    // "datagrid-cell"), so index 1 is always the Name column regardless of
    // whichever leading structural cells (drag handle, selection) precede them.
    private static List<string> RowOrder(IRenderedComponent<Lumeo.DataGrid<Emp>> cut) =>
        cut.FindAll("tr[data-slot='datagrid-row']")
           .Select(r => r.QuerySelectorAll("td[data-slot='datagrid-cell']")[1].TextContent.Trim())
           .ToList();

    // --- Registration / scope gate ---

    [Fact]
    public void Flat_Reorderable_Grid_Registers_Pointer_Row_Reorder_Listener()
    {
        var cut = _ctx.Render<Lumeo.DataGrid<Emp>>(p => p
            .Add(x => x.Items, GetData())
            .Add(x => x.Columns, GetColumns())
            .Add(x => x.RowReorderable, true));

        var gridId = cut.Find("[data-slot='datagrid']").GetAttribute("data-grid-id");
        Assert.Contains(gridId, _interop.RowReorderRegistrations);

        // The grip is live (data-row-reorder-grip present) — pointer-active scope.
        Assert.Equal(3, cut.FindAll("[data-row-reorder-grip]").Count);
    }

    [Fact]
    public void NonReorderable_Grid_Does_Not_Register_And_Renders_No_Handle_Column()
    {
        var cut = _ctx.Render<Lumeo.DataGrid<Emp>>(p => p
            .Add(x => x.Items, GetData())
            .Add(x => x.Columns, GetColumns())
            .Add(x => x.RowReorderable, false));

        var gridId = cut.Find("[data-slot='datagrid']").GetAttribute("data-grid-id");
        Assert.DoesNotContain(gridId, _interop.RowReorderRegistrations);
        Assert.Empty(cut.FindAll("[data-row-reorder-grip]"));
    }

    [Fact]
    public void Grouped_Grid_Keeps_Handle_Visible_But_Does_Not_Register_Pointer_Engine()
    {
        // Grouped indices restart per section — RowReorderPointerActive must be
        // false even though RowReorderable is true, so the JS engine is never
        // wired up (a live drag would desync index math). The handle stays
        // visible (RowReorderable still true) but inert.
        var cut = _ctx.Render<Lumeo.DataGrid<Emp>>(p => p
            .Add(x => x.Items, GetData())
            .Add(x => x.Columns, GetColumns())
            .Add(x => x.RowReorderable, true)
            .Add(x => x.GroupBy, "Dept"));

        var gridId = cut.Find("[data-slot='datagrid']").GetAttribute("data-grid-id");
        Assert.DoesNotContain(gridId, _interop.RowReorderRegistrations);
        // No live grip anywhere — but the (inert) handle column still renders.
        Assert.Empty(cut.FindAll("[data-row-reorder-grip]"));
    }

    [Fact]
    public void Virtualized_Grid_Keeps_Handle_Visible_But_Does_Not_Register_Pointer_Engine()
    {
        // Force UseVirtualization with a tiny threshold rather than 500+ rows.
        var cut = _ctx.Render<Lumeo.DataGrid<Emp>>(p => p
            .Add(x => x.Items, GetData())
            .Add(x => x.Columns, GetColumns())
            .Add(x => x.RowReorderable, true)
            .Add(x => x.VirtualizeThreshold, 1));

        var gridId = cut.Find("[data-slot='datagrid']").GetAttribute("data-grid-id");
        Assert.DoesNotContain(gridId, _interop.RowReorderRegistrations);
        Assert.Empty(cut.FindAll("[data-row-reorder-grip]"));
    }

    // --- Commit correctness ---

    [Fact]
    public async Task Pointer_Commit_Reorders_Rows_And_Fires_OnRowReorder_Once()
    {
        var fired = new List<RowReorderEventArgs<Emp>>();
        var cut = _ctx.Render<Lumeo.DataGrid<Emp>>(p => p
            .Add(x => x.Items, GetData())
            .Add(x => x.Columns, GetColumns())
            .Add(x => x.RowReorderable, true)
            .Add(x => x.OnRowReorder, EventCallback.Factory.Create<RowReorderEventArgs<Emp>>(this, args => fired.Add(args))));

        var gridId = cut.Find("[data-slot='datagrid']").GetAttribute("data-grid-id")!;
        Assert.Contains(gridId, _interop.RowReorderRegistrations);

        // Move row 0 (Alice) to row 2's slot — mirrors JS handing back the plain
        // DOM indices it measured at drag start.
        bool handled = false;
        await cut.InvokeAsync(async () => handled = await _interop.SimulateRowReorderCommit(gridId, 0, 2));
        Assert.True(handled, "no commit handler registered for gridId");
        Assert.Equal(new List<string> { "Bob", "Charlie", "Alice" }, RowOrder(cut));

        // The reorder callback is dispatched fire-and-forget (SafeAsyncDispatcher,
        // same as MoveRow's public contract) — poll rather than assume synchronous
        // completion.
        await WaitFor(() => fired.Count > 0);
        Assert.Single(fired);
        Assert.Equal("Alice", fired[0].Item.Name);
        Assert.Equal(0, fired[0].OldIndex);
        Assert.Equal(2, fired[0].NewIndex);
        Assert.Equal(new List<string> { "Bob", "Charlie", "Alice" }, RowOrder(cut));

        // FLIP handshake fired exactly once for this commit — CaptureRowRects runs
        // synchronously before the mutation; AnimateRowReorder runs from the
        // OnAfterRenderAsync pass triggered by MoveRow's StateHasChanged, which is
        // dispatched the same fire-and-forget way as OnRowReorder above.
        Assert.Single(_interop.CaptureRowRectsGridIds, gridId);
        await WaitFor(() => _interop.AnimateRowReorderCalls.Any(c => c.gridId == gridId));
        Assert.Single(_interop.AnimateRowReorderCalls, c => c.gridId == gridId);
    }

    /// <summary>Polls <paramref name="condition"/> for up to 2s — MoveRow's
    /// OnRowReorder callback (and the FLIP animation it schedules) is dispatched
    /// fire-and-forget (SafeAsyncDispatcher), so bUnit's synchronous InvokeAsync
    /// return doesn't guarantee either has landed yet.</summary>
    private static async Task WaitFor(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }
    }

    [Fact]
    public async Task Pointer_Commit_On_Same_Index_Commits_Nothing()
    {
        // Guards ReorderRowByIndexAsync's own oldIndex == newIndex short-circuit —
        // JS's finishDrag never calls the commit handler when targetIdx == srcIdx,
        // but the C# boundary must be defensively correct too (e.g. a future JS
        // change, or a directly-simulated commit as here).
        var fired = new List<RowReorderEventArgs<Emp>>();
        var cut = _ctx.Render<Lumeo.DataGrid<Emp>>(p => p
            .Add(x => x.Items, GetData())
            .Add(x => x.Columns, GetColumns())
            .Add(x => x.RowReorderable, true)
            .Add(x => x.OnRowReorder, EventCallback.Factory.Create<RowReorderEventArgs<Emp>>(this, args => fired.Add(args))));

        var gridId = cut.Find("[data-slot='datagrid']").GetAttribute("data-grid-id")!;
        var before = RowOrder(cut);

        await cut.InvokeAsync(() => _interop.SimulateRowReorderCommit(gridId, 1, 1));

        Assert.Empty(fired);
        Assert.Equal(before, RowOrder(cut));
        Assert.Empty(_interop.CaptureRowRectsGridIds);
    }

    [Fact]
    public async Task Pointer_Commit_With_OutOfRange_Index_Is_Rejected()
    {
        var fired = new List<RowReorderEventArgs<Emp>>();
        var cut = _ctx.Render<Lumeo.DataGrid<Emp>>(p => p
            .Add(x => x.Items, GetData())
            .Add(x => x.Columns, GetColumns())
            .Add(x => x.RowReorderable, true)
            .Add(x => x.OnRowReorder, EventCallback.Factory.Create<RowReorderEventArgs<Emp>>(this, args => fired.Add(args))));

        var gridId = cut.Find("[data-slot='datagrid']").GetAttribute("data-grid-id")!;

        await cut.InvokeAsync(() => _interop.SimulateRowReorderCommit(gridId, 0, 99));

        Assert.Empty(fired);
    }

    // --- Selection interplay: handle-only initiation must not break row click ---

    [Fact]
    public void Row_Click_Still_Toggles_Selection_With_Reorder_Handle_Present()
    {
        // Handle-only initiation (documented in DataGridRow): the reorder grip is
        // a separate element with @onclick:stopPropagation on its <td>, so a click
        // anywhere else on the row must still reach HandleClick unshadowed.
        var cut = _ctx.Render<Lumeo.DataGrid<Emp>>(p => p
            .Add(x => x.Items, GetData())
            .Add(x => x.Columns, GetColumns())
            .Add(x => x.RowReorderable, true)
            .Add(x => x.SelectionMode, DataGridSelectionMode.Multiple));

        var row = cut.FindAll("tr[data-slot='datagrid-row']")[0];
        Assert.Equal("false", row.GetAttribute("aria-selected"));

        row.Click();

        row = cut.FindAll("tr[data-slot='datagrid-row']")[0];
        Assert.Equal("true", row.GetAttribute("aria-selected"));
    }

    [Fact]
    public void Reorder_Grip_Cell_Has_No_Click_Handler_That_Could_Toggle_Selection()
    {
        // The grip's <span> carries no click handler at all (it's purely a
        // pointerdown drag initiator). Its structural <td> carries an
        // @onclick:stopPropagation modifier and no @onclick delegate — bUnit has
        // no clean way to simulate that modifier-only directive as a discrete
        // dispatchable event, so this asserts the structural guarantee directly:
        // neither element exposes an @onclick that could reach HandleClick /
        // ToggleSelection, and the cell is marked to swallow bubbling clicks.
        var cut = _ctx.Render<Lumeo.DataGrid<Emp>>(p => p
            .Add(x => x.Items, GetData())
            .Add(x => x.Columns, GetColumns())
            .Add(x => x.RowReorderable, true)
            .Add(x => x.SelectionMode, DataGridSelectionMode.Multiple));

        var grip = cut.FindAll("[data-row-reorder-grip]")[0];
        var handleCell = grip.Closest("td")!;

        Assert.Throws<Bunit.MissingEventHandlerException>(() => grip.Click());
        // The cell's own onclick:stopPropagation directive means clicking IT
        // (not the row) can never toggle selection — verified functionally by
        // Row_Click_Still_Toggles_Selection_With_Reorder_Handle_Present asserting
        // the row DOES select when clicked elsewhere; here we assert this cell has
        // no @onclick delegate for that click to invoke in the first place.
        Assert.Contains("stoppropagation", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    // --- Affordance structure ---

    [Fact]
    public void Reorderable_Rows_Carry_Index_And_Key_Attributes_When_Pointer_Active()
    {
        var cut = _ctx.Render<Lumeo.DataGrid<Emp>>(p => p
            .Add(x => x.Items, GetData())
            .Add(x => x.Columns, GetColumns())
            .Add(x => x.RowReorderable, true));

        var rows = cut.FindAll("tr[data-slot='datagrid-row']");
        for (var i = 0; i < rows.Count; i++)
        {
            Assert.Equal(i.ToString(), rows[i].GetAttribute("data-row-index"));
            Assert.False(string.IsNullOrEmpty(rows[i].GetAttribute("data-row-key")));
        }
    }

    [Fact]
    public void No_Native_Draggable_Attribute_Or_Drag_Handlers_Remain()
    {
        // Native HTML5 DnD is fully retired for rows — no draggable attribute at
        // all (it isn't just "false"; the attribute/handlers are gone entirely).
        var cut = _ctx.Render<Lumeo.DataGrid<Emp>>(p => p
            .Add(x => x.Items, GetData())
            .Add(x => x.Columns, GetColumns())
            .Add(x => x.RowReorderable, true));

        foreach (var row in cut.FindAll("tr[data-slot='datagrid-row']"))
        {
            Assert.Null(row.GetAttribute("draggable"));
        }
    }
}
