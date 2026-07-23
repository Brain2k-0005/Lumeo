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
/// Codex round 18 (PR #379) — the final review round's findings, attached to the
/// review that was misread as clean and merged. Follow-up on master via
/// fix/gantt-v3-post-merge-followup:
///
///  #1 (P1) — a direct-navigation action (prev/next/Today) mutated VisibleRange
///       OUTSIDE the reconcile pipeline without claiming a generation, so a
///       reconcile suspended in its live-center capture would resume, still pass
///       its IsCurrentReconcile check, and commit its now-stale mode/range on top
///       of the navigation. Fixed: nav claims the next generation (the same
///       supersession the reconciler already uses for the parameter/theme paths).
///  #2 (P2) — GanttTimeline's standalone fallback (Rows==null) copied every task
///       1:1, bypassing the duration filter every other GanttV3 entry point
///       applies — an invalid End&lt;Start task rendered an 8px sliver bar. Fixed:
///       route BuildFallbackRows through GanttRowModel.FilterValidDurationTasks.
///  #3 (P2) — the shipped utility bundles were stale and missing the group-header
///       stripe's arbitrary accent-opacity utility (bg-accent/[0.18]); regenerated
///       (verified by CSS-pipeline grep, not a bUnit test).
///
/// See docs/superpowers/gantt-v3-cx18-report.md for the full writeup.
/// </summary>
public class GanttV3CodexRound18Tests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public GanttV3CodexRound18Tests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<Lumeo.Services.IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static DateTime D(int y, int m, int d) => new(y, m, d);

    // ── Finding #1: navigation supersedes a suspended reconcile ─────────────

    [Fact]
    public async Task Finding1_A_Navigation_During_A_Suspended_Reconcile_Supersedes_It()
    {
        var task = new L.GanttTask("t1", "Task", D(2026, 1, 10), D(2026, 1, 20));
        var tasks = new List<L.GanttTask> { task };
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, tasks)
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.ShowTreePane, false));

        // The mount range in Day mode (task min/max ± padding) — a date RANGE.
        var mountPeriod = cut.Find("span.text-sm.font-medium").TextContent;
        Assert.Contains("–", mountPeriod);

        // Reconcile: Day -> Month (needs a live-center capture). Suspend it in
        // that capture, so it is stuck having claimed a generation but not yet
        // committed SetViewMode/SetVisibleRange.
        var gate = new TaskCompletionSource<double?>();
        _interop.GanttV3ScrollCenterXGate = gate;

        Task reconcile = Task.CompletedTask;
        await cut.InvokeAsync(() =>
        {
            reconcile = cut.Instance.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(L.Gantt3.Tasks)] = tasks,
                [nameof(L.Gantt3.ViewMode)] = L.GanttViewMode.Month,
                [nameof(L.Gantt3.ShowTreePane)] = false,
            }));
        });
        Assert.False(reconcile.IsCompleted, "the reconcile should still be awaiting its live-center capture");

        // A Today navigation lands WHILE the reconcile is suspended. It mutates
        // VisibleRange directly (still in Day mode) and must supersede the
        // in-flight reconcile.
        var goToToday = typeof(L.Gantt3).GetMethod("GoToTodayAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await cut.InvokeAsync(async () => await (Task)goToToday.Invoke(cut.Instance, null)!);

        // Resume the reconcile. Under the bug it would now commit its Month mode
        // + self-centered range on top of the Today navigation — so the label
        // would flip to Month's single "MMMM yyyy" (no "–"). Under the fix it
        // finds its generation superseded by the nav and abandons its ENTIRE
        // commit, leaving the Today-navigated Day range in place.
        gate.SetResult(0);
        await reconcile;

        var finalPeriod = cut.Find("span.text-sm.font-medium").TextContent;
        // Still Day mode — the Month switch abandoned (a superseded reconcile
        // never even runs SetViewMode).
        Assert.Contains("–", finalPeriod);
        // …and the Today navigation stands: it recentered the window on today
        // (far from the task's own Jan-2026 range), so the range actually moved.
        Assert.NotEqual(mountPeriod, finalPeriod);
    }

    [Fact]
    public async Task Finding1_A_Shift_During_A_Suspended_Reconcile_Supersedes_It()
    {
        // Same guarantee for the prev/next path (ShiftAsync), which is fully
        // synchronous — so it can only ever run BEFORE or AFTER a suspended
        // reconcile's capture, never interleaved within it.
        var task = new L.GanttTask("t1", "Task", D(2026, 1, 10), D(2026, 1, 20));
        var tasks = new List<L.GanttTask> { task };
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, tasks)
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.ShowTreePane, false));

        var mountPeriod = cut.Find("span.text-sm.font-medium").TextContent;

        var gate = new TaskCompletionSource<double?>();
        _interop.GanttV3ScrollCenterXGate = gate;

        Task reconcile = Task.CompletedTask;
        await cut.InvokeAsync(() =>
        {
            reconcile = cut.Instance.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(L.Gantt3.Tasks)] = tasks,
                [nameof(L.Gantt3.ViewMode)] = L.GanttViewMode.Month,
                [nameof(L.Gantt3.ShowTreePane)] = false,
            }));
        });
        Assert.False(reconcile.IsCompleted);

        var shiftNext = typeof(L.Gantt3).GetMethod("ShiftToNextAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await cut.InvokeAsync(async () => await (Task)shiftNext.Invoke(cut.Instance, null)!);

        gate.SetResult(0);
        await reconcile;

        var finalPeriod = cut.Find("span.text-sm.font-medium").TextContent;
        Assert.Contains("–", finalPeriod);      // still Day mode — the Month switch abandoned
        Assert.NotEqual(mountPeriod, finalPeriod); // the shift moved the window and stands
    }

    // ── Finding #2: standalone timeline applies the duration filter ─────────

    [Fact]
    public void Finding2_Standalone_Timeline_Drops_An_Invalid_Duration_Task()
    {
        // Rows is left null, so GanttTimeline builds its own fallback rows. An
        // invalid End<Start, non-milestone task must be dropped exactly as the
        // Gantt3-fed (Rows-supplied) path already drops it — previously the
        // standalone fallback copied every task 1:1 and rendered the 8px sliver.
        var valid = new L.GanttTask("t1", "Valid", D(2026, 1, 1), D(2026, 1, 10));
        var invalid = new L.GanttTask("bad", "Invalid", D(2026, 1, 20), D(2026, 1, 5)); // End < Start, non-milestone

        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { valid, invalid })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2025, 12, 1))
            .Add(c => c.RangeEnd, D(2026, 3, 1)));

        Assert.Single(cut.FindAll("[data-task-id='t1']"));
        Assert.Empty(cut.FindAll("[data-task-id='bad']"));
    }

    [Fact]
    public void Finding2_A_Standalone_Milestone_With_End_Before_Start_Still_Renders()
    {
        // A milestone is exempt from the End>=Start rule (its End is forced to
        // its Start upstream), so the shared filter keeps it — the standalone
        // path must match that, not drop it.
        var milestone = new L.GanttTask("m", "Milestone", D(2026, 1, 15), D(2026, 1, 1)) { IsMilestone = true };

        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { milestone })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2025, 12, 1))
            .Add(c => c.RangeEnd, D(2026, 3, 1)));

        Assert.Single(cut.FindAll("[data-task-id='m']"));
    }
}
