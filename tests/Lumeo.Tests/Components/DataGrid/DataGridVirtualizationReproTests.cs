using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Lumeo;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// Reproduction for the DocFlow field report against 5.0.0, §1.1 (a) and (c). These are NOT
/// regression tests for intended behaviour — they pin the CURRENT behaviour so the defect is
/// demonstrable, and each one names what the correct behaviour would be.
/// </summary>
public class DataGridVirtualizationReproTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataGridVirtualizationReproTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Row(int Id, string Name);

    private static List<DataGridColumn<Row>> Cols() =>
        new() { new DataGridColumn<Row> { Field = "Name", Title = "Name" } };

    private static List<Row> Rows(int n) =>
        Enumerable.Range(0, n).Select(i => new Row(i, $"Row {i}")).ToList();

    /// <summary>
    /// §1.1(a): with ServerMode + Virtualized + an OnRangeRequest handler, the grid should
    /// mount &lt;Virtualize ItemsProvider&gt; and pull the first window. It never does.
    ///
    /// Root cause is a branch ORDER in DataGridBody.razor: the
    /// `DisplayedItems.Count == 0` empty-state branch (line 187) sits in front of the
    /// `ServerVirtualizationProvider is not null` branch (line 207). In server
    /// virtualization the bound Items list is empty BY DESIGN — the provider is what
    /// supplies rows — so the empty-state branch always wins, Virtualize never mounts,
    /// and the provider is never invoked.
    /// </summary>
    [Fact]
    public async Task OnRangeRequest_Is_Never_Invoked_In_Server_Virtualization_Mode()
    {
        var calls = 0;

        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, new List<Row>())
            .Add(g => g.Columns, Cols())
            .Add(g => g.ServerMode, true)
            .Add(g => g.Virtualized, true)
            .Add(g => g.VirtualizeThreshold, 1)
            .Add(g => g.ShowPagination, false)
            .Add(g => g.TotalCount, 250)
            .Add(g => g.Height, "600px")
            .Add(g => g.OnRangeRequest, new Func<DataGridRangeRequest, ValueTask<DataGridRangeResponse<Row>>>(req =>
            {
                Interlocked.Increment(ref calls);
                return ValueTask.FromResult(new DataGridRangeResponse<Row>(
                    Rows(req.Count).ToList(), 250));
            })));

        await Task.Delay(150);

        // What the report observed, and what this pins: zero.
        Assert.Equal(0, calls);

        // And the reason it is zero: the grid rendered its EMPTY state instead of a
        // virtualized body, even though a provider and a TotalCount of 250 were supplied.
        // The empty state is a single full-width row; a virtualized body would have
        // spacer rows and a window of data rows instead.
        var body = cut.FindAll("tbody[data-slot='datagrid-body'] tr");
        Assert.Single(body);
        Assert.NotNull(body[0].QuerySelector("td[colspan]"));
    }

    /// <summary>
    /// §1.1(c): PageSize is forwarded to DataGridBody as SkeletonRowCount
    /// (DataGrid.razor:341), and the loading branch renders exactly that many &lt;tr&gt;
    /// synchronously — before any data exists and regardless of how many items are bound.
    /// A host that switches pagination off and sets a generous PageSize ("I supply the
    /// rows") pays for it with a main-thread freeze.
    /// </summary>
    [Theory]
    [InlineData(200)]
    [InlineData(3000)]
    public void Loading_Skeleton_Materialises_PageSize_Rows_Regardless_Of_Item_Count(int pageSize)
    {
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Rows(200))
            .Add(g => g.Columns, Cols())
            .Add(g => g.IsLoading, true)
            .Add(g => g.ShowPagination, false)
            .Add(g => g.PageSize, pageSize));

        var rows = cut.FindAll("tbody[data-slot='datagrid-body'] tr").Count;

        // 200 items are bound; the skeleton count tracks PageSize, not the data.
        Assert.Equal(pageSize, rows);

        // The multi-second freeze the report measured is a browser/WASM main-thread effect;
        // bUnit cannot measure it honestly, so it is not asserted here. The DOM count is the
        // part that IS measurable, and it is the mechanism behind the freeze.
    }
}
