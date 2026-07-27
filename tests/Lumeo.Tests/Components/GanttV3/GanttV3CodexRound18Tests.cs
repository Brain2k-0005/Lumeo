using System.Reflection;
using Bunit;
using Lumeo.GanttV3;
using Lumeo.Services;
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

    // ── Finding #1 (refined by Codex round 18-f2, P1): navigation supersedes a
    //    suspended reconcile WITHOUT dropping its pending parameter inputs ─────

    // Suspends a parameter-driven Day->Month reconcile, then runs a navigation
    // via `nav` while it is stuck. The navigation supersedes the stale reconcile
    // AND re-applies the pending Month, so the mode LANDS after navigating —
    // asserted by the period label becoming Month's single "MMMM yyyy" (no "–").
    // Under the round-18 (pre-f2) behavior the mode was silently DROPPED and the
    // label stayed a Day-mode "… – …" range.
    private async Task AssertNavigationReappliesPendingModeAsync(string navMethod)
    {
        var task = new L.GanttTask("t1", "Task", D(2026, 1, 10), D(2026, 1, 20));
        var tasks = new List<L.GanttTask> { task };
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, tasks)
            .Add(c => c.ViewMode, L.GanttViewMode.Day)   // UNCONTROLLED (no ViewModeChanged)
            .Add(c => c.ShowTreePane, false));

        Assert.Contains("–", cut.Find("span.text-sm.font-medium").TextContent); // mount: Day range

        // Parent pushes Day -> Month; suspend its live-center capture.
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
        Assert.False(reconcile.IsCompleted, "the mode reconcile should still be awaiting its capture");

        // Un-gate subsequent captures so the navigation's OWN re-apply can
        // complete; the suspended pass keeps awaiting the ORIGINAL gate above.
        _interop.GanttV3ScrollCenterXGate = null;
        _interop.GanttV3ScrollCenterXToReturn = 0;

        var nav = typeof(L.Gantt3).GetMethod(navMethod, BindingFlags.NonPublic | BindingFlags.Instance)!;
        await cut.InvokeAsync(async () => await (Task)nav.Invoke(cut.Instance, null)!);

        // Resume the superseded original reconcile — it abandons cleanly.
        gate.SetResult(0);
        await reconcile;

        // The pending Month LANDED after the navigation (not dropped): the label
        // is Month's "MMMM yyyy", not a Day/Week "… – …" range.
        Assert.DoesNotContain("–", cut.Find("span.text-sm.font-medium").TextContent);
    }

    [Fact]
    public Task Reapply_A_Today_Navigation_During_A_Suspended_Mode_Change_Still_Lands_The_Mode()
        => AssertNavigationReappliesPendingModeAsync("GoToTodayAsync");

    [Fact]
    public Task Reapply_A_Shift_Navigation_During_A_Suspended_Mode_Change_Still_Lands_The_Mode()
        => AssertNavigationReappliesPendingModeAsync("ShiftToNextAsync");

    [Fact]
    public async Task Reapply_A_Superseded_Tasks_And_Mode_Pass_Lands_Both_After_A_Navigation()
    {
        // Finding #2 (round 18-f2): the pending-input preservation is general —
        // a superseded pass carrying BOTH a task-set change AND a mode change
        // must land both after the navigation supersedes it.
        var taskA = new L.GanttTask("a", "A", D(2026, 1, 10), D(2026, 1, 20));
        var taskB = new L.GanttTask("b", "B", D(2026, 3, 1), D(2026, 3, 10)); // a different, non-empty set
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { taskA })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.ShowTreePane, false));
        Assert.Single(cut.FindAll("[data-task-id='a']"));

        var gate = new TaskCompletionSource<double?>();
        _interop.GanttV3ScrollCenterXGate = gate;
        Task reconcile = Task.CompletedTask;
        await cut.InvokeAsync(() =>
        {
            reconcile = cut.Instance.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(L.Gantt3.Tasks)] = new List<L.GanttTask> { taskB },
                [nameof(L.Gantt3.ViewMode)] = L.GanttViewMode.Month,
                [nameof(L.Gantt3.ShowTreePane)] = false,
            }));
        });
        Assert.False(reconcile.IsCompleted);

        _interop.GanttV3ScrollCenterXGate = null;
        _interop.GanttV3ScrollCenterXToReturn = 0;

        var shiftNext = typeof(L.Gantt3).GetMethod("ShiftToNextAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await cut.InvokeAsync(async () => await (Task)shiftNext.Invoke(cut.Instance, null)!);

        gate.SetResult(0);
        await reconcile;

        // Both the new task set AND the Month mode landed.
        Assert.Empty(cut.FindAll("[data-task-id='a']"));
        Assert.Single(cut.FindAll("[data-task-id='b']"));
        Assert.DoesNotContain("–", cut.Find("span.text-sm.font-medium").TextContent);
    }

    [Fact]
    public async Task Reapply_A_Theme_Direction_Flip_During_A_Suspended_Mode_Change_Still_Lands_The_Mode()
    {
        // The theme path has the same absorb-before-commit hazard as the nav
        // path: a ThemeService direction flip supersedes an in-flight parameter
        // reconcile, so it must carry that reconcile's pending mode forward, not
        // just the direction.
        var task = new L.GanttTask("t1", "Task", D(2026, 1, 10), D(2026, 1, 20));
        var tasks = new List<L.GanttTask> { task };
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, tasks)
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.ShowTreePane, false));
        Assert.Contains("–", cut.Find("span.text-sm.font-medium").TextContent);

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

        _interop.GanttV3ScrollCenterXGate = null;
        _interop.GanttV3ScrollCenterXToReturn = 0;

        var themeService = _ctx.Services.GetRequiredService<IThemeService>();
        await cut.InvokeAsync(async () => await themeService.SetDirectionAsync(LayoutDirection.Rtl));
        await cut.InvokeAsync(() => { }); // pump the fire-and-forget OnThemeChanged continuation

        gate.SetResult(0);
        await reconcile;

        cut.WaitForAssertion(() =>
            Assert.DoesNotContain("–", cut.Find("span.text-sm.font-medium").TextContent));
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
