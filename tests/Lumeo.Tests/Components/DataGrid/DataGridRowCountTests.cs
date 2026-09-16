using System.Reflection;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// DataGrid had <c>HasActiveFilters</c>/<c>ClearFiltersAsync</c> (5.10.0) but no public count of
/// the rows that survive filters/search — the Northlight demo's Customers view renders
/// "n of m accounts" from its own chip filter and cannot follow the grid's built-in search
/// because nothing on the grid exposes that count. These tests cover
/// <c>FilteredRowCount</c>/<c>TotalRowCount</c> and <c>OnRowCountChanged</c>.
///
/// Verified NOT already present against current source before writing these: <c>DataGrid&lt;TItem&gt;</c>
/// (src/Lumeo.DataGrid/UI/DataGrid/DataGrid.razor) had no <c>FilteredRowCount</c>, <c>TotalRowCount</c>
/// or <c>OnRowCountChanged</c> members; the pagination summary and <c>DataGridFooterContext</c>
/// computed their own count off a private <c>EffectiveTotalCount</c> with no public equivalent.
/// </summary>
public class DataGridRowCountTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataGridRowCountTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Row(int Id, string Name);

    private static List<Row> Sample(int count) =>
        Enumerable.Range(1, count).Select(i => new Row(i, i <= 3 ? $"Match{i}" : $"Other{i}")).ToList();

    private static List<DataGridColumn<Row>> Columns() => new()
    {
        new() { Field = "Name", Title = "Name", Filterable = true },
    };

    // Reflection: HandleFilter/HandleGlobalSearch are the same private entry points the
    // column-filter popover and toolbar search box invoke; used here to drive them
    // deterministically without the popover/input UI, mirroring the reflection pattern in
    // DataGridFilteredEmptyStateTests / DataGridBatchEditTests.
    private static Task InvokePrivateAsync(DataGrid<Row> grid, string methodName, params object[] args) =>
        (Task)typeof(DataGrid<Row>)
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(grid, args)!;

    private static Task ApplyFilterAsync(DataGrid<Row> grid, FilterDescriptor filter) =>
        InvokePrivateAsync(grid, "HandleFilter", filter);

    private static Task ApplyGlobalSearchAsync(DataGrid<Row> grid, string search) =>
        InvokePrivateAsync(grid, "HandleGlobalSearch", search);

    // Reflection: reads the private _toolbarContext field so the toolbar-context half of the
    // requirement (custom ToolbarContent tools cascaded DataGridToolbarContext<TItem>) is
    // verified without standing up a full custom toolbar-tool component tree.
    private static DataGridToolbarContext<Row> ToolbarContext(DataGrid<Row> grid) =>
        (DataGridToolbarContext<Row>)typeof(DataGrid<Row>)
            .GetField("_toolbarContext", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(grid)!;

    [Fact]
    public async Task ClientMode_FilterToSubset_UpdatesCounts_And_RaisesEventOnce()
    {
        var events = new List<DataGridRowCountChanged>();
        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(x => x.Items, Sample(12))
            .Add(x => x.Columns, Columns())
            .Add(x => x.OnRowCountChanged, EventCallback.Factory.Create<DataGridRowCountChanged>(
                this, e => events.Add(e))));

        Assert.Equal(12, cut.Instance.FilteredRowCount);
        Assert.Equal(12, cut.Instance.TotalRowCount);
        events.Clear(); // ignore whatever fired on mount; assert only the filter action below

        await cut.InvokeAsync(() => ApplyFilterAsync(cut.Instance,
            new FilterDescriptor("Name", FilterOperator.Contains, "Match")));
        cut.Render();

        Assert.Equal(3, cut.Instance.FilteredRowCount);
        Assert.Equal(12, cut.Instance.TotalRowCount);
        var e = Assert.Single(events);
        Assert.Equal(3, e.Filtered);
        Assert.Equal(12, e.Total);

        // Toolbar-context mirror (custom ToolbarContent tools read this).
        Assert.Equal(3, ToolbarContext(cut.Instance).FilteredRowCount);
        Assert.Equal(12, ToolbarContext(cut.Instance).TotalRowCount);
    }

    [Fact]
    public async Task ClientMode_ClearFilters_RestoresCounts_And_RaisesEvent()
    {
        var events = new List<DataGridRowCountChanged>();
        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(x => x.Items, Sample(12))
            .Add(x => x.Columns, Columns())
            .Add(x => x.OnRowCountChanged, EventCallback.Factory.Create<DataGridRowCountChanged>(
                this, e => events.Add(e))));

        await cut.InvokeAsync(() => ApplyFilterAsync(cut.Instance,
            new FilterDescriptor("Name", FilterOperator.Contains, "Match")));
        cut.Render();
        Assert.Equal(3, cut.Instance.FilteredRowCount);
        events.Clear();

        await cut.InvokeAsync(() => cut.Instance.ClearFiltersAsync());
        cut.Render();

        Assert.Equal(12, cut.Instance.FilteredRowCount);
        Assert.Equal(12, cut.Instance.TotalRowCount);
        var e = Assert.Single(events);
        Assert.Equal(12, e.Filtered);
        Assert.Equal(12, e.Total);
    }

    [Fact]
    public async Task ClientMode_GlobalSearch_UpdatesFilteredRowCount_TotalRowCountUnchanged()
    {
        var events = new List<DataGridRowCountChanged>();
        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(x => x.Items, Sample(12))
            .Add(x => x.Columns, Columns())
            .Add(x => x.OnRowCountChanged, EventCallback.Factory.Create<DataGridRowCountChanged>(
                this, e => events.Add(e))));
        events.Clear();

        await cut.InvokeAsync(() => ApplyGlobalSearchAsync(cut.Instance, "Match"));
        cut.Render();

        Assert.Equal(3, cut.Instance.FilteredRowCount);
        Assert.Equal(12, cut.Instance.TotalRowCount);
        var e = Assert.Single(events);
        Assert.Equal(3, e.Filtered);
        Assert.Equal(12, e.Total);
    }

    [Fact]
    public async Task ClientMode_RerenderWithoutCountChange_DoesNotRaiseEventAgain()
    {
        var events = new List<DataGridRowCountChanged>();
        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(x => x.Items, Sample(12))
            .Add(x => x.Columns, Columns())
            .Add(x => x.OnRowCountChanged, EventCallback.Factory.Create<DataGridRowCountChanged>(
                this, e => events.Add(e))));
        events.Clear();

        // Re-apply the exact same filter twice: the second call recomputes the same 3-of-12
        // and must not raise a second event.
        await cut.InvokeAsync(() => ApplyFilterAsync(cut.Instance,
            new FilterDescriptor("Name", FilterOperator.Contains, "Match")));
        cut.Render();
        Assert.Single(events);

        await cut.InvokeAsync(() => ApplyFilterAsync(cut.Instance,
            new FilterDescriptor("Name", FilterOperator.Contains, "Match")));
        cut.Render();

        Assert.Single(events);
    }

    [Fact]
    public async Task EmptyTemplate_Receives_FilteredRowCount_And_TotalRowCount()
    {
        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(x => x.Items, Sample(12))
            .Add(x => x.Columns, Columns())
            .Add(x => x.EmptyTemplate, (DataGridEmptyContext ctx) => builder =>
            {
                builder.OpenElement(0, "div");
                builder.AddAttribute(1, "data-testid", "custom-empty");
                builder.AddContent(2, $"{ctx.FilteredRowCount} of {ctx.TotalRowCount}");
                builder.CloseElement();
            }));

        await cut.InvokeAsync(() => ApplyFilterAsync(cut.Instance,
            new FilterDescriptor("Name", FilterOperator.Equals, "NoSuchName")));
        cut.Render();

        var custom = cut.Find("[data-testid='custom-empty']");
        Assert.Equal("0 of 12", custom.TextContent);
    }

    [Fact]
    public async Task ServerMode_TotalCountParameterChange_UpdatesCounts_And_RaisesEvent()
    {
        var events = new List<DataGridRowCountChanged>();
        var handler = EventCallback.Factory.Create<DataGridServerRequest>(this, _ => Task.CompletedTask);

        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(x => x.Items, Sample(12))
            .Add(x => x.Columns, Columns())
            .Add(x => x.ServerMode, true)
            .Add(x => x.TotalCount, 12)
            .Add(x => x.OnServerRequest, handler)
            .Add(x => x.OnRowCountChanged, EventCallback.Factory.Create<DataGridRowCountChanged>(
                this, e => events.Add(e))));

        await Task.Delay(150); // let the initial server request settle
        Assert.Equal(12, cut.Instance.FilteredRowCount);
        Assert.Equal(12, cut.Instance.TotalRowCount);
        events.Clear();

        // Simulate the consumer's OnServerRequest handler having fetched a filtered,
        // server-side page: a smaller Items batch plus the matching new TotalCount, exactly
        // as a real handler assigns both bound fields before Blazor's automatic re-render
        // hands the fresh parameters back to the grid.
        cut.Render(p => p
            .Add(x => x.Items, Sample(12).Take(3).ToList())
            .Add(x => x.Columns, Columns())
            .Add(x => x.ServerMode, true)
            .Add(x => x.TotalCount, 3)
            .Add(x => x.OnServerRequest, handler)
            .Add(x => x.OnRowCountChanged, EventCallback.Factory.Create<DataGridRowCountChanged>(
                this, e => events.Add(e))));

        Assert.Equal(3, cut.Instance.FilteredRowCount);
        Assert.Equal(3, cut.Instance.TotalRowCount);
        var e = Assert.Single(events);
        Assert.Equal(3, e.Filtered);
        Assert.Equal(3, e.Total);
    }

    // Reflection: drives DataGrid's private ServerVirtualizationProviderImpl directly, the same
    // Virtualized="true" + OnRangeRequest (no ServerMode) setup DataGridVirtualizedServerModeTests
    // uses. <Virtualize ItemsProvider="..."> does call the provider once on its own during the
    // initial render (it needs a first page regardless of IntersectionObserver), but a later
    // re-fetch — the scroll-driven kind, or the RefreshDataAsync a filter/search/sort change
    // triggers via RefreshVirtualizedAsync — isn't reliably observable through bUnit's headless
    // DOM, so invoking the provider method directly is the deterministic way to simulate one.
    private static async Task InvokeVirtualizationProviderAsync(DataGrid<Row> grid, int startIndex, int count)
    {
        var method = typeof(DataGrid<Row>).GetMethod("ServerVirtualizationProviderImpl", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var request = new Microsoft.AspNetCore.Components.Web.Virtualization.ItemsProviderRequest(startIndex, count, default);
        var task = (ValueTask<Microsoft.AspNetCore.Components.Web.Virtualization.ItemsProviderResult<DataGridBody<Row>.IndexedItem>>)
            method.Invoke(grid, new object[] { request })!;
        await task;
    }

    [Fact]
    public async Task Virtualized_WithoutServerMode_ProviderFetch_UpdatesCounts_And_RaisesEvent()
    {
        // Regression: FilteredRowCount/TotalRowCount used to branch only on ServerMode, so this
        // mode (Virtualized+OnRangeRequest, ServerMode left off) read the unrelated client Items
        // count (0, since Items is empty by design here) instead of the provider's TotalCount,
        // and ServerVirtualizationProviderImpl never raised OnRowCountChanged at all.
        //
        // Unlike DataGridVirtualizedServerModeTests' other cases, <Virtualize ItemsProvider="...">
        // DOES call the provider once on its own during the initial render even in bUnit's
        // headless DOM (it needs an initial page regardless of IntersectionObserver) — so the
        // first fetch below is the real one, driven by mounting the grid, not a manual call.
        var events = new List<DataGridRowCountChanged>();
        var serverTotal = 100;

        ValueTask<DataGridRangeResponse<Row>> Provider(DataGridRangeRequest req) =>
            ValueTask.FromResult(new DataGridRangeResponse<Row>(
                Enumerable.Range(1, Math.Min(req.Count, serverTotal)).Select(i => new Row(i, $"R{i}")).ToList(),
                serverTotal));

        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(x => x.Items, Array.Empty<Row>())
            .Add(x => x.Columns, Columns())
            .Add(x => x.Virtualized, true)
            .Add(x => x.OnRangeRequest, (Func<DataGridRangeRequest, ValueTask<DataGridRangeResponse<Row>>>)Provider)
            .Add(x => x.OnRowCountChanged, EventCallback.Factory.Create<DataGridRowCountChanged>(
                this, e => events.Add(e))));

        Assert.Equal(100, cut.Instance.FilteredRowCount);
        Assert.Equal(100, cut.Instance.TotalRowCount);
        // Mount itself raises an intermediate (0, 0) — client-mode's own OnParametersSetAsync
        // pass runs before Virtualize's initial ItemsProvider fetch lands — so assert on the
        // last event delivered rather than assuming exactly one fired during mount.
        var mountEvent = Assert.Single(events, e => e.Filtered == 100 && e.Total == 100);
        Assert.Equal(events[^1], mountEvent);

        // Simulate the provider's next fetch after a filter change narrowed the server-side
        // result set (the request itself already carries _filters/_globalSearch; here we just
        // change what the fake backend reports and drive the provider directly via reflection —
        // the same reflection pattern HandleFilter/HandleGlobalSearch use elsewhere in this
        // file — since a real filter action's own RefreshVirtualizedAsync -> Virtualize.RefreshDataAsync
        // round-trip isn't reliably observable through bUnit's headless DOM).
        serverTotal = 7;
        events.Clear();
        await cut.InvokeAsync(() => InvokeVirtualizationProviderAsync(cut.Instance, 0, 20));

        Assert.Equal(7, cut.Instance.FilteredRowCount);
        Assert.Equal(7, cut.Instance.TotalRowCount);
        var second = Assert.Single(events);
        Assert.Equal(7, second.Filtered);
        Assert.Equal(7, second.Total);
    }
}
