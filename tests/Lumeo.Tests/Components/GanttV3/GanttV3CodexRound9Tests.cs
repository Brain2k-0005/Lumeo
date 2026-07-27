using System.Reflection;
using Bunit;
using Lumeo.GanttV3;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.GanttV3;

/// <summary>
/// Codex round 9 (PR #379, feat/gantt-v3) - 7 P2 findings: one coherent
/// normalization pass (duration filter + range computation must both
/// date-truncate and agree on which tasks are valid - findings #1-#3, now
/// sharing GanttRowModel.FilterValidDurationTasks/HasValidDuration) plus 4
/// independent follow-ups (header transform not cleared on unregister,
/// async-populated far-from-today ranges wrongly centering on Today, an
/// unclamped tooltip progress value, and direction changes via the global
/// IThemeService going unobserved). See docs/superpowers/gantt-v3-cx9-report.md
/// for the full per-finding writeup. Finding #4's JS fix (gantt-v3.js) has no
/// bUnit angle of its own (bUnit has no real DOM/JS) - see the report for why
/// no new E2E spec was added either.
/// </summary>
public class GanttV3CodexRound9Tests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public GanttV3CodexRound9Tests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<Lumeo.Services.IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static DateTime D(int y, int m, int d) => new(y, m, d);

    private static string PeriodLabel(IRenderedComponent<L.Gantt3> cut) =>
        cut.Find("span.text-sm.font-medium").TextContent;

    // ── GanttRowModel: duration filter must date-truncate first (P2 #1) ─────

    [Fact]
    public void FilterValidDurationTasks_Keeps_A_Same_Day_Task_With_Inverted_Clock_Times()
    {
        // Bug fix (Codex round 9 review, P2 #1): v2's own pipeline order is
        // truncate-THEN-filter (parseDate strips time-of-day for every task
        // BEFORE normalizeTasks' `.filter(t => t.end >= t.start)` ever runs)
        // - a same-day task like 17:00 -> 09:00 is VALID in v2 (both
        // truncate to the SAME calendar day, End.Date >= Start.Date holds)
        // even though the raw clock times are inverted. The round-8 fix
        // compared raw Start/End and wrongly dropped this.
        var sameDayInverted = new L.GanttTask("t1", "Task", D(2026, 1, 5).AddHours(17), D(2026, 1, 5).AddHours(9));

        var rows = GanttRowModel.BuildVisibleRows(new[] { sameDayInverted }, new HashSet<string>());

        Assert.Single(rows);
    }

    [Fact]
    public void FilterValidDurationTasks_Still_Drops_A_Task_Whose_End_Day_Is_Genuinely_Before_Its_Start_Day()
    {
        var genuinelyInverted = new L.GanttTask("t1", "Task", D(2026, 1, 10), D(2026, 1, 5)); // End on an EARLIER calendar day

        var rows = GanttRowModel.BuildVisibleRows(new[] { genuinelyInverted }, new HashSet<string>());

        Assert.Empty(rows);
    }

    // ── Gantt3.ComputeInitialRange: shared normalize+filter step (P2 #2/#3) ─

    [Fact]
    public void Gantt3_VisibleRange_Is_Identical_For_A_Timed_Task_And_Its_Midnight_Twin()
    {
        // Bug fix (Codex round 9 review, P2 #2): ComputeInitialRange's own
        // min/max used to read RAW Start/End - a task carrying a real time-
        // of-day skewed the computed range by that fractional day, out of
        // step with BarGeometry's own (round-8 fixed) date-truncated bar
        // geometry. Routes through the SAME GanttRowModel.FilterValidDurationTasks
        // step BuildVisibleRows uses, then reads .Date on both sides of the
        // min/max, so a timed task now produces the IDENTICAL range as its
        // own midnight-truncated twin.
        var timedTask = new L.GanttTask("t1", "Task", D(2026, 3, 4).AddHours(14).AddMinutes(37), D(2026, 3, 6).AddHours(9).AddMinutes(15));
        var midnightTask = new L.GanttTask("t1", "Task", D(2026, 3, 4), D(2026, 3, 6));

        using var timedCtx = new BunitContext();
        timedCtx.AddLumeoServices();
        timedCtx.Services.AddSingleton<Lumeo.Services.IComponentInteropService>(new TrackingInteropService());
        var timedCut = timedCtx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { timedTask })
            .Add(c => c.ViewMode, L.GanttViewMode.Day));

        using var midnightCtx = new BunitContext();
        midnightCtx.AddLumeoServices();
        midnightCtx.Services.AddSingleton<Lumeo.Services.IComponentInteropService>(new TrackingInteropService());
        var midnightCut = midnightCtx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { midnightTask })
            .Add(c => c.ViewMode, L.GanttViewMode.Day));

        Assert.Equal(PeriodLabel(midnightCut), PeriodLabel(timedCut));
    }

    [Fact]
    public void Gantt3_VisibleRange_Excludes_A_Filtered_Invalid_Task_From_Its_Min_Max()
    {
        // Bug fix (Codex round 9 review, P2 #3): the range must exclude the
        // SAME invalid (non-milestone, End<Start) tasks GanttRowModel drops
        // from Rows - the exact skew the round-8 report flagged as out of
        // scope then. A chart with ONLY the valid task must compute the
        // IDENTICAL range as one with the valid task PLUS a wildly-dated
        // invalid one (which would otherwise stretch the min/max far out).
        var valid = new L.GanttTask("t1", "Task", D(2026, 3, 4), D(2026, 3, 6));
        var invalidFarPast = new L.GanttTask("t2", "Invalid", D(2010, 1, 10), D(2010, 1, 5)); // End before Start

        using var soleCtx = new BunitContext();
        soleCtx.AddLumeoServices();
        soleCtx.Services.AddSingleton<Lumeo.Services.IComponentInteropService>(new TrackingInteropService());
        var soleCut = soleCtx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { valid })
            .Add(c => c.ViewMode, L.GanttViewMode.Day));

        using var withInvalidCtx = new BunitContext();
        withInvalidCtx.AddLumeoServices();
        withInvalidCtx.Services.AddSingleton<Lumeo.Services.IComponentInteropService>(new TrackingInteropService());
        var withInvalidCut = withInvalidCtx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { valid, invalidFarPast })
            .Add(c => c.ViewMode, L.GanttViewMode.Day));

        Assert.Equal(PeriodLabel(soleCut), PeriodLabel(withInvalidCut));
    }

    // ── Gantt3: recenter on tasks (not Today) when Today falls outside the
    //    async-populated range (P2 #5) ───────────────────────────────────────

    [Fact]
    public void Gantt3_Centers_On_The_Tasks_Not_Today_When_Async_Populated_Tasks_Are_Far_From_Today()
    {
        // Bug fix (Codex round 9 review, P2 #5): the empty -> populated
        // recenter always targeted Today (clearing ScrollTargetDate) even
        // when the newly-arrived tasks sit entirely in the past/future and
        // Today isn't rendered anywhere in the just-computed range at all -
        // the viewport centered on empty padding instead of the tasks that
        // just arrived. Fixed by centering on the range's own midpoint
        // instead whenever Today falls outside it (a documented delta from
        // v2 - see the fix's own remarks for why v2's "just don't move" first-
        // render gate has no v3 equivalent to fall back to).
        var farPastTask = new L.GanttTask("t1", "Task", D(2010, 1, 1), D(2010, 1, 10));

        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, Array.Empty<L.GanttTask>())
            .Add(c => c.ViewMode, L.GanttViewMode.Day));

        var scrollToCallsBefore = _interop.GanttV3ScrollToXCallCount;

        cut.Render(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { farPastTask })
            .Add(c => c.ViewMode, L.GanttViewMode.Day));

        Assert.True(_interop.GanttV3ScrollToXCallCount > scrollToCallsBefore);

        // Ground truth: replicate ComputeInitialRange/ApplyPadding's Day-mode
        // math independently of the fix, then the range's own midpoint -
        // what the fix should have targeted instead of Today.
        var cfg = GanttScale.GetConfig(L.GanttViewMode.Day);
        var rangeStart = D(2010, 1, 1).AddDays(-cfg.PadBefore * cfg.Step);
        var rangeEnd = D(2010, 1, 10).AddDays(cfg.PadAfter * cfg.Step);
        var midpoint = rangeStart + new TimeSpan((rangeEnd - rangeStart).Ticks / 2);
        var origin = GanttScale.BuildDateUnits(L.GanttViewMode.Day, rangeStart, rangeEnd)[0];
        var expectedScrollToX = GanttScale.DateToPixel(L.GanttViewMode.Day, origin, midpoint, cfg.ColumnWidth);

        Assert.Equal(expectedScrollToX, _interop.GanttV3ScrollToXCalls[^1], 1);
    }

    [Fact]
    public void Gantt3_Still_Centers_On_Today_When_Async_Populated_Tasks_Do_Contain_Today()
    {
        // Complements the far-from-today test: when Today DOES fall inside
        // the newly-computed range, the pre-existing Today-centering
        // behavior (Codex round 4, P2 #8) must be unchanged.
        var todayStraddlingTask = new L.GanttTask("t1", "Task", DateTime.Today.AddDays(-5), DateTime.Today.AddDays(5));

        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, Array.Empty<L.GanttTask>())
            .Add(c => c.ViewMode, L.GanttViewMode.Day));

        var scrollToCallsBefore = _interop.GanttV3ScrollToXCallCount;

        cut.Render(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { todayStraddlingTask })
            .Add(c => c.ViewMode, L.GanttViewMode.Day));

        Assert.True(_interop.GanttV3ScrollToXCallCount > scrollToCallsBefore);

        var cfg = GanttScale.GetConfig(L.GanttViewMode.Day);
        var rangeStart = DateTime.Today.AddDays(-5).AddDays(-cfg.PadBefore * cfg.Step);
        var rangeEnd = DateTime.Today.AddDays(5).AddDays(cfg.PadAfter * cfg.Step);
        var origin = GanttScale.BuildDateUnits(L.GanttViewMode.Day, rangeStart, rangeEnd)[0];
        var expectedScrollToX = GanttScale.DateToPixel(L.GanttViewMode.Day, origin, DateTime.Today, cfg.ColumnWidth);

        Assert.Equal(expectedScrollToX, _interop.GanttV3ScrollToXCalls[^1], 1);
    }

    // ── GanttBar: tooltip progress must use the same clamp as the fill (P2 #6) ─

    [Fact]
    public async Task GanttBar_Tooltip_Clamps_An_Out_Of_Range_Progress_Value_To_100()
    {
        var task = new L.GanttTask("t1", "Task", D(2026, 1, 2), D(2026, 1, 9), Progress: 150);
        var cut = _ctx.Render<L.GanttBar>(p => p
            .Add(c => c.Task, task)
            .Add(c => c.X, 0d)
            .Add(c => c.Width, 114d));

        // TooltipContent only actually mounts (role="tooltip") once genuinely
        // open — pin it via the SAME touch tap-to-pin sequence
        // GanttBar_Touch_Tap_Still_Pins_The_Tooltip_Open already uses
        // (synchronous, no ShowDelay timer to race), rather than the
        // previous version of this test, which never opened the tooltip at
        // all and so never actually rendered the text it claimed to check.
        var wrapper = cut.Find("[data-task-id='t1']");
        await wrapper.TriggerEventAsync("onpointerdown", new PointerEventArgs { PointerType = "touch" });
        await wrapper.TriggerEventAsync("onclick", new MouseEventArgs());

        // Deflake (net8.0-only intermittent failure): scoped to the tooltip's
        // own rendered TEXT content, not the whole component markup — the
        // latter also contains attribute values (e.g. this bar's own
        // `id="gantt-bar-{Guid}"`/tooltip-wrapper id), and an unlucky
        // Guid.NewGuid() draw could occasionally contain the literal
        // substring "150" too, an assertion collision unrelated to the
        // progress-clamp behavior this test actually verifies. TextContent
        // never includes attribute values, only visible text, so it can't
        // collide with an id's own digits.
        var tooltipText = cut.Find("[role='tooltip']").TextContent;
        Assert.DoesNotContain("150", tooltipText);
        Assert.Contains("100", tooltipText);
    }

    // ── Gantt3: direction changes via the global IThemeService (P2 #7) ─────

    [Fact]
    public async Task Gantt3_Requests_A_Recenter_When_The_Global_ThemeService_Direction_Changes_Without_A_DirectionProvider()
    {
        // Bug fix (Codex round 9 review, P2 #7): the direction-flip reconcile
        // only ever ran from OnParametersSetAsync, which is invoked when a
        // PARENT re-renders this component - a DirectionProvider ancestor
        // toggling its Direction PARAMETER goes through exactly that path,
        // but the global ThemeService's own CurrentDirection changing
        // directly (no DirectionProvider ancestor at all, as here) raises
        // ONLY Theme.OnThemeChanged with no parent re-render involved, so
        // the flip went unnoticed. Gantt3 now subscribes directly.
        var task = new L.GanttTask("t1", "Task", D(2026, 1, 1), D(2026, 1, 5));
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { task })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.ShowTreePane, false));

        var scrollToCallsBefore = _interop.GanttV3ScrollToXCallCount;

        var theme = _ctx.Services.GetRequiredService<Lumeo.Services.IThemeService>();
        await theme.SetDirectionAsync(LayoutDirection.Rtl);

        cut.WaitForAssertion(() => Assert.True(_interop.GanttV3ScrollToXCallCount > scrollToCallsBefore,
            $"expected a service-driven direction flip to request a recenter, call count stayed at {_interop.GanttV3ScrollToXCallCount}"));
    }

    [Fact]
    public async Task Gantt3_Unsubscribes_From_The_ThemeService_On_Dispose()
    {
        // Mirrors ThemeToggleBattleWave3Tests' own established technique for
        // proving a disposed component left no dangling handler on the
        // shared, long-lived ThemeService event.
        using var ctx = new BunitContext();
        ctx.AddLumeoServices();
        ctx.Services.AddSingleton<Lumeo.Services.IComponentInteropService>(new TrackingInteropService());

        var task = new L.GanttTask("t1", "Task", D(2026, 1, 1), D(2026, 1, 5));
        ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { task })
            .Add(c => c.ViewMode, L.GanttViewMode.Day));

        var theme = ctx.Services.GetRequiredService<ThemeService>();
        Assert.Equal(1, ThemeChangedSubscriberCount(theme));

        await ctx.DisposeAsync();

        Assert.Equal(0, ThemeChangedSubscriberCount(theme));
    }

    private static int ThemeChangedSubscriberCount(ThemeService svc)
    {
        var field = typeof(ThemeService).GetField("OnThemeChanged", BindingFlags.NonPublic | BindingFlags.Instance);
        var del = (Delegate?)field?.GetValue(svc);
        return del?.GetInvocationList().Length ?? 0;
    }
}
