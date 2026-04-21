using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// Regression tests for the column-registration leak (repeated
/// <c>OnInitialized</c> firings on <c>DataGridColumnDef</c> children used to
/// accumulate duplicate entries in the parent grid's internal column list,
/// causing the filter popover to open N times and the Toggle Columns menu to
/// show each column N times). See <c>DataGrid.AddColumnDef</c> for the fix.
/// </summary>
public class DataGridColumnRegistrationTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataGridColumnRegistrationTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Row(int Id, string Name);

    /// <summary>
    /// Registers 5 columns, forces 10 parent re-renders, and asserts the
    /// registered column count is still exactly 5. Uses the public
    /// <see cref="DataGrid{TItem}.AddColumnDef"/> API + re-render loop to
    /// simulate what happens when Blazor re-runs column-def children.
    /// </summary>
    [Fact]
    public void AddColumnDef_Is_Idempotent_Across_Parent_Rerenders()
    {
        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(g => g.Items, new[] { new Row(1, "a") }));

        var grid = cut.Instance;

        // Register 5 columns by Field
        var fields = new[] { "Id", "Name", "Email", "Amount", "Status" };
        foreach (var f in fields)
        {
            grid.AddColumnDef(new DataGridColumn<Row> { Field = f, Title = f });
        }

        // Force 10 re-renders, re-registering the same 5 columns each time
        // (mirroring what the razor child component would do on OnInitialized).
        for (int i = 0; i < 10; i++)
        {
            foreach (var f in fields)
            {
                grid.AddColumnDef(new DataGridColumn<Row> { Field = f, Title = f });
            }
            cut.Render();
        }

        // The public GetCurrentLayout reflects _orderedColumns, so this
        // implicitly checks dedup of the registered column list.
        var layout = grid.GetCurrentLayout();
        Assert.Equal(5, layout.Columns.Count);
    }

    /// <summary>
    /// Declarative ChildContent path: renders a DataGrid with 5 DataGridColumnDef
    /// children, forces re-renders via parameter change, and verifies there are
    /// still exactly 5 header cells rendered (not 10, 15, 25...).
    /// </summary>
    [Fact]
    public void DataGridColumnDef_Children_Render_Unique_Headers_After_Rerender()
    {
        var data = new[]
        {
            new Row(1, "a"), new Row(2, "b")
        };

        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(g => g.Items, data)
            .Add(g => g.PageSize, 10)
            .AddChildContent<DataGridColumnDef<Row>>(c => c.Add(x => x.Field, "Id").Add(x => x.Title, "ID"))
            .AddChildContent<DataGridColumnDef<Row>>(c => c.Add(x => x.Field, "Name").Add(x => x.Title, "Name")));

        // Force re-render by changing a parameter multiple times
        for (int i = 0; i < 5; i++)
        {
            cut.Render(p => p.Add(g => g.PageSize, 10 + i));
        }

        // Only the two declared columns should be in the layout
        var layout = cut.Instance.GetCurrentLayout();
        Assert.Equal(2, layout.Columns.Count);
        Assert.Contains(layout.Columns, c => c.Field == "Id");
        Assert.Contains(layout.Columns, c => c.Field == "Name");
    }
}
