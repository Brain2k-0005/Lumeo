using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Lumeo;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// End-to-end bUnit coverage for the <see cref="DataGridColumnSizing.FitWithMinimum"/> fix
/// (DocFlow field report against 5.11.0): DataGridHeaderCell.StyleString used to emit ONLY
/// `min-width` under `table-layout: auto`, which (1) never actually capped a nowrap cell's
/// growth — a 12-column, 1310px-container search grid rendered at 1512/2035px — and (2) never
/// reflected a resized or LayoutStorageKey-restored width in any CSS property at all, so a
/// drag to 90px "jumped back" on reload.
///
/// The fix: DataGrid measures its horizontal scroll wrapper (a ResizeObserver reporting back
/// to .NET via <c>OnFitContainerWidthChanged</c> — components.js's registerColumnFitObserver)
/// and computes an explicit pixel width per column (<see cref="DataGridColumnFit.Compute"/>),
/// then renders with `table-layout: fixed` at those widths. bUnit never runs real JS, so
/// these tests drive the same JSInvokable callback the ResizeObserver would
/// (<c>cut.Instance.OnFitContainerWidthChanged(width)</c> — same pattern as
/// AgentMessageListScrollTests' OnScrollAwayChanged) rather than mocking the interop call.
/// See DataGridColumnFitTests for the algorithm's own pure unit coverage, and
/// DataGridDocFlowReproTests.D3 for the PRE-measurement fallback this replaced.
/// </summary>
public class DataGridFitWithMinimumWidthsTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataGridFitWithMinimumWidthsTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Row(int Id, string Name, string City);

    // Explicit Id (rather than the default random Guid) so tests can address a column
    // directly through DataGrid's public width APIs, which key off Id, not Field.
    private static List<DataGridColumn<Row>> TwoColumns() => new()
    {
        new() { Id = "name", Field = "Name", Title = "Name", Width = 150, MinWidth = 100 },
        new() { Id = "city", Field = "City", Title = "City", Width = 100, MinWidth = 80 },
    };

    private IRenderedComponent<Lumeo.DataGrid<Row>> RenderFitGrid(List<DataGridColumn<Row>>? columns = null) =>
        _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, new List<Row> { new(1, "Alice", "Berlin") })
            .Add(g => g.ColumnSizing, DataGridColumnSizing.FitWithMinimum)
            .Add(g => g.Columns, columns ?? TwoColumns()));

    private static string HeaderStyle(IRenderedComponent<Lumeo.DataGrid<Row>> cut, int index) =>
        cut.FindAll("th[data-slot=\"datagrid-header-cell\"]")[index].GetAttribute("style") ?? "";

    private static double ExtractPx(string style, string property)
    {
        var marker = property + ": ";
        var start = style.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"'{property}' not found in style '{style}'");
        start += marker.Length;
        var end = style.IndexOf("px", start, StringComparison.Ordinal);
        return double.Parse(style[start..end], System.Globalization.CultureInfo.InvariantCulture);
    }

    // --- Fit case: room to spare, table fills the container exactly. ---

    [Fact]
    public async Task Fit_Case_Table_Fills_Container_Exactly_With_Fixed_Layout()
    {
        var cut = RenderFitGrid();
        await cut.InvokeAsync(() => cut.Instance.OnFitContainerWidthChanged(400));

        var tableStyle = cut.Find("table").GetAttribute("style") ?? "";
        Assert.Contains("table-layout: fixed", tableStyle);
        Assert.Contains("width: 400px", tableStyle);

        var nameWidth = ExtractPx(HeaderStyle(cut, 0), "width");
        var cityWidth = ExtractPx(HeaderStyle(cut, 1), "width");
        Assert.Equal(400, nameWidth + cityWidth, 3);
    }

    [Fact]
    public async Task Twelve_Column_Grid_Fills_1310px_Container_Exactly()
    {
        var columns = Enumerable.Range(0, 12)
            .Select(i => new DataGridColumn<Row> { Field = $"F{i}", Title = $"F{i}", Width = 100, MinWidth = 80 })
            .ToList();
        var cut = RenderFitGrid(columns);
        await cut.InvokeAsync(() => cut.Instance.OnFitContainerWidthChanged(1310));

        var tableStyle = cut.Find("table").GetAttribute("style") ?? "";
        Assert.Contains("width: 1310px", tableStyle);

        var sum = cut.FindAll("th[data-slot=\"datagrid-header-cell\"]")
            .Select(h => ExtractPx(h.GetAttribute("style") ?? "", "width"))
            .Sum();
        Assert.Equal(1310, sum, 3);
    }

    // --- Overflow case: sum(MinWidth) > container — every column pinned at its floor,
    // table grows past the container (existing overflow-x-auto scrolls). ---

    [Fact]
    public async Task Overflow_Case_Every_Column_At_MinWidth_Table_Wider_Than_Container()
    {
        var cut = RenderFitGrid();
        // sum(MinWidth) = 100 + 80 = 180 > 150.
        await cut.InvokeAsync(() => cut.Instance.OnFitContainerWidthChanged(150));

        var nameWidth = ExtractPx(HeaderStyle(cut, 0), "width");
        var cityWidth = ExtractPx(HeaderStyle(cut, 1), "width");
        Assert.Equal(100, nameWidth, 3);
        Assert.Equal(80, cityWidth, 3);

        var tableStyle = cut.Find("table").GetAttribute("style") ?? "";
        Assert.Contains("width: 180px", tableStyle); // sum(MinWidth), exceeds the 150px container
    }

    // --- A user-resized width must win over the declared MinWidth once measured
    // (DocFlow finding 2's "jumps back on reload" — the resize is no longer dropped). ---

    [Fact]
    public async Task Resized_Width_Wins_Over_Declared_Width_Once_Measured()
    {
        var cut = RenderFitGrid();

        // Drag City to 90px (>= its MinWidth of 80) — same commit path a real drag/keyboard
        // resize uses (DataGrid.UpdateColumnWidth -> ApplyColumnWidth).
        await cut.InvokeAsync(() => cut.Instance.UpdateColumnWidth("city", 90));
        // Container == the exact new sum of desired widths (Name 150 + resized City 90):
        // an exact fit, so there's no surplus to redistribute — isolates "the resize is
        // reflected at all" from the (separately tested) proportional-growth behaviour.
        await cut.InvokeAsync(() => cut.Instance.OnFitContainerWidthChanged(240));

        var cityWidth = ExtractPx(HeaderStyle(cut, 1), "width");
        Assert.Equal(90, cityWidth, 3); // not squeezed back to content/declared width
    }

    [Fact]
    public async Task Resize_Below_MinWidth_Clamps_To_MinWidth()
    {
        var cut = RenderFitGrid();

        await cut.InvokeAsync(() => cut.Instance.UpdateColumnWidth("city", 10)); // below MinWidth=80
        // ApplyColumnWidth clamps the commit itself to MinWidth=80 — exact-fit container
        // (Name 150 + clamped City 80) so there's no room to grow it back up.
        await cut.InvokeAsync(() => cut.Instance.OnFitContainerWidthChanged(230));

        var cityWidth = ExtractPx(HeaderStyle(cut, 1), "width");
        Assert.Equal(80, cityWidth, 3);
    }

    [Fact]
    public async Task Resize_Above_MaxWidth_Clamps_To_MaxWidth()
    {
        var columns = new List<DataGridColumn<Row>>
        {
            new() { Id = "name", Field = "Name", Title = "Name", Width = 150, MinWidth = 100, MaxWidth = 220 },
            new() { Id = "city", Field = "City", Title = "City", Width = 100, MinWidth = 80 },
        };
        var cut = RenderFitGrid(columns);

        await cut.InvokeAsync(() => cut.Instance.UpdateColumnWidth("name", 500)); // way above MaxWidth=220
        // Exact-fit (clamped Name 220 + City 100) so Name has no room to grow further.
        await cut.InvokeAsync(() => cut.Instance.OnFitContainerWidthChanged(320));

        var nameWidth = ExtractPx(HeaderStyle(cut, 0), "width");
        Assert.Equal(220, nameWidth, 3);
    }

    // --- A LayoutStorageKey-restored width must win the same way a live resize does. ---

    [Fact]
    public async Task Layout_Restored_Width_Wins_Once_Measured()
    {
        var cut = RenderFitGrid();
        var layout = cut.Instance.GetCurrentLayout();
        var cityLayout = layout.Columns.First(c => c.Field == "City");
        cityLayout.Width = 95;

        await cut.InvokeAsync(() => cut.Instance.ApplyLayoutAsync(layout));
        // Exact-fit (Name 150 + restored City 95).
        await cut.InvokeAsync(() => cut.Instance.OnFitContainerWidthChanged(245));

        var cityWidth = ExtractPx(HeaderStyle(cut, 1), "width");
        Assert.Equal(95, cityWidth, 3);
    }

    // --- ResetColumnWidthsAsync must restore the DECLARED widths, not just clear the
    // measured state — the negotiated layout still applies on top afterwards. ---

    [Fact]
    public async Task ResetColumnWidthsAsync_Restores_Declared_Width_Distribution()
    {
        var cut = RenderFitGrid();

        await cut.InvokeAsync(() => cut.Instance.UpdateColumnWidth("city", 95));
        await cut.InvokeAsync(() => cut.Instance.OnFitContainerWidthChanged(245)); // exact fit (150 + 95)
        Assert.Equal(95, ExtractPx(HeaderStyle(cut, 1), "width"), 3);

        await cut.InvokeAsync(() => cut.Instance.ResetColumnWidthsAsync());

        // Back to the declared City Width (100) as the desired input to the distribution.
        // The container is still 245 (< the now-restored 250px sum of declared widths), so
        // the shrink pass removes a small proportional deficit from both columns — City no
        // longer sits at the resized 95.
        Assert.NotEqual(95, ExtractPx(HeaderStyle(cut, 1), "width"), 3);
    }

    // --- Body cells truncate instead of forcing the column wider. ---

    [Fact]
    public async Task Body_Cells_Get_Truncate_Class_In_FitWithMinimum_Mode()
    {
        var cut = RenderFitGrid();
        await cut.InvokeAsync(() => cut.Instance.OnFitContainerWidthChanged(400));

        var cell = cut.Find("tbody td[data-slot='datagrid-cell']");
        Assert.Contains("truncate", cell.GetAttribute("class"));
    }

    [Fact]
    public void Body_Cells_Do_Not_Get_Truncate_Class_In_Auto_Mode()
    {
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, new List<Row> { new(1, "Alice", "Berlin") })
            .Add(g => g.Columns, TwoColumns()));

        var cell = cut.Find("tbody td[data-slot='datagrid-cell']");
        Assert.DoesNotContain("truncate", cell.GetAttribute("class"));
    }
}
