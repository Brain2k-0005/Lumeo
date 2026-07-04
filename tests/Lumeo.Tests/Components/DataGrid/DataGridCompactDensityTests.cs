using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// Regression tests for the previously-dead <see cref="DataGrid{TItem}.Compact"/>
/// parameter. It used to only append <c>text-sm</c> at the root while
/// <see cref="DataGridCell{TItem}"/> / <see cref="DataGridHeaderCell{TItem}"/>
/// hard-coded <c>px-3 py-2</c>, so density never actually changed. Compact now
/// flows through <see cref="DataGridContext{TItem}"/> and tightens header + body
/// cell padding to <c>px-2 py-1</c>, and reacts to a post-first-render toggle.
/// </summary>
public class DataGridCompactDensityTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataGridCompactDensityTests() => _ctx.AddLumeoServices();
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

    private static string CellClass(IRenderedComponent<DataGrid<Row>> cut) =>
        cut.FindAll("td[data-slot='datagrid-cell']")[0].GetAttribute("class") ?? "";

    private static string HeaderClass(IRenderedComponent<DataGrid<Row>> cut) =>
        cut.FindAll("th[data-slot='datagrid-header-cell']")[0].GetAttribute("class") ?? "";

    [Fact]
    public void Compact_Tightens_Body_Cell_Padding()
    {
        var cut = RenderGrid(compact: true);
        var css = CellClass(cut);
        Assert.Contains("px-2 py-1", css);
        Assert.DoesNotContain("px-3 py-2", css);
    }

    [Fact]
    public void Compact_Tightens_Header_Cell_Padding()
    {
        var cut = RenderGrid(compact: true);
        Assert.Contains("px-2 py-1", HeaderClass(cut));
    }

    [Fact]
    public void Default_Density_Keeps_Standard_Padding()
    {
        var cut = RenderGrid(compact: false);
        Assert.Contains("px-3 py-2", CellClass(cut));
        Assert.DoesNotContain("px-2 py-1", CellClass(cut));
        Assert.Contains("px-3 py-2", HeaderClass(cut));
    }

    [Fact]
    public void Toggling_Compact_After_First_Render_Repads_Cells()
    {
        // Starts standard, then flips to Compact — the DataGridCell css memo must
        // invalidate (Compact is part of its cache key) rather than latch to the
        // first-render density.
        // Same Columns list instance across both renders so only Compact changes.
        var cols = Cols();
        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(g => g.Items, Data)
            .Add(g => g.Compact, false)
            .Add(g => g.Columns, cols));
        Assert.Contains("px-3 py-2", CellClass(cut));

        cut.Render(p => p
            .Add(g => g.Items, Data)
            .Add(g => g.Compact, true)
            .Add(g => g.Columns, cols));

        Assert.Contains("px-2 py-1", CellClass(cut));
        Assert.DoesNotContain("px-3 py-2", CellClass(cut));
    }
}
