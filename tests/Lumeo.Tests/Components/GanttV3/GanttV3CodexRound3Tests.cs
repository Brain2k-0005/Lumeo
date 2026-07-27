using System.Globalization;
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
/// Codex round 3 (PR #379, feat/gantt-v3) — 7 P2 findings + 1 comment-only
/// minor, following the round-2 restructure. See
/// docs/superpowers/gantt-v3-cx3-report.md for the full per-finding writeup.
/// Findings #1 (Virtualize scroll-ancestor) and #7 (RTL scroll normalization)
/// are covered by E2E specs (a real browser is needed to observe Virtualize's
/// actual ancestor resolution and native RTL scrollLeft semantics — bUnit's
/// headless DOM materializes every Virtualize item regardless of viewport, and
/// has no CSSOM to resolve computed direction/overflow against); the tests
/// here cover #1's STRUCTURAL surface only (the ScrollHost parameter's two
/// code paths) plus #2, #3, #4, #5, #6 in full.
/// </summary>
public class GanttV3CodexRound3Tests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public GanttV3CodexRound3Tests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<Lumeo.Services.IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static DateTime D(int y, int m, int d) => new(y, m, d);

    // ── GanttTimeline: ScrollHost structural surface (finding #1) ───────────

    [Fact]
    public void GanttTimeline_Drops_Its_Own_Horizontal_Scroll_And_Skips_Header_Sync_Registration_When_ScrollHost_Is_Supplied()
    {
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { new("t1", "Task", D(2026, 1, 1), D(2026, 1, 5)) })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.ScrollHost, new ElementReference("host")));

        // Bug fix (Codex round 3, P2 #1): before this fix, this wrapper ALWAYS
        // carried overflow-x-auto, making it a scroll container on both axes
        // (CSS overflow-promotion — see GanttTimeline.ScrollHost's own
        // remarks) and hijacking Blazor's Virtualize ancestor walk. When a
        // caller supplies its own ScrollHost, this div must not set
        // overflow-x-auto at all.
        var scrollWrapper = cut.Find(".lumeo-gantt-v3-canvas-scroll");
        Assert.DoesNotContain("overflow-x-auto", scrollWrapper.GetAttribute("class"));

        // The header-scroll-sync JS transform exists only to work around an
        // intervening scroll container hijacking the header's own
        // position:sticky — with ScrollHost supplied there's none left, and
        // registering it anyway would double-apply the horizontal offset (see
        // GanttTimeline.OnAfterRenderAsync's own remarks). Must stay
        // unregistered in this mode.
        Assert.Equal(0, _interop.GanttV3RegisterHeaderScrollSyncCallCount);

        // Scroll-to-today interop still fires normally on firstRender in this
        // mode — a caller-supplied @ref (like Gantt3's own outer pane) is
        // already a fully resolved ElementReference by the time a CHILD
        // component's first OnAfterRenderAsync runs (a parent's own ref is
        // captured well before that point), so there is no "stale ancestor
        // ref" race here — it just targets the supplied host instead of this
        // component's own (now non-scrolling) row-canvas div.
        Assert.Equal(1, _interop.GanttV3ScrollToXCallCount);
    }

    [Fact]
    public void GanttTimeline_Keeps_Its_Prior_Self_Contained_Behavior_When_ScrollHost_Is_Not_Supplied()
    {
        // Regression guard: every EXISTING standalone usage (e.g. a test that
        // renders GanttTimeline alone, with no Gantt3 wrapping it) must be
        // completely unaffected by the ScrollHost parameter's addition.
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { new("t1", "Task", D(2026, 1, 1), D(2026, 1, 5)) })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10)));

        var scrollWrapper = cut.Find(".lumeo-gantt-v3-canvas-scroll");
        Assert.Contains("overflow-x-auto", scrollWrapper.GetAttribute("class"));
        Assert.Equal(1, _interop.GanttV3RegisterHeaderScrollSyncCallCount);
        Assert.Equal(1, _interop.GanttV3ScrollToXCallCount);
    }

    // ── Gantt3: recenter on empty→populated (finding #2) ────────────────────

    [Fact]
    public void Gantt3_Rerequests_The_Initial_Scroll_When_An_Empty_Task_List_Is_Later_Populated()
    {
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, Array.Empty<L.GanttTask>())
            .Add(c => c.ViewMode, L.GanttViewMode.Day));

        // ComputeInitialRange's empty-list fallback branch centers the window
        // AROUND today by construction, so ShouldAttemptTodayScroll is true at
        // mount — this fires once, exactly as any populated-from-the-start
        // render would.
        Assert.Equal(1, _interop.GanttV3ScrollToXCallCount);

        var today = DateTime.Today;
        cut.Render(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { new("t1", "Task", today.AddDays(-2), today.AddDays(2)) })
            .Add(c => c.ViewMode, L.GanttViewMode.Day));

        // Bug fix (Codex round 3, P2 #2): before this fix, recomputing
        // VisibleRange around the newly-arrived task set never told
        // GanttTimeline to re-attempt its initial-viewport scroll — the count
        // stayed at 1 forever. It must now go 1 -> 2 across exactly this
        // empty-to-populated transition.
        Assert.Equal(2, _interop.GanttV3ScrollToXCallCount);
    }

    [Fact]
    public void Gantt3_Does_Not_Rerequest_The_Scroll_When_An_Already_Populated_Task_List_Changes()
    {
        // Regression guard: the fix is scoped to "previous set was empty" —
        // an ordinary tasks-changed update (already-populated -> still
        // populated) must NOT get an extra scroll re-request every time.
        var today = DateTime.Today;
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { new("t1", "Task", today.AddDays(-2), today.AddDays(2)) })
            .Add(c => c.ViewMode, L.GanttViewMode.Day));

        Assert.Equal(1, _interop.GanttV3ScrollToXCallCount);

        cut.Render(p => p
            .Add(c => c.Tasks, new List<L.GanttTask>
            {
                new("t1", "Task", today.AddDays(-2), today.AddDays(2)),
                new("t2", "Task 2", today.AddDays(3), today.AddDays(6)),
            })
            .Add(c => c.ViewMode, L.GanttViewMode.Day));

        Assert.Equal(1, _interop.GanttV3ScrollToXCallCount);
    }

    // ── GanttTimeline: TodayHighlight decoupled from scroll (finding #3) ────

    [Fact]
    public void GanttTimeline_Still_Attempts_A_Scroll_When_TodayHighlight_Is_False()
    {
        // Bug fix (Codex round 3, P2 #3): TodayHighlight="false" used to ALSO
        // suppress the scroll-to-today attempt, conflating "don't draw the
        // marker line" with "don't center the viewport" — reproducing the
        // exact "empty grid on first paint" regression the P1 fix exists to
        // prevent, just gated by a different flag. v2's own scroll block runs
        // unconditionally; only the marker draw is gated by todayHighlight.
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { new("t1", "Task", D(2026, 1, 1), D(2026, 1, 5)) })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.Today, D(2026, 1, 3))
            .Add(c => c.TodayHighlight, false));

        Assert.Equal(1, _interop.GanttV3ScrollToXCallCount);
        // The marker itself must still be suppressed — TodayHighlight keeps
        // controlling THAT, unchanged.
        Assert.Empty(cut.FindAll(".lumeo-gantt-v3-today-line"));
    }

    [Fact]
    public void GanttTimeline_Still_Shows_The_Marker_And_Scrolls_When_TodayHighlight_Is_True()
    {
        // Baseline (unchanged by the fix): both the marker and the scroll
        // fire when TodayHighlight is left at its default.
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { new("t1", "Task", D(2026, 1, 1), D(2026, 1, 5)) })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.Today, D(2026, 1, 3))
            .Add(c => c.TodayHighlight, true));

        Assert.Equal(1, _interop.GanttV3ScrollToXCallCount);
        Assert.Single(cut.FindAll(".lumeo-gantt-v3-today-line"));
    }

    // ── GanttTimeline: empty-task state message (finding #4) ────────────────

    [Fact]
    public void GanttTimeline_Shows_The_Empty_Message_When_There_Are_No_Rows()
    {
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, Array.Empty<L.GanttTask>())
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10)));

        var empty = cut.Find(".lumeo-gantt-v3-empty");
        Assert.Equal("No tasks to display", empty.TextContent);
    }

    [Fact]
    public void GanttTimeline_Hides_The_Empty_Message_When_Tasks_Are_Present()
    {
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { new("t1", "Task", D(2026, 1, 1), D(2026, 1, 5)) })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10)));

        Assert.Empty(cut.FindAll(".lumeo-gantt-v3-empty"));
    }

    // ── GanttBar: milestone tooltip normalization (finding #5) ──────────────

    [Fact]
    public void GanttBar_Milestone_Tooltip_Shows_The_Same_Date_Twice_Even_When_End_Differs_From_Start()
    {
        // A milestone with an inconsistent (non-matching) End — nothing
        // upstream enforces Start==End for a caller-supplied milestone.
        // GanttScale.BarGeometry's milestone branch positions the diamond
        // using ONLY Start (a milestone is a point, not a span), so before
        // this fix the tooltip claimed a multi-day range for a bar that
        // visually occupies a single point.
        var task = new L.GanttTask("m1", "Kickoff", D(2026, 3, 8), D(2026, 3, 20), IsMilestone: true);
        var cut = _ctx.Render<L.GanttBar>(p => p
            .Add(c => c.Task, task)
            .Add(c => c.X, 0d)
            .Add(c => c.Width, 22d));

        // TooltipContent only mounts once open (Tooltip's Presence-gated
        // render — see TooltipTests.OpenTooltip for the same pattern);
        // mouseenter on the wrapper starts Tooltip's own ShowDelay timer
        // (GanttBar doesn't expose a Delay passthrough, so it's the real
        // 200ms default here) — WaitForState polls for it to actually land
        // instead of assuming it's synchronous.
        cut.Find("[data-milestone='true']").TriggerEvent("onmouseenter", new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cut.WaitForState(() => cut.FindAll("[role='tooltip']").Count > 0, TimeSpan.FromSeconds(2));

        var tooltipText = cut.Find("[role='tooltip']").TextContent;
        Assert.Contains("Mar 8, 2026", tooltipText);
        Assert.DoesNotContain("Mar 20, 2026", tooltipText);
    }

    [Fact]
    public void GanttBar_Regular_Bar_Tooltip_Still_Shows_The_Raw_Start_End_Range()
    {
        // Regression guard: the milestone-only normalization must not affect
        // an ordinary duration bar's tooltip.
        var task = new L.GanttTask("t1", "Design", D(2026, 1, 2), D(2026, 1, 9));
        var cut = _ctx.Render<L.GanttBar>(p => p
            .Add(c => c.Task, task)
            .Add(c => c.X, 0d)
            .Add(c => c.Width, 114d));

        cut.Find("[data-task-id='t1']").TriggerEvent("onmouseenter", new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cut.WaitForState(() => cut.FindAll("[role='tooltip']").Count > 0, TimeSpan.FromSeconds(2));

        var tooltipText = cut.Find("[role='tooltip']").TextContent;
        Assert.Contains("Jan 2, 2026", tooltipText);
        Assert.Contains("Jan 9, 2026", tooltipText);
    }

    // ── Gantt3: Today-recenter unit alignment (finding #6) ──────────────────

    [Fact]
    public void Gantt3_GoToTodayAsync_In_Month_Mode_Aligns_The_Origin_So_Bar_Pixels_Match_DateToPixel_Ground_Truth()
    {
        // Deliberately far from "today" so the pre-click window (task min/max
        // +/- 12 months' padding, Month mode) can never accidentally already
        // straddle today — keeps the ground-truth math below unambiguous
        // regardless of which day this suite happens to run on.
        var fixedDate = new DateTime(2020, 6, 15);
        var task = new L.GanttTask("t1", "Task", fixedDate, fixedDate);

        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { task })
            .Add(c => c.ViewMode, L.GanttViewMode.Month));

        // Ground truth: replicate ComputeInitialRange's Month branch (single
        // task, so min==max==fixedDate) and GoToTodayAsync's own recenter math
        // (Gantt3.razor's own remarks) independently of the fix, to compute
        // the EXPECTED aligned post-click range start.
        var cfg = GanttScale.GetConfig(L.GanttViewMode.Month);
        var monthStart = new DateTime(fixedDate.Year, fixedDate.Month, 1);
        var preClickStart = monthStart.AddMonths(-cfg.PadBefore);
        var preClickEnd = monthStart.AddMonths(cfg.PadAfter);
        var width = preClickEnd - preClickStart;
        var half = new TimeSpan(width.Ticks / 2);
        var rawStart = DateTime.Today - half;
        var expectedAlignedStart = new DateTime(rawStart.Year, rawStart.Month, 1);
        var expectedX = GanttScale.DateToPixel(L.GanttViewMode.Month, expectedAlignedStart, fixedDate, cfg.ColumnWidth);

        var todayButton = cut.FindAll("button").First(b => b.TextContent.Trim() == "Today");
        todayButton.Click();

        var barStyle = cut.Find("[data-task-id='t1']").GetAttribute("style") ?? "";
        Assert.Contains($"--lumeo-gantt-bar-x:{expectedX.ToString(CultureInfo.InvariantCulture)}px", barStyle);
    }
}
