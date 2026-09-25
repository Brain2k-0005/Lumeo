using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// DocFlow T2 density follow-up (#523): <c>--lumeo-grid-header-h</c> and
/// <c>--lumeo-grid-row-h</c> shipped in 5.11.1 but no component read them. This wires a
/// `min-height` floor onto the header row and each data row (see
/// <see cref="Lumeo.DataGridHeader{TItem}.HeaderRowMinHeightClass"/> and
/// <see cref="Lumeo.DataGridRow{TItem}.MinHeightClass"/>) at Comfortable/default density,
/// and leaves Compact alone (it has its own tighter, hard-coded padding-driven height with
/// no token of its own — forcing the Comfortable floor onto it would be a real regression).
/// </summary>
public class DataGridRowHeightTokenTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataGridRowHeightTokenTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Row(int Id, string Name);

    private static readonly Row[] Data = { new(1, "Alice"), new(2, "Bob") };

    private static List<DataGridColumn<Row>> Cols() => new()
    {
        new() { Field = "Id", Title = "ID" },
        new() { Field = "Name", Title = "Name" },
    };

    private IRenderedComponent<DataGrid<Row>> RenderGrid(bool compact) =>
        _ctx.Render<DataGrid<Row>>(p => p
            .Add(g => g.Items, Data)
            .Add(g => g.Compact, compact)
            .Add(g => g.Columns, Cols()));

    private const string HeaderMinHClass = "min-h-[var(--lumeo-grid-header-h,calc(var(--spacing,0.25rem)*8))]";
    private const string RowMinHClass = "min-h-[var(--lumeo-grid-row-h,calc(var(--spacing,0.25rem)*9))]";

    [Fact]
    public void Default_Density_Header_Row_Carries_The_Height_Token()
    {
        var cut = RenderGrid(compact: false);
        var headerRow = cut.Find("thead tr[role='row']");
        Assert.Contains(HeaderMinHClass, headerRow.GetAttribute("class"));
    }

    [Fact]
    public void Default_Density_Data_Row_Carries_The_Height_Token()
    {
        var cut = RenderGrid(compact: false);
        var row = cut.FindAll("tr[data-slot='datagrid-row']")[0];
        Assert.Contains(RowMinHClass, row.GetAttribute("class"));
    }

    [Fact]
    public void Compact_Does_Not_Carry_The_Height_Token()
    {
        // Compact has its own tighter, hard-coded padding-driven height (px-2 py-1) with no
        // token of its own — the Comfortable-density floor must not be applied on top of it.
        var cut = RenderGrid(compact: true);
        var headerRow = cut.Find("thead tr[role='row']");
        var row = cut.FindAll("tr[data-slot='datagrid-row']")[0];
        Assert.DoesNotContain(HeaderMinHClass, headerRow.GetAttribute("class"));
        Assert.DoesNotContain(RowMinHClass, row.GetAttribute("class"));
    }

    [Fact]
    public void Toggling_Compact_After_First_Render_Updates_The_Height_Class()
    {
        var cols = Cols();
        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(g => g.Items, Data)
            .Add(g => g.Compact, false)
            .Add(g => g.Columns, cols));
        Assert.Contains(RowMinHClass, cut.FindAll("tr[data-slot='datagrid-row']")[0].GetAttribute("class"));

        cut.Render(p => p
            .Add(g => g.Items, Data)
            .Add(g => g.Compact, true)
            .Add(g => g.Columns, cols));

        Assert.DoesNotContain(RowMinHClass, cut.FindAll("tr[data-slot='datagrid-row']")[0].GetAttribute("class"));
    }
}
