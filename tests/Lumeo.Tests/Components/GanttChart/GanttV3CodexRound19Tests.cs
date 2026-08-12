using System.Globalization;
using System.Linq;
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
/// Codex round 19 (PR #382) — four findings against the round-18/18-f2 nav
/// supersession fixes themselves, attached to the "post-merge follow-up" PR
/// (fix/gantt-v3-post-merge-followup) once Codex reviewed that diff:
///
///  #1 (P1, GanttChart.razor line ~487) — a suspended reconcile that originated in
///       HandleViewModeChangedAsync (the TOOLBAR), not OnParametersSetAsync,
///       carried its target mode nowhere a superseding navigation/theme
///       re-apply could see: neither ViewMode (a controlled parameter only
///       echoes the toolbar's pick back AFTER a commit) nor
///       _committedViewModeParam (blind to a not-yet-committed toolbar pick
///       either) reflected it. Fixed: _pendingToolbarMode records the
///       toolbar's own target before its reconcile's capture await, and
///       ReapplyCurrentParametersAsync consults it with priority.
///  #2 (P2, line ~786) — a NoOp reconcile outcome (the incoming parameter
///       already matches what's committed) left _committedViewModeParam
///       behind, so a LATER toolbar selection's own supersession misread the
///       stale reference as "the parent's old value is still pending" and
///       reverted the newer mode. Fixed: advance the discriminator on NoOp
///       too (only Superseded is excluded).
///  #3 (P1, line ~1028) — ShiftAsync applies its shift BEFORE re-applying a
///       suspended pass; when that pass carries a pending task-set change, the
///       replay lands via GanttRangeSource.TaskDerived, which unconditionally
///       re-derives VisibleRange from the NEW tasks' own min/max — silently
///       erasing the shift even though the tasks/mode land successfully.
///       Fixed: re-apply the same shift on top of a TaskDerived commit
///       (detected via _tasksVersion having moved).
///  #4 (P1, line ~1056) — GoToTodayAsync discarded SupersedeReconcileForNavigationAsync's
///       own outcome, so a Today click superseded by a LATER Previous/Next
///       still ran its own final recenter, overwriting the newer navigation's
///       result — reversing the generation guard's newest-action-wins
///       ordering. Fixed: propagate the outcome and return without
///       recentering when the replay was superseded.
/// </summary>
public class GanttV3CodexRound19Tests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public GanttV3CodexRound19Tests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<Lumeo.Services.IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static DateTime D(int y, int m, int d) => new(y, m, d);

    // ── Finding #1: a suspended TOOLBAR reconcile's pending mode survives a superseding nav ──

    [Fact]
    public async Task A_Navigation_Superseding_A_Suspended_Toolbar_Reconcile_Still_Lands_The_Toolbars_Mode()
    {
        var task = new L.GanttTask("t1", "Task", D(2026, 1, 10), D(2026, 1, 20));
        var tasks = new List<L.GanttTask> { task };
        var cut = _ctx.Render<L.GanttChart>(p => p
            .Add(c => c.Tasks, tasks)
            .Add(c => c.ViewMode, L.GanttViewMode.Day) // UNCONTROLLED (no ViewModeChanged)
            .Add(c => c.ShowTreePane, false));

        Assert.Contains("–", cut.Find("span.text-sm.font-medium").TextContent); // mount: Day range

        // Toolbar picks Month; suspend its own live-center capture — this
        // reconcile originates in HandleViewModeChangedAsync, NOT a parameter
        // pass (the distinction Finding #1 is specifically about).
        var gate = new TaskCompletionSource<double?>();
        _interop.GanttV3ScrollCenterXGate = gate;

        var handleViewModeChanged = typeof(L.GanttChart).GetMethod(
            "HandleViewModeChangedAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Task toolbarReconcile = Task.CompletedTask;
        await cut.InvokeAsync(() =>
        {
            toolbarReconcile = (Task)handleViewModeChanged.Invoke(cut.Instance, new object[] { L.GanttViewMode.Month })!;
        });
        Assert.False(toolbarReconcile.IsCompleted, "the toolbar's own mode reconcile should still be awaiting its capture");

        // Un-gate subsequent captures so the navigation's OWN re-apply can
        // complete; the suspended toolbar pass keeps awaiting the ORIGINAL gate.
        _interop.GanttV3ScrollCenterXGate = null;
        _interop.GanttV3ScrollCenterXToReturn = 0;

        var shiftNext = typeof(L.GanttChart).GetMethod("ShiftToNextAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await cut.InvokeAsync(async () => await (Task)shiftNext.Invoke(cut.Instance, null)!);

        // Resume the superseded toolbar reconcile — it abandons cleanly.
        gate.SetResult(0);
        await toolbarReconcile;

        // Every mutation above was reached via raw reflection calls, not
        // Blazor's own event/parameter dispatch — neither triggers the
        // automatic post-handler re-render ComponentBase normally provides, so
        // force one before inspecting the DOM.
        cut.Render();

        // The toolbar's Month selection LANDED after the navigation superseded
        // it (not dropped): the label is Month's "MMMM yyyy", not a Day/Week
        // "… – …" range. Under the pre-fix behavior, neither ViewMode nor
        // _committedViewModeParam carried the toolbar's target, so the re-apply
        // silently reverted to Day.
        Assert.DoesNotContain("–", cut.Find("span.text-sm.font-medium").TextContent);
    }

    // ── Finding #2: a NoOp parameter echo still advances the committed discriminator ──

    [Fact]
    public async Task A_NoOp_Parameter_Echo_Of_An_Already_Applied_Toolbar_Mode_Does_Not_Resurrect_The_Old_Value_Later()
    {
        var task = new L.GanttTask("t1", "Task", D(2026, 1, 10), D(2026, 1, 20));
        var tasks = new List<L.GanttTask> { task };
        var cut = _ctx.Render<L.GanttChart>(p => p
            .Add(c => c.Tasks, tasks)
            .Add(c => c.ViewMode, L.GanttViewMode.Day) // UNCONTROLLED
            .Add(c => c.ShowTreePane, false));

        var handleViewModeChanged = typeof(L.GanttChart).GetMethod(
            "HandleViewModeChangedAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;

        // Toolbar independently selects Month — commits cleanly (no supersession).
        // A raw reflection call bypasses ComponentBase's automatic post-handler
        // re-render, so force one before inspecting the DOM (here and below).
        await cut.InvokeAsync(async () => await (Task)handleViewModeChanged.Invoke(cut.Instance, new object[] { L.GanttViewMode.Month })!);
        cut.Render();
        Assert.DoesNotContain("–", cut.Find("span.text-sm.font-medium").TextContent); // Month landed

        // The (uncontrolled) parent now pushes ViewMode=Month too — a value
        // ALREADY present in _state via the toolbar. ReconcileAsync decides
        // this is a NoOp (nothing actually changed).
        await cut.InvokeAsync(() => cut.Instance.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(L.GanttChart.Tasks)] = tasks,
            [nameof(L.GanttChart.ViewMode)] = L.GanttViewMode.Month,
            [nameof(L.GanttChart.ShowTreePane)] = false,
        })));

        // A LATER toolbar selection — Year — commits cleanly too.
        await cut.InvokeAsync(async () => await (Task)handleViewModeChanged.Invoke(cut.Instance, new object[] { L.GanttViewMode.Year })!);

        // A plain navigation (nothing suspended, nothing pending) must not
        // revert to the parent's stale Month parameter — it must preserve the
        // LATER Year selection. Year's own PeriodLabel format ("{y1}–{y2}") has
        // no letters at all, unlike Month's ("MMMM yyyy") or Day's own
        // "MMM d, yyyy – MMM d, yyyy" — a format-shape check that doesn't
        // require predicting the exact captured-center date.
        var shiftNext = typeof(L.GanttChart).GetMethod("ShiftToNextAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await cut.InvokeAsync(async () => await (Task)shiftNext.Invoke(cut.Instance, null)!);
        cut.Render();

        var label = cut.Find("span.text-sm.font-medium").TextContent;
        Assert.False(label.Any(char.IsLetter),
            $"expected Year mode's letter-free \"{{y1}}–{{y2}}\" label, got \"{label}\" — " +
            "the stale _committedViewModeParam reverted the later toolbar selection back to Month");
    }

    // ── Finding #3: ShiftAsync re-applies its shift on top of a replayed task-derived range ──

    [Fact]
    public async Task ShiftAsync_Reapplies_The_Shift_On_Top_Of_A_Replayed_Task_Derived_Range()
    {
        var taskA = new L.GanttTask("a", "A", D(2026, 1, 10), D(2026, 1, 20));
        var taskB = new L.GanttTask("b", "B", D(2026, 3, 1), D(2026, 3, 10)); // a different, non-empty set
        var cut = _ctx.Render<L.GanttChart>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { taskA })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.ShowTreePane, false));
        Assert.Single(cut.FindAll("[data-task-id='a']"));

        // Suspend a parameter pass carrying BOTH a task-set change AND a mode
        // change (Day -> Month) — the exact "tasks plus mode" shape the
        // finding itself calls out.
        var gate = new TaskCompletionSource<double?>();
        _interop.GanttV3ScrollCenterXGate = gate;
        Task reconcile = Task.CompletedTask;
        await cut.InvokeAsync(() =>
        {
            reconcile = cut.Instance.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(L.GanttChart.Tasks)] = new List<L.GanttTask> { taskB },
                [nameof(L.GanttChart.ViewMode)] = L.GanttViewMode.Month,
                [nameof(L.GanttChart.ShowTreePane)] = false,
            }));
        });
        Assert.False(reconcile.IsCompleted);

        _interop.GanttV3ScrollCenterXGate = null;
        _interop.GanttV3ScrollCenterXToReturn = 0;

        var shiftNext = typeof(L.GanttChart).GetMethod("ShiftToNextAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await cut.InvokeAsync(async () => await (Task)shiftNext.Invoke(cut.Instance, null)!);

        gate.SetResult(0);
        await reconcile;

        // Both the new task set AND the Month mode landed (round 18-f2's own
        // guarantee, unaffected by this fix).
        Assert.Empty(cut.FindAll("[data-task-id='a']"));
        Assert.Single(cut.FindAll("[data-task-id='b']"));

        // Ground truth: ComputeInitialRange's Month-mode padding (PadBefore/
        // PadAfter = 12 months) around taskB's own Mar 2026 min/max gives
        // [2025-03-01, 2027-03-01) — then the Next click's own shift (Step = 1
        // month) must land on TOP of that freshly task-derived range, not be
        // silently discarded by it: [2025-04-01, 2027-04-01).
        var expectedStart = new DateTime(2025, 4, 1);
        var expectedLabel = expectedStart.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
        Assert.Equal(expectedLabel, cut.Find("span.text-sm.font-medium").TextContent);
    }

    // ── Finding #4: a superseded Today click bails out before its final recenter ──

    [Fact]
    public async Task GoToTodayAsync_Superseded_By_A_Later_Shift_Does_Not_Overwrite_The_Shifts_Result()
    {
        var task = new L.GanttTask("t1", "Task", D(2026, 1, 10), D(2026, 1, 20));
        var tasks = new List<L.GanttTask> { task };
        var cut = _ctx.Render<L.GanttChart>(p => p
            .Add(c => c.Tasks, tasks)
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.ShowTreePane, false));

        _interop.GanttV3LocalDateToReturn = "2026-07-27"; // pinned, deterministic "today"

        // Suspend a PARAMETER pass that only flips ShowTreePane (a pure
        // geometry change — Range stays Keep, so its own commit, once it
        // eventually lands, never touches VisibleRange at all: this isolates
        // Finding #4 from Finding #3's separate TaskDerived-clobbers-shift fix).
        var gate1 = new TaskCompletionSource<double?>();
        _interop.GanttV3ScrollCenterXGate = gate1;
        Task pushReconcile = Task.CompletedTask;
        await cut.InvokeAsync(() =>
        {
            pushReconcile = cut.Instance.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(L.GanttChart.Tasks)] = tasks,
                [nameof(L.GanttChart.ViewMode)] = L.GanttViewMode.Day,
                [nameof(L.GanttChart.ShowTreePane)] = true,
            }));
        });
        Assert.False(pushReconcile.IsCompleted);

        // Click Today — its OWN re-apply also sees ShowTreePane's geometry
        // change (still unresolved: _lastSnapshot never advanced past mount)
        // and needs a FRESH capture too. Suspend THAT one on a second gate.
        var gate2 = new TaskCompletionSource<double?>();
        _interop.GanttV3ScrollCenterXGate = gate2;

        var goToToday = typeof(L.GanttChart).GetMethod("GoToTodayAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Task todayTask = Task.CompletedTask;
        await cut.InvokeAsync(() =>
        {
            todayTask = (Task)goToToday.Invoke(cut.Instance, null)!;
        });
        Assert.False(todayTask.IsCompleted, "Today's own re-apply should still be awaiting its live-center capture");

        // A LATER Next click supersedes Today's still-suspended re-apply. Its
        // OWN re-apply (same pending geometry change) gets an immediately-
        // resolving capture, so it fully commits before Today's ever resumes.
        _interop.GanttV3ScrollCenterXGate = null;
        _interop.GanttV3ScrollCenterXToReturn = 0;
        var shiftNext = typeof(L.GanttChart).GetMethod("ShiftToNextAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await cut.InvokeAsync(async () => await (Task)shiftNext.Invoke(cut.Instance, null)!);

        // Clean up the original ShowTreePane-push suspension (harmless no-op:
        // superseded long ago).
        gate1.SetResult(0);
        await pushReconcile;

        // Resume Today's own suspended capture — under the bug, GoToTodayAsync
        // would ignore its own Superseded outcome and barrel into its final
        // recenter (today-centered window), clobbering the Next click's result.
        gate2.SetResult(999);
        await todayTask;
        // Force a re-render — GoToTodayAsync was reached via raw reflection,
        // not Blazor's own dispatch, so nothing else triggers one automatically
        // (the EARLIER SetParametersAsync completion at line ~282 already
        // painted a frame, but that predates this resumption entirely — without
        // this, the assertion below would trivially pass against that STALE
        // frame regardless of what GoToTodayAsync's tail just did).
        cut.Render();

        // Ground truth: ComputeInitialRange's Day-mode padding (PadBefore/
        // PadAfter = 60 days) around taskA's own Jan 2026 min/max gives
        // [2025-11-11, 2026-03-21) — the Next click's own shift (Step = 1 day)
        // must be the FINAL state: [2025-11-12, 2026-03-22). A today-centered
        // (2026-07-27) window would land somewhere entirely different.
        var expectedStart = new DateTime(2025, 11, 12);
        var expectedEnd = new DateTime(2026, 3, 22);
        var expectedLabel =
            $"{expectedStart.ToString("MMM d, yyyy", CultureInfo.CurrentCulture)} – " +
            $"{expectedEnd.ToString("MMM d, yyyy", CultureInfo.CurrentCulture)}";
        Assert.Equal(expectedLabel, cut.Find("span.text-sm.font-medium").TextContent);
    }
}
