using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// Regression tests for the column-chooser REORDER path.
///
/// The preview.6 rewrite turned each chooser row into a <c>role=checkbox</c>
/// button so the whole label region toggles. This locks in that the rewrite did
/// NOT cost the row its reorder affordance: the chooser still renders a
/// SortableList drag handle per row, and reordering (drag / touch / keyboard on
/// the handle) still surfaces a <see cref="ColumnReorderEventArgs"/> to the grid.
/// The keyboard path is the deterministic one bUnit can drive (a real HTML5 drag
/// needs a browser — see the E2E suite); it shares the exact
/// SortableList → HandleSortChanged → OnColumnReorder pipeline the pointer/touch
/// drops use, so it guards the whole reorder wiring.
/// </summary>
public class DataGridColumnVisibilityReorderTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataGridColumnVisibilityReorderTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Row(int Id, string Name);

    private static IReadOnlyList<DataGridColumn<Row>> ThreeColumns() => new[]
    {
        new DataGridColumn<Row> { Field = "A", Title = "Alpha",   Visible = true },
        new DataGridColumn<Row> { Field = "B", Title = "Bravo",   Visible = true },
        new DataGridColumn<Row> { Field = "C", Title = "Charlie", Visible = true },
    };

    private IRenderedComponent<DataGridColumnVisibility<Row>> RenderChooser(
        IReadOnlyList<DataGridColumn<Row>> cols,
        Action<ColumnReorderEventArgs>? onReorder = null)
        => _ctx.Render<DataGridColumnVisibility<Row>>(b =>
        {
            b.OpenComponent<DataGridColumnVisibility<Row>>(0);
            b.AddAttribute(1, "Columns", cols);
            if (onReorder is not null)
                b.AddAttribute(2, "OnColumnReorder",
                    EventCallback.Factory.Create<ColumnReorderEventArgs>(this, onReorder));
            b.CloseComponent();
        });

    [Fact]
    public void Chooser_Renders_A_Reorder_Handle_Per_Row()
    {
        var cut = RenderChooser(ThreeColumns());

        // One SortableList keyboard/drag handle per row (aria-keyshortcuts marks it).
        // If the rewrite had dropped the SortableList (or the handle), this is 0.
        var handles = cut.FindAll("[aria-keyshortcuts]");
        Assert.Equal(3, handles.Count);

        // Each row is ALSO the toggle (so both stories coexist in one popover).
        Assert.Equal(3, cut.FindAll("button[role=checkbox]").Count);
    }

    [Fact]
    public void Keyboard_Reorder_On_A_Handle_Raises_OnColumnReorder()
    {
        var cols = ThreeColumns();
        ColumnReorderEventArgs? captured = null;
        var cut = RenderChooser(cols, a => captured = a);

        // Move the FIRST row down one position via its drag handle (ArrowDown).
        var firstHandle = cut.FindAll("[aria-keyshortcuts]")[0];
        firstHandle.KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });

        // The reorder must surface to the grid. Order became [Bravo, Alpha, Charlie];
        // HandleSortChanged reports the earliest-changed slot — Bravo moved 1 -> 0.
        Assert.NotNull(captured);
        Assert.Equal(cols[1].Id, captured!.ColumnId);
        Assert.Equal(1, captured.OldIndex);
        Assert.Equal(0, captured.NewIndex);
    }
}
