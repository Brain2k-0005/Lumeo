using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// Regression tests for column PINNING direction + placement.
///
/// The reported defect: pinning a column RIGHT (or unpinning) from the column
/// chooser left the column visually on the LEFT. Root cause — the grid set the
/// correct <c>position: sticky; right: Npx</c> offset but never physically moved
/// the column in DOM order, and <c>position: sticky</c> can only anchor a cell to
/// a scroll-container edge, it cannot relocate the cell across its siblings. A
/// right-pinned leading column therefore kept rendering on the left with a
/// <c>right:</c> offset that never engaged.
///
/// The fix stable-partitions the columns (left-pinned → unpinned → right-pinned)
/// in <c>RefreshVisibleColumns</c>, so the DOM order matches the sticky edge for
/// every pin path. These tests assert the DOM ORDER (not just the offset) so they
/// fail against the offset-only implementation.
/// </summary>
public class DataGridColumnPinTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataGridColumnPinTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Row(int Id, string Name);

    private static List<Row> Data() => new()
    {
        new(1, "Alice"), new(2, "Bob"), new(3, "Charlie"),
    };

    private IRenderedComponent<DataGrid<Row>> RenderGrid(List<DataGridColumn<Row>> cols)
        => _ctx.Render<DataGrid<Row>>(p => p
            .Add(x => x.Items, Data())
            .Add(x => x.Columns, cols)
            .Add(x => x.ShowPagination, false)
            .Add(x => x.ShowToolbar, false));

    // Ordered data-column header cells (leading structural th's carry no data-slot).
    private static IReadOnlyList<AngleSharp.Dom.IElement> HeaderCells(IRenderedComponent<DataGrid<Row>> cut)
        => cut.FindAll("th[data-slot=\"datagrid-header-cell\"]");

    private static string Style(AngleSharp.Dom.IElement th) => th.GetAttribute("style") ?? "";
    private static string ColId(AngleSharp.Dom.IElement th) => th.GetAttribute("data-col-id") ?? "";

    // ---------------------------------------------------------------------------
    // Core direction / placement
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task PinRight_On_Leading_Column_Moves_It_To_Right_Edge_And_Sticks()
    {
        var a = new DataGridColumn<Row> { Field = "A", Title = "Alpha", Pinnable = true };
        var b = new DataGridColumn<Row> { Field = "B", Title = "Bravo" };
        var c = new DataGridColumn<Row> { Field = "C", Title = "Charlie" };
        var cut = RenderGrid(new() { a, b, c });

        // Runtime pin of the LEADING column to the RIGHT — the exact reported case.
        await cut.InvokeAsync(() => cut.Instance.ApplyColumnPin(a.Id, PinDirection.Right));

        var cells = HeaderCells(cut);
        // A must now be the LAST (right-most) data column in DOM order, not the first.
        Assert.Equal(a.Id, ColId(cells[^1]));
        var aTh = cells[^1];
        Assert.Contains("sticky", aTh.ClassName);
        Assert.Contains("right:", Style(aTh));   // anchored to the RIGHT edge
        Assert.DoesNotContain("left:", Style(aTh));
    }

    [Fact]
    public void MultipleRightPins_Stack_Offsets_From_The_Right_Edge()
    {
        var a = new DataGridColumn<Row> { Field = "A", Title = "Alpha" };
        var b = new DataGridColumn<Row> { Field = "B", Title = "Bravo", Pinnable = true, Pin = PinDirection.Right, Width = 100 };
        var c = new DataGridColumn<Row> { Field = "C", Title = "Charlie", Pinnable = true, Pin = PinDirection.Right, Width = 150 };
        // Declared OUT of partitioned order (right pins first) so the test also
        // guards the reorder, not just the offset stacking.
        var cut = RenderGrid(new() { b, c, a });

        var cells = HeaderCells(cut);
        // DOM order must be re-partitioned: unpinned A, then the right group [B, C].
        Assert.Equal(new[] { a.Id, b.Id, c.Id }, cells.Select(ColId).ToArray());

        // The right-most (last) pin sits flush at right:0; the one to its left is
        // offset by the right-most column's width (150px) — stacked from the edge.
        Assert.Contains("right: 0px", Style(cells[2]));    // C is flush right
        Assert.Contains("right: 150px", Style(cells[1]));  // B stacked left of C
    }

    [Fact]
    public async Task Unpin_Clears_Sticky_And_Returns_Column_To_Normal_Flow()
    {
        var a = new DataGridColumn<Row> { Field = "A", Title = "Alpha", Pinnable = true };
        var b = new DataGridColumn<Row> { Field = "B", Title = "Bravo" };
        var c = new DataGridColumn<Row> { Field = "C", Title = "Charlie" };
        var cut = RenderGrid(new() { a, b, c });

        await cut.InvokeAsync(() => cut.Instance.ApplyColumnPin(a.Id, PinDirection.Right));
        var aTh = HeaderCells(cut).First(t => ColId(t) == a.Id);
        Assert.Contains("sticky", aTh.ClassName);          // pinned right first

        await cut.InvokeAsync(() => cut.Instance.ApplyColumnPin(a.Id, PinDirection.None));

        // Unpin drops A back into normal flow: no sticky, no edge offset. With no
        // remaining pins the whole header is free-scrolling.
        aTh = HeaderCells(cut).First(t => ColId(t) == a.Id);
        Assert.DoesNotContain("sticky", aTh.ClassName);
        Assert.DoesNotContain("right:", Style(aTh));
        Assert.DoesNotContain("left:", Style(aTh));
        Assert.DoesNotContain(HeaderCells(cut), t => (t.ClassName ?? "").Contains("sticky"));
    }

    [Fact]
    public void Mixed_Left_And_Right_Pins_Anchor_Opposite_Edges()
    {
        var a = new DataGridColumn<Row> { Field = "A", Title = "Alpha", Pinnable = true, Pin = PinDirection.Left };
        var b = new DataGridColumn<Row> { Field = "B", Title = "Bravo" };
        var c = new DataGridColumn<Row> { Field = "C", Title = "Charlie", Pinnable = true, Pin = PinDirection.Right };
        // Declared OUT of partitioned order (right, left, center) so the test also
        // guards the reorder into [left, center, right].
        var cut = RenderGrid(new() { c, a, b });

        var cells = HeaderCells(cut);
        Assert.Equal(new[] { a.Id, b.Id, c.Id }, cells.Select(ColId).ToArray());

        Assert.Contains("left:", Style(cells[0]));     // A anchored left
        Assert.Contains("sticky", cells[0].ClassName);
        Assert.DoesNotContain("sticky", cells[1].ClassName);   // B free-scrolling
        Assert.Contains("right:", Style(cells[2]));    // C anchored right
        Assert.Contains("sticky", cells[2].ClassName);
    }

    // ---------------------------------------------------------------------------
    // Header-level pin affordance
    // ---------------------------------------------------------------------------

    [Fact]
    public void Header_Renders_Pin_Control_Only_For_Pinnable_Columns()
    {
        var a = new DataGridColumn<Row> { Field = "A", Title = "Alpha", Pinnable = true };
        var b = new DataGridColumn<Row> { Field = "B", Title = "Bravo", Pinnable = false };
        var cut = RenderGrid(new() { a, b });

        // Exactly one header pin trigger (A's) — B is not pinnable, and the toolbar
        // column chooser (the other place with this aria-label) is off.
        var triggers = cut.FindAll("button[aria-label=\"Pin column\"]");
        Assert.Single(triggers);

        var aTh = HeaderCells(cut).First(t => ColId(t) == a.Id);
        Assert.Single(aTh.QuerySelectorAll("button[aria-label=\"Pin column\"]"));
        var bTh = HeaderCells(cut).First(t => ColId(t) == b.Id);
        Assert.Empty(bTh.QuerySelectorAll("button[aria-label=\"Pin column\"]"));
    }

    [Fact]
    public void Header_Pin_Control_Menu_Pins_The_Column_Right()
    {
        var a = new DataGridColumn<Row> { Field = "A", Title = "Alpha", Pinnable = true };
        var b = new DataGridColumn<Row> { Field = "B", Title = "Bravo" };
        var c = new DataGridColumn<Row> { Field = "C", Title = "Charlie" };
        var cut = RenderGrid(new() { a, b, c });

        // Open A's header pin menu.
        cut.Find("button[aria-label=\"Pin column\"]").Click();
        // The menu (Pin left / Pin right / Unpin) is now in the DOM.
        var items = cut.FindAll("[role=menuitem]");
        Assert.Equal(3, items.Count);

        // Click "Pin to right" (index 1: Left, Right, Unpin).
        items[1].Click();

        var cells = HeaderCells(cut);
        Assert.Equal(a.Id, ColId(cells[^1]));           // A moved to the right edge
        Assert.Contains("right:", Style(cells[^1]));
        Assert.Contains("sticky", cells[^1].ClassName);
    }
}
