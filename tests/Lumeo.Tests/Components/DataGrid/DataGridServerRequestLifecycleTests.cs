using Microsoft.AspNetCore.Components;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Lumeo;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// #442. The initial server request used to fire from exactly one place — OnInitializedAsync —
/// and every other dispatch was a user action. So ServerMode was read once, before children
/// registered and before the first render, and anything that made it or the handler arrive
/// later left the grid in its empty state permanently: no error, no console output, and no
/// public way to ask again (RefreshVirtualizedAsync is a documented no-op outside virtualized
/// mode). A grid that silently shows "no data" is indistinguishable from an empty result.
/// </summary>
public class DataGridServerRequestLifecycleTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataGridServerRequestLifecycleTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Row(int Id, string Name);

    private static List<DataGridColumn<Row>> Cols() =>
        new() { new DataGridColumn<Row> { Field = "Name", Title = "Name" } };

    [Fact]
    public async Task Server_Mode_Requests_Once_On_Mount()
    {
        var calls = 0;

        _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, new List<Row>())
            .Add(g => g.Columns, Cols())
            .Add(g => g.ServerMode, true)
            .Add(g => g.OnServerRequest, EventCallback.Factory.Create<DataGridServerRequest>(
                this, _ => Interlocked.Increment(ref calls))));

        await Task.Delay(150);

        // Exactly once: the first-render retry must not double up on the OnInitializedAsync path.
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ServerMode_Turning_On_After_Mount_Still_Requests()
    {
        var calls = 0;
        var handler = EventCallback.Factory.Create<DataGridServerRequest>(
            this, _ => Interlocked.Increment(ref calls));

        // Mounted with ServerMode off — the host has not resolved its flag yet.
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, new List<Row>())
            .Add(g => g.Columns, Cols())
            .Add(g => g.ServerMode, false)
            .Add(g => g.OnServerRequest, handler));

        await Task.Delay(100);
        Assert.Equal(0, calls);

        cut.Render(p => p
            .Add(g => g.Items, new List<Row>())
            .Add(g => g.Columns, Cols())
            .Add(g => g.ServerMode, true)
            .Add(g => g.OnServerRequest, handler));

        await Task.Delay(150);

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task RefreshAsync_Re_Runs_The_Handler_With_The_Current_State()
    {
        var calls = 0;

        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, new List<Row>())
            .Add(g => g.Columns, Cols())
            .Add(g => g.ServerMode, true)
            .Add(g => g.OnServerRequest, EventCallback.Factory.Create<DataGridServerRequest>(
                this, _ => Interlocked.Increment(ref calls))));

        await Task.Delay(150);
        var afterMount = calls;

        // The case this exists for: something outside the grid — a page-level filter bar, a
        // save, a tab switch — invalidated the data.
        await cut.Instance.RefreshAsync();

        Assert.Equal(afterMount + 1, calls);
    }

    [Fact]
    public async Task RefreshAsync_Is_A_No_Op_Without_Server_Mode()
    {
        var calls = 0;

        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, new List<Row> { new(1, "a") })
            .Add(g => g.Columns, Cols())
            .Add(g => g.ServerMode, false)
            .Add(g => g.OnServerRequest, EventCallback.Factory.Create<DataGridServerRequest>(
                this, _ => Interlocked.Increment(ref calls))));

        await cut.Instance.RefreshAsync();

        // There is no server to ask. Silently doing nothing beats inventing a request.
        Assert.Equal(0, calls);
    }
}
