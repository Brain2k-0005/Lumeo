using AngleSharp.Dom;
using Bunit;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.GanttV3;

/// <summary>
/// Codex round 2 (PR #379, feat/gantt-v3) — the sticky-header restructure
/// (finding #3, "sticky header STILL broken"), GanttTimeline's v2-parity
/// scroll-to-today gate (finding #8, "visual snapshot drift"), and Gantt3's
/// browser-local "today" resolution (finding #9, "v3 today uses server
/// timezone"). See docs/superpowers/gantt-v3-cx2-report.md for the full
/// per-finding writeup.
/// </summary>
public class GanttV3CodexRound2Tests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public GanttV3CodexRound2Tests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<Lumeo.Services.IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static DateTime D(int y, int m, int d) => new(y, m, d);

    // ── GanttTimeline: sticky-header restructure (finding #3) ───────────────

    [Fact]
    public void GanttTimeline_Header_Is_A_Sibling_Of_The_Horizontal_Scroll_Wrapper_Not_Nested_Inside_It()
    {
        // Regression: the header must NOT be a descendant of the
        // overflow-x-auto row-canvas wrapper — that nesting is exactly what
        // hijacked `position: sticky` in the first place (see
        // GanttTimeline.razor's own remarks). Both the header and the
        // dedicated overflow-x-auto wrapper must exist as direct children of
        // the component's root, with the header NOT inside the wrapper.
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { new("t1", "Task", D(2026, 1, 1), D(2026, 1, 5)) })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10)));

        var header = cut.Find(".lumeo-gantt-v3-header");
        var scrollWrapper = cut.Find(".lumeo-gantt-v3-canvas-scroll");

        Assert.DoesNotContain(header, scrollWrapper.Descendants<IElement>());
        Assert.Contains("sticky", header.GetAttribute("class"));
        Assert.Contains("overflow-x-auto", scrollWrapper.GetAttribute("class"));
    }

    [Fact]
    public async Task GanttTimeline_Registers_The_Header_Scroll_Sync_On_Mount_And_Unregisters_On_Dispose()
    {
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { new("t1", "Task", D(2026, 1, 1), D(2026, 1, 5)) })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10)));

        Assert.Equal(1, _interop.GanttV3RegisterHeaderScrollSyncCallCount);
        Assert.Equal(0, _interop.GanttV3UnregisterHeaderScrollSyncCallCount);

        await cut.Instance.DisposeAsync();

        Assert.Equal(1, _interop.GanttV3UnregisterHeaderScrollSyncCallCount);
    }

    // ── GanttTimeline: v2-parity scroll gate (finding #8) ───────────────────

    [Fact]
    public void GanttTimeline_Attempts_A_Scroll_When_Today_Is_Past_The_Window_End()
    {
        // v2 parity: v2's scroll gate is `todayPx > 0` (gantt-v2.js:684),
        // looser than the marker's own render gate — v2 still attempts to
        // scroll toward today even when today is PAST the rendered window,
        // relying on the browser's scrollLeft clamp to land at the far-right
        // edge. Before this fix, GanttTimeline reused TodayInRange (upper AND
        // lower bounded) for the scroll gate too, so a "today past the
        // window" fixture silently stopped scrolling at all, staying at the
        // DOM default (the far LEFT / earliest dates) — the root cause of the
        // visual-snapshot-drift finding.
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { new("t1", "Task", D(2026, 1, 1), D(2026, 1, 5)) })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.Today, D(2027, 1, 1))); // WAY past RangeEnd

        Assert.Equal(1, _interop.GanttV3ScrollToXCallCount);
    }

    [Fact]
    public void GanttTimeline_Still_Attempts_A_Scroll_When_Today_Is_Before_The_Window_Start()
    {
        // Bug fix (Codex round 5, P2 #9): this test originally asserted the
        // OPPOSITE — v2's `todayPx > 0` gate (mirrored here) skips the scroll
        // attempt entirely when today is before the window's origin, relying
        // on the DOM's own untouched default scrollLeft to already show "the
        // earliest edge". That default only happens to BE the earliest edge
        // under LTR; under RTL it is NOT (see ShouldAttemptTodayScroll's own
        // remarks — native scrollLeft 0 there is the RTL START, the physical
        // RIGHT edge, wherever GanttTree ends up pinned), so relying on it
        // left the timeline scrolled entirely out of view under RTL. The
        // fix always attempts the scroll now and lets centerOn's own
        // Math.max(0, ...) clamp (direction-correct in both directions) land
        // at the earliest edge instead of skipping the call and hoping the
        // DOM's own default happens to agree — this is a deliberate,
        // documented behavior change, not a regression.
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { new("t1", "Task", D(2026, 1, 1), D(2026, 1, 5)) })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.Today, D(2025, 1, 1))); // before RangeStart

        Assert.Equal(1, _interop.GanttV3ScrollToXCallCount);
    }

    [Fact]
    public void GanttTimeline_Still_Attempts_A_Scroll_When_Today_Is_Within_The_Window()
    {
        // Baseline (unchanged by the fix): today inside the window scrolls,
        // same as before.
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { new("t1", "Task", D(2026, 1, 1), D(2026, 1, 5)) })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.Today, D(2026, 1, 3)));

        Assert.Equal(1, _interop.GanttV3ScrollToXCallCount);
    }

    [Fact]
    public void GanttTimeline_Reattempts_The_Scroll_When_The_Today_Parameter_Changes_After_Mount()
    {
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { new("t1", "Task", D(2026, 1, 1), D(2026, 1, 5)) })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.Today, D(2026, 1, 3)));
        Assert.Equal(1, _interop.GanttV3ScrollToXCallCount);

        // Gantt3's browser-date resolution can land a render or two after
        // mount — a changed Today must re-trigger the scroll-to-today attempt
        // exactly like a ScrollToTodayRequestId bump does.
        cut.Render(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { new("t1", "Task", D(2026, 1, 1), D(2026, 1, 5)) })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.Today, D(2026, 1, 4)));

        Assert.Equal(2, _interop.GanttV3ScrollToXCallCount);
    }

    // ── Gantt3: browser-local "today" resolution (finding #9) ───────────────

    [Fact]
    public void Gantt3_Resolves_The_Browsers_Local_Date_And_Uses_It_For_The_Initial_Range()
    {
        // The empty-task-list fallback branch of ComputeInitialRange is the
        // most direct, deterministic way to observe WHICH "today" Gantt3
        // actually used — regardless of the real wall-clock date the test
        // happens to run on, a fixed far-future browser date always produces
        // the SAME PeriodLabel.
        _interop.GanttV3LocalDateToReturn = "2099-06-15";

        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, Array.Empty<L.GanttTask>())
            .Add(c => c.ViewMode, L.GanttViewMode.Month));

        // ComputeInitialRange (empty list): minDate = 2099-06-15 - 7d = 2099-06-08;
        // Month-unit padding: new DateTime(2099, 6, 1).AddMonths(-12) = 2098-06-01.
        // The interop resolution + StateHasChanged land in a render AFTER the
        // one _ctx.Render's initial call itself observes (OnAfterRenderAsync's
        // await, even over an already-completed Task, yields back to the
        // Blazor renderer's dispatcher before resuming) — WaitForAssertion
        // polls until that follow-up render has landed.
        cut.WaitForAssertion(() => Assert.Equal("June 2098", cut.Find("span.text-sm.font-medium").TextContent));
    }

    [Fact]
    public void Gantt3_Falls_Back_To_Server_DateTime_Today_When_Interop_Is_Unavailable()
    {
        // GanttV3LocalDateToReturn left at its default (null) — mirrors
        // prerendering / a non-Gantt-aware IComponentInteropService implementer.
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, Array.Empty<L.GanttTask>())
            .Add(c => c.ViewMode, L.GanttViewMode.Month));

        var expectedMin = DateTime.Today.AddDays(-7);
        var expectedStart = new DateTime(expectedMin.Year, expectedMin.Month, 1).AddMonths(-12);
        Assert.Equal(expectedStart.ToString("MMMM yyyy"), cut.Find("span.text-sm.font-medium").TextContent);

        Assert.Equal(1, _interop.GanttV3GetLocalDateCallCount); // resolution was attempted exactly once
    }
}
