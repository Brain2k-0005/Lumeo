using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>Shift-click range selection in virtualized server mode (Virtualized + OnRangeRequest).
/// The grid holds no row list there, so the range is fetched by index through the provider.
/// bUnit cannot drive Virtualize's scrolling, so the tests go through the public
/// SelectRangeAsync and the cascaded context's index-aware toggle.</summary>
public class DataGridVirtualizedRangeSelectionTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataGridVirtualizedRangeSelectionTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Row(int Id, string Name);

    /// <summary>Renders inside the grid's ChildContent and hands the cascaded context out.</summary>
    private sealed class ContextProbe : ComponentBase
    {
        [CascadingParameter] public DataGridContext<Row>? Context { get; set; }
        [Parameter] public Action<DataGridContext<Row>>? OnContext { get; set; }
        protected override void OnParametersSet() { if (Context is not null) OnContext?.Invoke(Context); }
    }

    private static List<Row> Data() => Enumerable.Range(1, 100).Select(i => new Row(i, $"R{i}")).ToList();

    private static List<DataGridColumn<Row>> Cols() => new()
    {
        new() { Field = "Id", Title = "Id" }, new() { Field = "Name", Title = "Name" },
    };

    private sealed class Host
    {
        public List<Row> Data = DataGridVirtualizedRangeSelectionTests.Data();
        public List<DataGridRangeRequest> Requests = new();
        public IReadOnlyList<Row> Selected = Array.Empty<Row>();
        public DataGridContext<Row>? Context;

        public ValueTask<DataGridRangeResponse<Row>> Provide(DataGridRangeRequest req)
        {
            Requests.Add(req);
            return ValueTask.FromResult(new DataGridRangeResponse<Row>(
                Data.Skip(req.StartIndex).Take(req.Count).ToList(), Data.Count));
        }
    }

    private IRenderedComponent<DataGrid<Row>> RenderVirtualized(Host host) => _ctx.Render<DataGrid<Row>>(p => p
        .Add(g => g.Items, Array.Empty<Row>())
        .Add(g => g.Columns, Cols())
        .Add(g => g.Virtualized, true)
        .Add(g => g.SelectionMode, DataGridSelectionMode.Multiple)
        .Add(g => g.SelectionKeySelector, (Row r) => r.Id)
        .Add(g => g.OnRangeRequest, (Func<DataGridRangeRequest, ValueTask<DataGridRangeResponse<Row>>>)host.Provide)
        .Add(g => g.SelectedItemsChanged, EventCallback.Factory.Create<IReadOnlyList<Row>>(this, s => host.Selected = s))
        .AddChildContent<ContextProbe>(c => c.Add(x => x.OnContext, (DataGridContext<Row> ctx) => host.Context = ctx)));

    [Fact]
    public async Task SelectRangeAsync_Fetches_The_Range_Through_OnRangeRequest()
    {
        var host = new Host();
        var cut = RenderVirtualized(host);
        host.Requests.Clear();

        await cut.Instance.SelectRangeAsync(6, 2);

        var fetch = Assert.Single(host.Requests);
        Assert.Equal(2, fetch.StartIndex);
        Assert.Equal(5, fetch.Count);
        cut.WaitForAssertion(() => Assert.Equal(new[] { 3, 4, 5, 6, 7 }, host.Selected.Select(r => r.Id).OrderBy(i => i)));
    }

    [Fact]
    public void Shift_Click_Selects_The_Range_Beyond_The_Loaded_Rows()
    {
        var host = new Host();
        var cut = RenderVirtualized(host);
        var ctx = host.Context!;
        host.Requests.Clear();

        // plain click on row 2 anchors, Shift-click on row 40 fills the range by index
        cut.InvokeAsync(() => ctx.ToggleSelectionModifiedAt!(host.Data[2], 2, false, false));
        cut.InvokeAsync(() => ctx.ToggleSelectionModifiedAt!(host.Data[40], 40, true, false));

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(39, host.Selected.Count);
            Assert.Equal(3, host.Selected.Min(r => r.Id));
            Assert.Equal(41, host.Selected.Max(r => r.Id));
        });
        var fetch = Assert.Single(host.Requests);
        Assert.Equal((2, 39), (fetch.StartIndex, fetch.Count));
    }

    [Fact]
    public async Task A_Refresh_Drops_The_Anchor_Index_So_Shift_Falls_Back_To_A_Toggle()
    {
        var host = new Host();
        var cut = RenderVirtualized(host);
        var ctx = host.Context!;

        await cut.InvokeAsync(() => ctx.ToggleSelectionModifiedAt!(host.Data[2], 2, false, false));
        await cut.Instance.RefreshVirtualizedAsync();
        host.Requests.Clear();
        await cut.InvokeAsync(() => ctx.ToggleSelectionModifiedAt!(host.Data[9], 9, true, false));

        cut.WaitForAssertion(() => Assert.Equal(new[] { 3, 10 }, host.Selected.Select(r => r.Id).OrderBy(i => i)));
        Assert.Empty(host.Requests);
    }

    [Fact]
    public async Task SelectRangeAsync_Addresses_The_Displayed_Rows_Without_Virtualization()
    {
        IReadOnlyList<Row> selected = Array.Empty<Row>();
        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.Columns, Cols())
            .Add(g => g.PageSize, 20)
            .Add(g => g.SelectionMode, DataGridSelectionMode.Multiple)
            .Add(g => g.SelectedItemsChanged, EventCallback.Factory.Create<IReadOnlyList<Row>>(this, s => selected = s)));

        await cut.Instance.SelectRangeAsync(1, 3);

        cut.WaitForAssertion(() => Assert.Equal(new[] { 2, 3, 4 }, selected.Select(r => r.Id).OrderBy(i => i)));
    }
}
