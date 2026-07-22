using System.Linq;
using Bunit;
using Lumeo.GanttV3;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.GanttV3;

/// <summary>
/// Codex round 7 (PR #379, feat/gantt-v3) - 4 P2 findings, deep edge cases
/// surfaced against round 6's own fixes. See
/// docs/superpowers/gantt-v3-cx7-report.md for the full per-finding writeup.
/// Finding #1 (GanttScale month-fraction clamp degeneracy) is covered in
/// GanttScaleTests.cs alongside the existing round-6 tests it refines.
/// </summary>
public class GanttV3CodexRound7Tests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public GanttV3CodexRound7Tests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<Lumeo.Services.IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static DateTime D(int y, int m, int d) => new(y, m, d);

    // ── GanttTimeline: scroll-host rebinding (P2 #2) ────────────────────────

    [Fact]
    public void GanttTimeline_Registers_Vertical_Scroll_Tracking_When_A_Caller_Supplied_Host_Becomes_Ready_Later()
    {
        // A caller-supplied ScrollHost that starts out uncaptured (empty Id -
        // see GanttTimeline.OnAfterRenderAsync's own remarks on the ancestor
        // ref-capture race) must still bind once it resolves to a real
        // element on a later render. This is the pre-existing "not ready ->
        // ready" path, re-verified after the round-7 refactor generalized it
        // into an identity-based check.
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { new("t1", "Task", D(2026, 1, 1), D(2026, 1, 5)) })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.ScrollHost, default(ElementReference)));

        Assert.Equal(0, _interop.GanttV3RegisterVerticalScrollTrackingCallCount);

        cut.Render(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { new("t1", "Task", D(2026, 1, 1), D(2026, 1, 5)) })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.ScrollHost, new ElementReference("host-a")));

        Assert.Equal(1, _interop.GanttV3RegisterVerticalScrollTrackingCallCount);
        Assert.Equal(0, _interop.GanttV3UnregisterVerticalScrollTrackingCallCount);
    }

    [Fact]
    public void GanttTimeline_Rebinds_Vertical_Scroll_Tracking_When_The_Scroll_Host_Identity_Changes()
    {
        // Bug fix (Codex round 7 review, P2 #2): the old `_scrollHostWasReady`
        // bool only ever tracked a ONE-WAY "not ready -> ready" transition -
        // once true, it could never fire again, so a parent later swapping in
        // a genuinely DIFFERENT host element (re-parenting, or replacing the
        // shared scroll pane outright) left the OLD registration in place
        // forever: the timeline kept reporting scroll positions for an
        // element that was no longer the real scroll ancestor. The fix tracks
        // the host's own captured Id and rebinds (unregister old, register
        // new) whenever that identity actually changes.
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { new("t1", "Task", D(2026, 1, 1), D(2026, 1, 5)) })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.ScrollHost, new ElementReference("host-a")));

        Assert.Equal(1, _interop.GanttV3RegisterVerticalScrollTrackingCallCount);
        Assert.Equal(0, _interop.GanttV3UnregisterVerticalScrollTrackingCallCount);

        cut.Render(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { new("t1", "Task", D(2026, 1, 1), D(2026, 1, 5)) })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.ScrollHost, new ElementReference("host-b")));

        Assert.Equal(1, _interop.GanttV3UnregisterVerticalScrollTrackingCallCount);
        Assert.Equal(2, _interop.GanttV3RegisterVerticalScrollTrackingCallCount);
    }

    // ── Gantt3: browser-date staleness fix (P2 #3) ──────────────────────────

    [Fact]
    public void Gantt3_GoToTodayAsync_Always_Requeries_The_Browser_Date_On_An_Explicit_Today_Click()
    {
        // Bug fix (Codex round 7 review, P2 #3): _browserToday used to be
        // resolved exactly once (firstRender) and never touched again -
        // clicking Today on a long-mounted chart (one left open across local
        // midnight) recentered on a potentially STALE cached day. The fix
        // always re-queries the browser on an explicit Today action,
        // unconditionally, rather than relying only on the cheap staleness
        // heuristic used elsewhere (OnAfterRenderAsync's own periodic check).
        _interop.GanttV3LocalDateToReturn = "2026-07-22";

        var task = new L.GanttTask("t1", "Task", D(2026, 1, 1), D(2026, 1, 5));
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { task })
            .Add(c => c.ViewMode, L.GanttViewMode.Month));

        cut.WaitForAssertion(() => Assert.Equal(1, _interop.GanttV3GetLocalDateCallCount));

        var todayButton = cut.FindAll("button").First(b => b.TextContent.Trim() == "Today");
        todayButton.Click();

        Assert.Equal(2, _interop.GanttV3GetLocalDateCallCount);
    }

    [Fact]
    public void Gantt3_Reresolves_The_Browser_Date_On_A_Later_Render_Once_The_Servers_Own_Day_Has_Advanced()
    {
        // Complements the explicit-click test above: even WITHOUT a Today
        // click, a later render must re-query the browser once the cheap,
        // interop-free staleness signal fires (the server's own current day
        // has advanced past whatever was cached) - this is what actually
        // corrects a long-mounted chart's marker/highlight without requiring
        // user interaction at all. Uses a browser date placed in the PAST
        // relative to the real system clock this suite runs under, so the
        // signal is guaranteed to already be true - in fact this fires on the
        // very NEXT render this component performs on its own (the firstRender
        // resolution's own StateHasChanged calls are enough to trigger it),
        // regardless of which day the suite happens to execute on. Asserting
        // "at least 2" rather than an exact count keeps this robust against
        // exactly how many follow-up renders bUnit's dispatcher schedules.
        _interop.GanttV3LocalDateToReturn = "2020-01-01";

        var task = new L.GanttTask("t1", "Task", D(2020, 1, 1), D(2020, 1, 5));
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { task })
            .Add(c => c.ViewMode, L.GanttViewMode.Month));

        cut.WaitForAssertion(() => Assert.True(_interop.GanttV3GetLocalDateCallCount >= 2,
            $"expected the stale cached date to trigger at least one re-query, got {_interop.GanttV3GetLocalDateCallCount} calls"));
    }
}
