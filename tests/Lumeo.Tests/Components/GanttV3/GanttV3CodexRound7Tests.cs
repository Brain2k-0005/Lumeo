using System.Linq;
using System.Reflection;
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
///
/// Also carries the cx7b review fix-round's own coverage (2 Important:
/// visual-suite path anchoring - covered by GanttParityVisualTests itself,
/// no bUnit angle - and the browser-today thrash fix below). See
/// docs/superpowers/gantt-v3-cx7-report.md's own cx7b section.
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

    // ── Gantt3: browser-date thrash fix (cx7b review, Important #2) ────────
    //
    // Superseded the original round-7 "Gantt3_Reresolves_The_Browser_Date_
    // On_A_Later_Render_Once_The_Servers_Own_Day_Has_Advanced" test, which
    // asserted the OLD (buggy) staleness gate's own behavior - comparing the
    // server's current day against the CACHED BROWSER day. That looked
    // right but wasn't: for a user whose browser sits WEST of the server,
    // the server's own midnight lands hours before theirs, so
    // "server day > cached browser day" stayed true for the WHOLE gap in
    // between, re-querying the browser on literally every render in that
    // window (proven directly below: 15 renders, still cached far in the
    // past, and only the ONE firstRender query ever happens now). Fixed by
    // gating on the SERVER's own day advancing since the LAST CHECK
    // (_lastCheckedServerDay) instead of against the browser's cached value.
    //
    // These tests use reflection to read/write the private
    // _lastCheckedServerDay field directly - there's no other observable way
    // to simulate "the server's day already advanced since the component's
    // last check" without controlling the real wall clock, and the field's
    // own semantics (a plain per-instance timestamp of the last check, nothing
    // event-driven) make direct manipulation safe and precise here.

    private static void SetLastCheckedServerDay(L.Gantt3 instance, DateTime value)
    {
        var field = typeof(L.Gantt3).GetField("_lastCheckedServerDay", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);
        field!.SetValue(instance, (DateTime?)value);
    }

    [Fact]
    public void Gantt3_Does_Not_Requery_The_Browser_On_Every_Render_While_The_Servers_Day_Is_Unchanged()
    {
        // Regression for the west-of-server thrash: a cached browser date far
        // in the past (simulating a user whose browser hasn't yet reached
        // the server's own current calendar day) must NOT trigger a fresh
        // interop call on every subsequent render - only firstRender's own
        // resolution should have queried at all, since _lastCheckedServerDay
        // gets seeded to the server's CURRENT day right after that, and stays
        // unchanged (same real day) for every render performed by this test.
        _interop.GanttV3LocalDateToReturn = "2020-01-01";

        var task = new L.GanttTask("t1", "Task", D(2020, 1, 1), D(2020, 1, 5));
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { task })
            .Add(c => c.ViewMode, L.GanttViewMode.Month));

        cut.WaitForAssertion(() => Assert.Equal(1, _interop.GanttV3GetLocalDateCallCount));

        // Several more renders, all still the same real server day - the OLD
        // gate would have re-queried on every single one of these (server's
        // real today is always > the far-past 2020-01-01 cached value).
        for (var i = 0; i < 5; i++)
        {
            // "extra-{i}" (not "t{i}") -- the fixture's own task above is
            // already "t1", and Task.Id must stay unique per Gantt3 instance
            // (GanttBar's own @key relies on it; see Gantt3CodexRound15Tests'
            // finding #4). "t{i}" collided with it at i=1, which used to be a
            // harmless accident (nothing enforced task-id uniqueness before
            // round 15's @key fix) but now throws a duplicate-key render
            // exception -- unrelated to what this test actually exercises.
            cut.Render(p => p
                .Add(c => c.Tasks, new List<L.GanttTask> { task, new($"extra-{i}", $"Task {i}", D(2020, 1, 1), D(2020, 1, 2)) })
                .Add(c => c.ViewMode, L.GanttViewMode.Month));
        }

        Assert.Equal(1, _interop.GanttV3GetLocalDateCallCount);
    }

    [Fact]
    public void Gantt3_Requeries_Exactly_Once_When_The_Servers_Own_Day_Has_Advanced_Since_The_Last_Check()
    {
        // Complements the no-thrash test above: the gate must still actually
        // fire - exactly once - once the SERVER's own day genuinely advances
        // past whatever was recorded at the last check. Simulates that
        // advance directly (see SetLastCheckedServerDay's own remarks) rather
        // than waiting on the real wall clock.
        _interop.GanttV3LocalDateToReturn = "2026-07-22";

        var task = new L.GanttTask("t1", "Task", D(2026, 1, 1), D(2026, 1, 5));
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { task })
            .Add(c => c.ViewMode, L.GanttViewMode.Month));

        cut.WaitForAssertion(() => Assert.Equal(1, _interop.GanttV3GetLocalDateCallCount));

        SetLastCheckedServerDay(cut.Instance, DateTime.Today.AddDays(-1));

        cut.Render(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { task })
            .Add(c => c.ViewMode, L.GanttViewMode.Month));

        Assert.Equal(2, _interop.GanttV3GetLocalDateCallCount);

        // A further render on the SAME (now-current, just-re-seeded) server
        // day must not re-query again - the "exactly once per advance" half
        // of the contract.
        cut.Render(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { task })
            .Add(c => c.ViewMode, L.GanttViewMode.Month));

        Assert.Equal(2, _interop.GanttV3GetLocalDateCallCount);
    }
}
