using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Lumeo;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// Regression tests for the two DataGrid virtualization defects found by the DocFlow field
/// report against 5.0.0 (#431, #432).
///
/// These started life as characterization tests pinning the DEFECTIVE behaviour, so the
/// defects were demonstrable in CI while they were still open. They are inverted now that
/// both are fixed — the doc comments below say what the defect was, because the shape of
/// each fix is only obvious once you know what it is defending against.
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

    private IRenderedComponent<Lumeo.DataGrid<Row>> RenderServerVirtualized(
        Func<DataGridRangeRequest, ValueTask<DataGridRangeResponse<Row>>> onRange) =>
        _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, new List<Row>())
            .Add(g => g.Columns, Cols())
            .Add(g => g.ServerMode, true)
            .Add(g => g.Virtualized, true)
            .Add(g => g.VirtualizeThreshold, 1)
            .Add(g => g.ShowPagination, false)
            .Add(g => g.TotalCount, 250)
            .Add(g => g.Height, "600px")
            .Add(g => g.OnRangeRequest, onRange));

    /// <summary>
    /// #431. The bound Items list is empty by design in server virtualization — the provider
    /// is what supplies the rows — so the `DisplayedItems.Count == 0` empty-state branch in
    /// DataGridBody used to match on every mount, in front of the provider branch.
    /// &lt;Virtualize&gt; never mounted, its ItemsProvider never ran, and OnRangeRequest — whose
    /// only caller is that provider — never fired.
    /// </summary>
    [Fact]
    public async Task OnRangeRequest_Is_Invoked_In_Server_Virtualization_Mode()
    {
        var calls = 0;

        RenderServerVirtualized(req =>
        {
            Interlocked.Increment(ref calls);
            return ValueTask.FromResult(new DataGridRangeResponse<Row>(Rows(req.Count).ToList(), 250));
        });

        await Task.Delay(200);

        Assert.True(calls > 0,
            "OnRangeRequest was never called: the grid is not mounting its virtualized body.");
    }

    /// <summary>
    /// The other half of #431's fix. Guarding the empty-state branch must not cost the
    /// "nothing to show" message when the SERVER is the one reporting nothing — that is the
    /// only party that can know it here.
    ///
    /// The message renders beside &lt;Virtualize&gt;, never instead of it: replacing it would
    /// stop the provider running, and a later filter change that does have rows could then
    /// never bring the grid back.
    /// </summary>
    [Fact]
    public async Task A_Server_Reporting_Zero_Rows_Still_Shows_The_Empty_State()
    {
        var cut = RenderServerVirtualized(_ =>
            ValueTask.FromResult(new DataGridRangeResponse<Row>(new List<Row>(), 0)));

        await Task.Delay(200);
        cut.Render();

        // The empty row is a single full-width cell inside the body, rendered alongside
        // Virtualize's spacers. Matching the cell rather than the localized string keeps
        // this independent of which locale the test host resolves.
        var body = cut.Find("tbody[data-slot='datagrid-body']");
        Assert.NotNull(body.QuerySelector("td[colspan]"));
    }

    /// <summary>
    /// #432. PageSize reached DataGridBody as SkeletonRowCount and the loading branch rendered
    /// exactly that many rows, synchronously, before any data existed and regardless of how
    /// many items were bound. PageSize is documented as a paging window, so the natural
    /// configuration for "pagination is off, I supply the rows" — a generous PageSize — froze
    /// the main thread for seconds with no spinner and no error.
    /// </summary>
    [Theory]
    [InlineData(10)]
    [InlineData(200)]
    [InlineData(3000)]
    [InlineData(10000)]
    public void Loading_Skeleton_Is_Capped_Regardless_Of_PageSize(int pageSize)
    {
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Rows(200))
            .Add(g => g.Columns, Cols())
            .Add(g => g.IsLoading, true)
            .Add(g => g.ShowPagination, false)
            .Add(g => g.PageSize, pageSize));

        var rows = cut.FindAll("tbody[data-slot='datagrid-body'] tr").Count;

        // Small values still render exactly what was asked for — the cap is a ceiling, not a
        // replacement, so a deliberately short skeleton is unaffected.
        Assert.Equal(Math.Min(pageSize, 40), rows);
    }
}
