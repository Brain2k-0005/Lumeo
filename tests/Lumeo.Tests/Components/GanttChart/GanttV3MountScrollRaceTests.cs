using System.Reflection;
using Bunit;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.GanttV3;

/// <summary>
/// Issue #390 — the mount-time "scroll to today" duplicate. GanttTimeline's
/// OnAfterRenderAsync (firstRender branch) only marks the initial-scroll
/// intent consumed (<c>_lastConsumedScrollRequestId</c>) AFTER its own
/// <c>ScrollToCenterAsync</c> interop call resolves — several awaited
/// round-trips (SyncDragRegistrationAsync, SyncBarContextMenuRegistrationAsync,
/// SyncWheelZoomRegistrationAsync, ReconcileHeaderScrollSyncAsync) run first,
/// all BEFORE that claim. A second render dispatched while this firstRender
/// pass is still mid-flight (in real usage: GanttChart's own OnAfterRenderAsync
/// calls StateHasChanged BEFORE its own RefreshBrowserTodayAsync/
/// RefreshBrowserNowAsync awaits — see GanttChart.OnAfterRenderAsync's own
/// remarks) reads the still-stale sentinel and independently concludes the
/// scroll is still owed, issuing its OWN duplicate ScrollToCenterAsync call.
///
/// Both calls target the identical pixel (Today never changes mid-mount), so
/// the duplicate is invisible in the common case — it only surfaces when
/// something ELSE writes scrollLeft between the two calls landing (exactly
/// what GanttV3StickyHeaderTests' and GanttDragParityTests' own manual
/// scrollLeft resets do), which is what turned this into a genuinely fragile
/// E2E failure (a `-5633` px delta, GanttV3StickyHeaderTests.
/// Header_columns_stay_aligned_with_the_row_canvas_after_a_horizontal_scroll)
/// rather than a purely theoretical race.
///
/// Fix shape mirrors GanttTimeline._rangeExtensionInFlight (TryRequestRangeExtensionAsync):
/// claim the "intent owed" state BEFORE the first await in the block, not
/// after the round-trip that state guards — see OnAfterRenderAsync's own
/// remarks for the full reasoning.
/// </summary>
public class GanttV3MountScrollRaceTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public GanttV3MountScrollRaceTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<Lumeo.Services.IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static DateTime D(int y, int m, int d) => new(y, m, d);

    [Fact]
    public async Task A_Render_Interleaved_With_FirstRenders_Own_Awaits_Does_Not_Duplicate_The_Initial_Scroll()
    {
        var tasks = new List<L.GanttTask> { new("t1", "Task", D(2026, 1, 1), D(2026, 1, 20)) };
        var rangeStart = D(2025, 12, 1);
        var rangeEnd = D(2026, 3, 1);
        var today = D(2026, 1, 15);

        // Suspends GanttTimeline's OWN scroll-to-today interop call
        // (GanttV3ScrollToXAsync, reached via ScrollToCenterAsync) — set
        // BEFORE the component ever renders, so the automatic firstRender
        // pass bUnit triggers below runs every one of its OWN preceding
        // awaits (SyncDragRegistrationAsync etc. — all Task.CompletedTask in
        // this mock, so they don't yield) and suspends right here, its FIRST
        // genuine await.
        var gate = new TaskCompletionSource();
        _interop.GanttV3ScrollToXGate = gate;

        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, tasks)
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, rangeStart)
            .Add(c => c.RangeEnd, rangeEnd)
            .Add(c => c.Today, today)
            .Add(c => c.ScrollToTodayRequestId, 0));

        // firstRender's own pass reached (and recorded) its scroll call, then
        // suspended on the gate — not yet resumed, not yet marked consumed.
        Assert.Equal(1, _interop.GanttV3ScrollToXCallCount);

        var onAfterRenderAsync = typeof(L.GanttTimeline).GetMethod(
            "OnAfterRenderAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;

        // A second render dispatched WHILE firstRender's own pass is still
        // mid-await — exactly the scenario the issue describes. Captured
        // (not awaited to completion) since, under the bug, this ALSO
        // reaches ScrollToCenterAsync and suspends on the very same gate.
        Task secondRender = Task.CompletedTask;
        await cut.InvokeAsync(() =>
        {
            secondRender = (Task)onAfterRenderAsync.Invoke(cut.Instance, new object[] { false })!;
        });

        // Resume both (whichever suspended on the gate).
        gate.SetResult();
        _interop.GanttV3ScrollToXGate = null;
        await secondRender;
        for (var i = 0; i < 20; i++) await Task.Yield();

        // Exactly ONE scroll-to-today call for the entire mount. Under the
        // bug this is 2: the interleaved render's own stale-sentinel read
        // (ScrollToTodayRequestId != _lastConsumedScrollRequestId, still
        // true because firstRender's own claim hadn't landed yet) let it
        // independently issue a second, duplicate ScrollToCenterAsync call
        // targeting the identical pixel.
        Assert.Equal(1, _interop.GanttV3ScrollToXCallCount);
    }

    // Disable-check companion (negative control): confirms the assertion
    // above is not vacuous by construction — with NOTHING gated, mount
    // still issues exactly one scroll call, so "1" is a real, specific
    // prediction the fixture can distinguish from "0" or "2", not merely
    // whatever value happens to fall out.
    [Fact]
    public void An_Ungated_Mount_Issues_Exactly_One_Scroll_To_Today_Call()
    {
        var tasks = new List<L.GanttTask> { new("t1", "Task", D(2026, 1, 1), D(2026, 1, 20)) };

        _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, tasks)
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2025, 12, 1))
            .Add(c => c.RangeEnd, D(2026, 3, 1))
            .Add(c => c.Today, D(2026, 1, 15))
            .Add(c => c.ScrollToTodayRequestId, 0));

        Assert.Equal(1, _interop.GanttV3ScrollToXCallCount);
    }
}
