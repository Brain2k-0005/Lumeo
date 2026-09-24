using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Lumeo;
using Lumeo.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// SQL Analyst field report against 5.11.0, LU-12 and LU-13 — both about
/// <c>Virtualized="true"</c> + <c>OnRangeRequest</c> (server virtualization).
///
/// LU-12 (Medium): <c>IsLoading="true"</c> used to tear the whole scroll body down to a plain
/// skeleton tbody — <see cref="DataGridBody{TItem}"/>'s <c>Context.IsLoading</c> branch matched
/// FIRST, unconditionally, before the server-virtualization branch, unmounting Blazor's
/// <c>&lt;Virtualize ItemsProvider&gt;</c> (and with it the ONLY thing that ever calls
/// <c>OnRangeRequest</c>) along with it. A consumer whose own loading flag flips back to false
/// from inside that same <c>OnRangeRequest</c> handler could never get there — permanently
/// stuck showing the skeleton. Fixed: the plain skeleton-replaces-everything branch is now
/// skipped for server virtualization; that branch renders the same skeleton rows BESIDE a
/// still-mounted <c>&lt;Virtualize&gt;</c> instead (DataGridBody.RenderSkeletonRows /
/// <c>_virtualize</c> field via <c>@ref</c>).
///
/// LU-13 (Low): a <c>LayoutStorageKey</c>-persisted sort used to reach the FIRST range request
/// only on a second fetch. PR #514 (DocFlow D2) restores a persisted layout in
/// <c>OnAfterRenderAsync(firstRender)</c> before ServerMode's own first request — but
/// <c>&lt;Virtualize&gt;</c>'s first request fires from the CHILD's own initialization, which
/// runs as part of the SAME render pass that mounts it, strictly before that await. Fixed with
/// <c>DataGrid._initialVirtualizationLayoutGate</c>: <c>ServerVirtualizationProviderImpl</c>
/// awaits it before issuing its first request, and it's released only once the persisted-layout
/// read (found or not) has resolved.
///
/// <c>&lt;Virtualize&gt;</c>'s own IntersectionObserver-driven scroll fetch doesn't run in
/// bUnit's headless DOM (see DataGridVirtualizedServerModeTests' remarks), but its DIRECT
/// <c>RefreshDataAsync()</c> entry point (wired to the public <c>RefreshVirtualizedAsync</c>)
/// does — and only if <c>&lt;Virtualize&gt;</c> is actually mounted (its <c>@ref</c> sets the
/// body's private <c>_virtualize</c> field, which <c>RefreshVirtualizeAsync</c> is a no-op
/// without). That's the deterministic signal LU-12's tests use: if the provider fires on a
/// direct refresh, <c>&lt;Virtualize&gt;</c> is mounted; if it never fires, it was torn down.
/// LU-13's test instead BLOCKS the persisted-layout read (<see cref="BlockingLocalStorageInterop"/>,
/// same technique as OverlayExitAnimationRaceTests' BlockingOpenInterop) to reproduce the race
/// deterministically.
/// </summary>
public class DataGridVirtualizedLoadingStateTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataGridVirtualizedLoadingStateTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Row(int Id, string Name);

    private static List<DataGridColumn<Row>> Cols() => new()
    {
        new() { Field = "Id", Title = "ID" },
        new() { Field = "Name", Title = "Name" },
    };

    private static readonly List<Row> Data = Enumerable.Range(1, 50).Select(i => new Row(i, $"R{i}")).ToList();

    [Fact]
    public async Task IsLoading_True_Does_Not_Unmount_Virtualize_Provider_Still_Reachable()
    {
        var calls = 0;
        ValueTask<DataGridRangeResponse<Row>> Provider(DataGridRangeRequest req)
        {
            calls++;
            return ValueTask.FromResult(new DataGridRangeResponse<Row>(
                Data.Skip(req.StartIndex).Take(req.Count).ToList(), Data.Count));
        }

        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Array.Empty<Row>())
            .Add(g => g.Columns, Cols())
            .Add(g => g.Virtualized, true)
            .Add(g => g.IsLoading, true)
            .Add(g => g.OnRangeRequest, (Func<DataGridRangeRequest, ValueTask<DataGridRangeResponse<Row>>>)Provider));

        // A direct refresh only reaches the provider if <Virtualize> is actually mounted
        // (DataGridBody._virtualize set via @ref) — the pre-fix behavior unmounted it
        // whenever IsLoading was true, so this would never fire.
        await cut.InvokeAsync(() => cut.Instance.RefreshVirtualizedAsync());

        Assert.True(calls > 0,
            "OnRangeRequest was never reached — <Virtualize> appears to have been unmounted while IsLoading=true (LU-12 regression)");
    }

    [Fact]
    public void IsLoading_True_Still_Shows_Skeleton_Rows_For_Server_Virtualization()
    {
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Array.Empty<Row>())
            .Add(g => g.Columns, Cols())
            .Add(g => g.Virtualized, true)
            .Add(g => g.IsLoading, true)
            .Add(g => g.OnRangeRequest, (DataGridRangeRequest req) =>
                ValueTask.FromResult(new DataGridRangeResponse<Row>(new List<Row>(), 0))));

        // The loading cue must still be visible (LU-12's "no skeleton can be shown" half) —
        // it just has to sit beside <Virtualize> rather than replace it.
        Assert.NotEmpty(cut.FindAll("[data-slot='skeleton']"));
    }

    [Fact]
    public async Task IsLoading_False_After_Loading_Provider_Still_Reachable_No_Regression()
    {
        var calls = 0;
        ValueTask<DataGridRangeResponse<Row>> Provider(DataGridRangeRequest req)
        {
            calls++;
            return ValueTask.FromResult(new DataGridRangeResponse<Row>(
                Data.Skip(req.StartIndex).Take(req.Count).ToList(), Data.Count));
        }

        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Array.Empty<Row>())
            .Add(g => g.Columns, Cols())
            .Add(g => g.Virtualized, true)
            .Add(g => g.IsLoading, false)
            .Add(g => g.OnRangeRequest, (Func<DataGridRangeRequest, ValueTask<DataGridRangeResponse<Row>>>)Provider));

        await cut.InvokeAsync(() => cut.Instance.RefreshVirtualizedAsync());

        Assert.True(calls > 0, "baseline regression: OnRangeRequest unreachable even with IsLoading=false");
    }

    [Fact]
    public void Non_Virtualized_IsLoading_Still_Replaces_Whole_Body_With_Skeleton()
    {
        // Regression guard: the plain (non-virtualized) skeleton-replaces-everything path
        // is UNCHANGED — this fix is scoped to server virtualization only.
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Data)
            .Add(g => g.Columns, Cols())
            .Add(g => g.IsLoading, true));

        Assert.NotEmpty(cut.FindAll("[data-slot='skeleton']"));
        // None of the real row content should have rendered under the plain skeleton path.
        Assert.DoesNotContain("R1<", cut.Markup);
    }

    // --- LU-13: the first server-virtualization range request must carry a pending
    // LayoutStorageKey restore's sort, not the default one. ---

    /// <summary>Blocks <see cref="LoadFromLocalStorage"/> until <see cref="Release"/> is
    /// called, the deterministic stand-in for the async localStorage round-trip still being
    /// in flight when <c>&lt;Virtualize&gt;</c>'s first <c>ItemsProvider</c> call would
    /// otherwise go out. Same technique as OverlayExitAnimationRaceTests.BlockingOpenInterop.</summary>
    private sealed class BlockingLocalStorageInterop : TrackingInteropService
    {
        private readonly TaskCompletionSource<string?> _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override ValueTask<string?> LoadFromLocalStorage(string key) => new(_gate.Task);
        public void Release(string? json) => _gate.TrySetResult(json);
    }

    [Fact]
    public async Task Initial_Range_Request_Carries_A_Pending_Persisted_Layout_Restores_Sort()
    {
        var interop = new BlockingLocalStorageInterop();
        // Registered AFTER AddLumeoServices so this binding wins for IComponentInteropService
        // (matches OverlayExitAnimationRaceTests' own setup comment).
        _ctx.Services.AddScoped<IComponentInteropService>(_ => interop);

        var requests = new List<DataGridRangeRequest>();
        ValueTask<DataGridRangeResponse<Row>> Provider(DataGridRangeRequest req)
        {
            requests.Add(req);
            return ValueTask.FromResult(new DataGridRangeResponse<Row>(Data.Take(req.Count).ToList(), Data.Count));
        }

        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Array.Empty<Row>())
            .Add(g => g.Columns, Cols())
            .Add(g => g.Virtualized, true)
            .Add(g => g.EnableLayoutPersistence, true)
            .Add(g => g.LayoutStorageKey, "lu13-test")
            .Add(g => g.OnRangeRequest, (Func<DataGridRangeRequest, ValueTask<DataGridRangeResponse<Row>>>)Provider));

        // The persisted-layout read is still pending (blocked on the interop) — nothing has
        // reached OnRangeRequest yet. No manual trigger here: once released below,
        // ApplyLayoutAsync's OWN internal RefreshVirtualizedAsync call (DocFlow D1, PR #514)
        // is what fires the request — plain awaited C#, not dependent on a scroll observer,
        // so it's deterministic in bUnit's headless DOM.
        await Task.Delay(50);
        Assert.Empty(requests);

        // Release the persisted layout with a restored sort that differs from the default.
        var layout = new DataGridLayout
        {
            Sorts = new List<SortDescriptor> { new("Name", SortDirection.Descending) }
        };
        interop.Release(System.Text.Json.JsonSerializer.Serialize(layout));
        await Task.Delay(150);

        Assert.Single(requests); // exactly one request — no separate "default then restored" pair
        Assert.Contains(requests[0].Sorts ?? new List<SortDescriptor>(),
            s => s.Field == "Name" && s.Direction == SortDirection.Descending);
    }

    [Fact]
    public async Task No_Persisted_Layout_Still_Releases_The_Gated_Natural_Fetch_Exactly_Once()
    {
        var interop = new BlockingLocalStorageInterop();
        _ctx.Services.AddScoped<IComponentInteropService>(_ => interop);

        var requests = new List<DataGridRangeRequest>();
        ValueTask<DataGridRangeResponse<Row>> Provider(DataGridRangeRequest req)
        {
            requests.Add(req);
            return ValueTask.FromResult(new DataGridRangeResponse<Row>(Data.Take(req.Count).ToList(), Data.Count));
        }

        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Array.Empty<Row>())
            .Add(g => g.Columns, Cols())
            .Add(g => g.Virtualized, true)
            .Add(g => g.EnableLayoutPersistence, true)
            .Add(g => g.LayoutStorageKey, "lu13-test-empty")
            .Add(g => g.OnRangeRequest, (Func<DataGridRangeRequest, ValueTask<DataGridRangeResponse<Row>>>)Provider));

        // <Virtualize>'s own initial mount fetch DOES run in bUnit (unlike a scroll-triggered
        // one) — it's just gated here, still pending against the blocked interop.
        await Task.Delay(50);
        Assert.Empty(requests);

        // Nothing persisted (null) — LoadPersistedLayoutAsync never calls ApplyLayoutAsync, so
        // the gate must still release unconditionally, letting the natural fetch (still
        // holding its original request) through with the default sort instead of hanging
        // forever waiting on a layout that will never arrive.
        interop.Release(null);
        await Task.Delay(100);

        Assert.Single(requests);
        Assert.True(requests[0].Sorts is null or { Count: 0 });
    }

    [Fact]
    public async Task No_LayoutStorageKey_Never_Waits()
    {
        // No EnableLayoutPersistence -> no gate created at all -> the natural initial fetch
        // must go through immediately, even against an interop whose LoadFromLocalStorage
        // would otherwise block forever (it's simply never called).
        var interop = new BlockingLocalStorageInterop();
        _ctx.Services.AddScoped<IComponentInteropService>(_ => interop);

        var requests = new List<DataGridRangeRequest>();
        ValueTask<DataGridRangeResponse<Row>> Provider(DataGridRangeRequest req)
        {
            requests.Add(req);
            return ValueTask.FromResult(new DataGridRangeResponse<Row>(Data.Take(req.Count).ToList(), Data.Count));
        }

        _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Array.Empty<Row>())
            .Add(g => g.Columns, Cols())
            .Add(g => g.Virtualized, true)
            .Add(g => g.OnRangeRequest, (Func<DataGridRangeRequest, ValueTask<DataGridRangeResponse<Row>>>)Provider));

        await Task.Delay(50);

        Assert.Single(requests);
    }
}
