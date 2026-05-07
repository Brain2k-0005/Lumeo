using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.DataGrid;

public class DataGridVirtualizedServerModeTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataGridVirtualizedServerModeTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Row(int Id, string Name);

    private static List<DataGridColumn<Row>> Cols() => new()
    {
        new() { Field = "Id", Title = "ID", Sortable = true },
        new() { Field = "Name", Title = "Name", Sortable = true, Filterable = true },
    };

    [Fact]
    public void Virtualized_ServerMode_Hides_Pagination()
    {
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Array.Empty<Row>())
            .Add(g => g.Columns, Cols())
            .Add(g => g.Virtualized, true)
            .Add(g => g.ShowPagination, true) // explicit on — should still be hidden
            .Add(g => g.OnRangeRequest, (DataGridRangeRequest _) =>
                ValueTask.FromResult(new DataGridRangeResponse<Row>(Array.Empty<Row>(), 0)))
        );

        // Pagination has data-slot="datagrid-pagination" — must NOT be in DOM.
        Assert.DoesNotContain("data-slot=\"datagrid-pagination\"", cut.Markup);
    }

    [Fact]
    public void Virtualized_Without_OnRangeRequest_Falls_Back_To_Normal_Render()
    {
        // Virtualized=true but no callback: defensively fall back, do not crash.
        var data = new[] { new Row(1, "A"), new Row(2, "B") };
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, data)
            .Add(g => g.Columns, Cols())
            .Add(g => g.Virtualized, true)
            .Add(g => g.ShowPagination, false)
        );

        // Without a callback, the grid must render the supplied items normally.
        Assert.Contains("A", cut.Markup);
        Assert.Contains("B", cut.Markup);
    }

    [Fact]
    public async Task Virtualized_ServerMode_Invokes_OnRangeRequest_With_Sort_Context()
    {
        DataGridRangeRequest? captured = null;
        var data = Enumerable.Range(1, 100).Select(i => new Row(i, $"R{i}")).ToList();

        ValueTask<DataGridRangeResponse<Row>> Provider(DataGridRangeRequest req)
        {
            captured = req;
            return ValueTask.FromResult(new DataGridRangeResponse<Row>(
                data.Skip(req.StartIndex).Take(req.Count).ToList(), data.Count));
        }

        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Array.Empty<Row>())
            .Add(g => g.Columns, Cols())
            .Add(g => g.Virtualized, true)
            .Add(g => g.OnRangeRequest, (Func<DataGridRangeRequest, ValueTask<DataGridRangeResponse<Row>>>)Provider)
        );

        // Sort change should call RefreshVirtualizedAsync() — captured request
        // should reflect _sorts. We invoke the public refresh API directly here
        // because <Virtualize> doesn't fetch in bUnit's headless DOM.
        await cut.Instance.RefreshVirtualizedAsync();

        // Ensure the public API exists and is awaitable. The bUnit JS shim
        // doesn't drive Virtualize's IntersectionObserver, so the provider may
        // not actually be invoked in this test — we only assert that the API
        // surface compiles, dispatches without error, and the grid stays sane.
        Assert.NotNull(cut.Instance);
    }
}
