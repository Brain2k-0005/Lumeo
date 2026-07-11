using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// Pinned-column reorder constraint: a column may only be reordered WITHIN its pin
/// partition (left / unpinned / right). A cross-partition drop is silently undone by
/// the pin re-partition on the next render, so the grid must reject it up front — no
/// reorder event — via the single unified reorder commit path (mouse + touch + pen)
/// routed through RegisterColumnReorder → ReorderColumnByIdAsync. Same-partition
/// reorders are the control and must still work.
///
/// The native-DnD header-to-header drag/drop-indicator tests that used to live here
/// were removed with the ReUI-parity pass: column reorder no longer uses native HTML5
/// DnD at all (dragstart/dragenter/drop-on-header + the glow indicator are gone from
/// DataGridHeaderCell) — every pointer type now drives the same JS pointer-based path,
/// which enforces this exact constraint client-side before ever calling .NET. The
/// constraint's authoritative enforcement point is (and always was) this C# boundary,
/// which the tests below still exercise directly.
/// </summary>
public class DataGridReorderConstraintTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public DataGridReorderConstraintTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddScoped<IComponentInteropService>(_ => _interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Row(int Id, string Name, string Dept);

    private static List<Row> Data() => new() { new(1, "Alice", "Eng"), new(2, "Bob", "Sales") };

    // A = pinned Left, B + C = unpinned. Partition order: A | B, C.
    private (DataGridColumn<Row> a, DataGridColumn<Row> b, DataGridColumn<Row> c) Cols()
        => (
            new DataGridColumn<Row> { Field = "Id", Title = "A", Pin = PinDirection.Left },
            new DataGridColumn<Row> { Field = "Name", Title = "B" },
            new DataGridColumn<Row> { Field = "Dept", Title = "C" });

    private IRenderedComponent<Lumeo.DataGrid<Row>> RenderGrid(
        List<DataGridColumn<Row>> cols, Action<ColumnReorderEventArgs>? onReorder = null)
        => _ctx.Render<Lumeo.DataGrid<Row>>(p =>
        {
            p.Add(g => g.Items, Data());
            p.Add(g => g.Columns, cols);
            p.Add(g => g.Reorderable, true);
            if (onReorder is not null) p.Add(g => g.OnColumnReorder, onReorder);
        });

    private static List<string?> HeaderOrder(IRenderedComponent<Lumeo.DataGrid<Row>> cut)
        => cut.FindAll("th[data-slot='datagrid-header-cell']")
              .Select(h => h.GetAttribute("data-col-id")).ToList();

    // --- Unified pointer path (RegisterColumnReorder commit — mouse/touch/pen) ---

    [Fact]
    public void Reorderable_Grid_Registers_Pointer_Reorder_Listener()
    {
        var (a, b, c) = Cols();
        var cut = RenderGrid(new() { a, b, c });

        var gridId = cut.Find("[data-slot='datagrid']").GetAttribute("data-grid-id");
        Assert.Contains(gridId, _interop.ColumnReorderRegistrations);
    }

    [Fact]
    public async Task Pointer_CrossPartition_Commit_Is_Rejected()
    {
        var (a, b, c) = Cols();
        ColumnReorderEventArgs? fired = null;
        var cut = RenderGrid(new() { a, b, c }, args => fired = args);
        var gridId = cut.Find("[data-slot='datagrid']").GetAttribute("data-grid-id")!;
        var before = HeaderOrder(cut);

        // Drop pinned A onto unpinned C — different partitions.
        await cut.InvokeAsync(() => _interop.SimulateColumnReorderCommit(gridId, a.Id, c.Id));

        Assert.Null(fired);
        Assert.Equal(before, HeaderOrder(cut));
    }

    [Fact]
    public async Task Pointer_SamePartition_Commit_Reorders()
    {
        var (a, b, c) = Cols();
        ColumnReorderEventArgs? fired = null;
        var cut = RenderGrid(new() { a, b, c }, args => fired = args);
        var gridId = cut.Find("[data-slot='datagrid']").GetAttribute("data-grid-id")!;

        // Move unpinned B to C's slot — same (None) partition.
        await cut.InvokeAsync(() => _interop.SimulateColumnReorderCommit(gridId, b.Id, c.Id));

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(fired);
            Assert.Equal(b.Id, fired!.ColumnId);
            // A stays pinned first; B and C have swapped.
            Assert.Equal(new List<string?> { a.Id, c.Id, b.Id }, HeaderOrder(cut));
        });
    }

    [Fact]
    public async Task Pointer_NonReorderable_Column_Commit_Is_Rejected()
    {
        var a = new DataGridColumn<Row> { Field = "Id", Title = "A", Reorderable = false };
        var b = new DataGridColumn<Row> { Field = "Name", Title = "B" };
        var c = new DataGridColumn<Row> { Field = "Dept", Title = "C" };
        ColumnReorderEventArgs? fired = null;
        var cut = RenderGrid(new() { a, b, c }, args => fired = args);
        var gridId = cut.Find("[data-slot='datagrid']").GetAttribute("data-grid-id")!;
        var before = HeaderOrder(cut);

        await cut.InvokeAsync(() => _interop.SimulateColumnReorderCommit(gridId, a.Id, c.Id));

        Assert.Null(fired);
        Assert.Equal(before, HeaderOrder(cut));
    }

    [Fact]
    public async Task Pointer_Drop_Onto_NonReorderable_Target_Is_Rejected()
    {
        // A locked column must be un-displaceable: a reorderable source dropped
        // ONTO a Reorderable=false target's slot must be refused too, not just a
        // non-reorderable source dragged elsewhere (that's the test above).
        var a = new DataGridColumn<Row> { Field = "Id", Title = "A" };
        var b = new DataGridColumn<Row> { Field = "Name", Title = "B", Reorderable = false };
        var c = new DataGridColumn<Row> { Field = "Dept", Title = "C" };
        ColumnReorderEventArgs? fired = null;
        var cut = RenderGrid(new() { a, b, c }, args => fired = args);
        var gridId = cut.Find("[data-slot='datagrid']").GetAttribute("data-grid-id")!;
        var before = HeaderOrder(cut);

        // Drop reorderable A onto locked B's slot.
        await cut.InvokeAsync(() => _interop.SimulateColumnReorderCommit(gridId, a.Id, b.Id));

        Assert.Null(fired);
        Assert.Equal(before, HeaderOrder(cut));
    }

    [Fact]
    public async Task Pointer_Commit_Skipping_Over_Locked_Column_Keeps_It_At_Its_Absolute_Index()
    {
        // [A, locked B, C], drag A onto C. The JS live preview never displaces B
        // (it's un-displaceable — the dragged column skips over it in place), so
        // the .NET commit must not either: a plain RemoveAt(0)+Insert(2) over the
        // FULL array would yield [B, C, A], sliding locked B from absolute index 1
        // to 0 even though it never visually moved. Reordering within just the
        // reorderable subsequence (A, C) and splicing locked B back into its own
        // fixed slot yields [C, B, A] instead — B's absolute index is unchanged.
        var a = new DataGridColumn<Row> { Field = "Id", Title = "A" };
        var b = new DataGridColumn<Row> { Field = "Name", Title = "B", Reorderable = false };
        var c = new DataGridColumn<Row> { Field = "Dept", Title = "C" };
        ColumnReorderEventArgs? fired = null;
        var cut = RenderGrid(new() { a, b, c }, args => fired = args);
        var gridId = cut.Find("[data-slot='datagrid']").GetAttribute("data-grid-id")!;

        await cut.InvokeAsync(() => _interop.SimulateColumnReorderCommit(gridId, a.Id, c.Id));

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(fired);
            Assert.Equal(a.Id, fired!.ColumnId);
            Assert.Equal(new List<string?> { c.Id, b.Id, a.Id }, HeaderOrder(cut));
        });
    }

    [Fact]
    public async Task Pointer_Commit_Skipping_Over_Locked_Column_Dragging_Right_To_Left()
    {
        // Mirror of the above in the opposite direction: [A, locked B, C], drag C
        // onto A. Reorderable subsequence (A, C) becomes (C, A); locked B stays
        // pinned at absolute index 1 -> final order [C, B, A].
        var a = new DataGridColumn<Row> { Field = "Id", Title = "A" };
        var b = new DataGridColumn<Row> { Field = "Name", Title = "B", Reorderable = false };
        var c = new DataGridColumn<Row> { Field = "Dept", Title = "C" };
        ColumnReorderEventArgs? fired = null;
        var cut = RenderGrid(new() { a, b, c }, args => fired = args);
        var gridId = cut.Find("[data-slot='datagrid']").GetAttribute("data-grid-id")!;

        await cut.InvokeAsync(() => _interop.SimulateColumnReorderCommit(gridId, c.Id, a.Id));

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(fired);
            Assert.Equal(c.Id, fired!.ColumnId);
            Assert.Equal(new List<string?> { c.Id, b.Id, a.Id }, HeaderOrder(cut));
        });
    }

    [Fact]
    public async Task Index_Based_Redirect_Off_A_Locked_Edge_Column_Never_Escapes_Its_Pin_Group()
    {
        // Round-7 #3: [A(None), locked B(None), C(Right)] — B sits at the very
        // edge of the unpinned partition, immediately before the right-pinned
        // one. Dragging A onto B's slot (index 1) hits the column-CHOOSER'S
        // index-based path (DataGridColumnVisibility -> HandleColumnReorder),
        // NOT the pointer/id-based ReorderColumnByIdAsync (which already
        // refuses a locked TARGET outright and so never reaches the redirect
        // loop at all). HandleColumnReorder's own cross-pin guard only checks
        // the ORIGINAL clamped target (B, still Pin=None — passes); the
        // skip-over loop inside ReorderColumnsPreservingLocked used to walk
        // straight past B into C because C is Reorderable, without checking
        // C's Pin differs from B's/A's. This must resolve to a no-op instead
        // of redirecting the drop into the right-pinned partition.
        var a = new DataGridColumn<Row> { Field = "Id", Title = "A" };
        var b = new DataGridColumn<Row> { Field = "Name", Title = "B", Reorderable = false };
        var c = new DataGridColumn<Row> { Field = "Dept", Title = "C", Pin = PinDirection.Right };
        ColumnReorderEventArgs? fired = null;
        var cut = RenderGrid(new() { a, b, c }, args => fired = args);
        var before = HeaderOrder(cut);

        // Directly drives the index-based path HandleColumnReorder exposes to
        // the column chooser — there is no ID-based equivalent that can even
        // reach a locked target's slot (see the remark above).
        var method = cut.Instance.GetType().GetMethod("HandleColumnReorder",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        await cut.InvokeAsync(async () =>
        {
            var task = (Task)method.Invoke(cut.Instance, new object[] { new ColumnReorderEventArgs(a.Id, 0, 1) })!;
            await task;
        });

        Assert.Null(fired);
        Assert.Equal(before, HeaderOrder(cut)); // right-pinned C never displaced, B never moved
    }

    // --- Affordance structure ---

    [Fact]
    public void Reorderable_Columns_Render_Grip_And_Pin_Metadata()
    {
        var (a, b, c) = Cols();
        var cut = RenderGrid(new() { a, b, c });

        // Every reorderable header carries a drag grip (mouse + touch + pen all
        // initiate from it; mouse can ALSO initiate from the rest of the header —
        // see the JS pointer path — but the grip is the universal affordance).
        Assert.Equal(3, cut.FindAll("[data-reorder-grip]").Count);
        // Every reorderable header exposes the marker the JS pointer path keys off
        // for its mouse header-wide initiation.
        Assert.Equal(3, cut.FindAll("th[data-reorderable='true']").Count);
        // The pinned column's th advertises its partition for the JS reorder clamp.
        var pinnedHeader = cut.FindAll("th[data-slot='datagrid-header-cell']")
                              .First(h => h.GetAttribute("data-col-id") == a.Id);
        Assert.Equal("Left", pinnedHeader.GetAttribute("data-col-pin"));
    }

    // --- Native DnD removal (ReUI-parity pass) ---
    //
    // Column reorder no longer uses native HTML5 DnD at all — the unified JS
    // pointer path (above) owns mouse + touch + pen. Native DnD is kept for
    // exactly one, unrelated gesture: dragging a Groupable column into the group
    // panel (DataGridDragOverHotPathTests / DataGridGroupPanelTests cover that).

    [Fact]
    public void Reorderable_NonGroupable_Header_Is_Not_Natively_Draggable()
    {
        var (a, b, c) = Cols();
        var cut = RenderGrid(new() { a, b, c }); // no ShowGroupPanel — nothing is Groupable here

        foreach (var header in cut.FindAll("th[data-slot='datagrid-header-cell']"))
        {
            Assert.Equal("false", header.GetAttribute("draggable"));
        }
    }

    [Fact]
    public void Groupable_Header_With_GroupPanel_Stays_Natively_Draggable_For_GroupDrag()
    {
        var cols = new List<DataGridColumn<Row>>
        {
            new() { Field = "Id", Title = "A" },
            new() { Field = "Dept", Title = "B", Groupable = true },
        };
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p =>
        {
            p.Add(g => g.Items, Data());
            p.Add(g => g.Columns, cols);
            p.Add(g => g.Reorderable, true);
            p.Add(g => g.ShowGroupPanel, true);
        });

        // Groupable + ShowGroupPanel keeps native draggable="true" (drag-to-group);
        // it also stays reorderable — the grip is that column's sole reorder
        // initiator so the two gestures don't compete over the same pointerdown.
        var groupableHeader = cut.FindAll("th[data-slot='datagrid-header-cell']")[1];
        Assert.Equal("true", groupableHeader.GetAttribute("draggable"));
        Assert.Equal("true", groupableHeader.GetAttribute("data-reorderable"));
    }

    [Fact]
    public void No_Drop_Indicator_Markup_Rendered_Anymore()
    {
        var (a, b, c) = Cols();
        var cut = RenderGrid(new() { a, b, c });

        // The glow drop-indicator div (and its class) is gone entirely — live
        // sibling-shift replaces it, driven purely in JS with no DOM marker to
        // assert on at the bUnit level (see the Playwright evidence for that).
        Assert.DoesNotContain("lumeo-datagrid-drop-indicator", cut.Markup);
    }

    [Fact]
    public void Reorderable_Header_Click_Still_Sorts()
    {
        // Guards the click-vs-drag disambiguation at the C# boundary: the sort
        // button's @onclick binding must still fire even though the header is
        // ALSO wired for pointer-based reorder (grip + header-wide mouse init
        // with a movement threshold — the threshold itself is JS-only and is
        // exercised in the Playwright evidence, not here).
        var cols = new List<DataGridColumn<Row>>
        {
            new() { Field = "Id", Title = "A", Sortable = true },
            new() { Field = "Name", Title = "B" },
        };
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p =>
        {
            p.Add(g => g.Items, Data());
            p.Add(g => g.Columns, cols);
            p.Add(g => g.Reorderable, true);
        });

        var header = cut.FindAll("th[data-slot='datagrid-header-cell']")[0];
        Assert.Equal("none", header.GetAttribute("aria-sort"));

        header.QuerySelector("button")!.Click();

        header = cut.FindAll("th[data-slot='datagrid-header-cell']")[0];
        Assert.Equal("ascending", header.GetAttribute("aria-sort"));
    }
}
