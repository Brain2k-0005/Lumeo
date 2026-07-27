using System.Linq;
using System.Reflection;
using Bunit;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.GanttV3;

/// <summary>
/// Codex round 20 (PR #382) — two P1 findings against round 19's own fixes,
/// posted on Codex's re-review of that push (88f6023d):
///
///  A (P1, Gantt3.razor line ~526) — "Recheck supersession after awaiting the
///       mode callback". Round 19's own fix for Finding #1 added an
///       `await ViewModeChanged.InvokeAsync(...)` inside
///       ReapplyCurrentParametersAsync, invoked AFTER a reconcile already
///       committed. For a CONTROLLED consumer, invoking that callback can
///       yield arbitrarily long (the parent's own handler may itself be
///       async) — a LATER Previous/Next click landing during that yield
///       claims a newer generation and commits, but ReapplyCurrentParametersAsync
///       still returned the ALREADY-COMPUTED (now stale) Committed outcome, so
///       a caller with its own post-reconcile tail (GoToTodayAsync's recenter)
///       ran it on top of the newer commit, overwriting it.
///       Fixed: capture the generation right after this call's own commit,
///       and re-check it immediately after the callback await resolves —
///       downgrading to Superseded if something newer landed in the meantime.
///
///  B (P1, Gantt3.razor line ~501) — "Discard parameters superseded by a
///       toolbar commit". With an UNCONTROLLED component, if a parent's
///       Day-&gt;Month parameter reconcile is suspended in its live-center
///       capture and the user then picks Year from the toolbar, the toolbar's
///       reconcile supersedes the parameter pass and commits Year — but
///       _committedViewModeParam was never touched by
///       HandleViewModeChangedAsync, so it stayed at the OLD "Day" baseline.
///       A later navigation's `ViewMode != _committedViewModeParam` diff then
///       saw the still-live "Month" parameter as newly different from "Day"
///       and re-applied it, silently reverting the toolbar's Year pick.
///       Fixed (consolidated, not a fourth ad hoc patch): every reconcile
///       trigger that reaches a non-superseded outcome now calls the SAME
///       MarkViewModeParamAccountedFor() helper, so a toolbar commit retires
///       the parameter's stale pending value exactly as a parameter commit
///       already retired a stale TOOLBAR pending value (round 19's own
///       Finding #2).
/// </summary>
public class GanttV3CodexRound20Tests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public GanttV3CodexRound20Tests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<Lumeo.Services.IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static DateTime D(int y, int m, int d) => new(y, m, d);

    // Forces a repaint via the PROTECTED ComponentBase.StateHasChanged (reflection),
    // NOT bUnit's own parameterless cut.Render() — that overload re-issues
    // SetParametersAsync with the SAME ParameterView, which re-invokes
    // OnParametersSetAsync and, for an UNCONTROLLED component, can "helpfully"
    // advance _committedViewModeParam (via that method's own, unrelated,
    // already-correct NoOp-handling — round 19's own Finding #2 fix) BEFORE this
    // test ever gets to observe the STALE value Finding B is specifically about.
    // A real toolbar click's own automatic post-handler re-render never re-pushes
    // parameters like that — StateHasChanged alone is the faithful equivalent,
    // and the only one that doesn't mask this class of bug.
    private static async Task ForceRepaintAsync(IRenderedComponent<L.Gantt3> cut)
    {
        var stateHasChanged = typeof(ComponentBase).GetMethod("StateHasChanged", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await cut.InvokeAsync(() => stateHasChanged.Invoke(cut.Instance, null));
    }

    // ── Finding A: a stale Committed outcome must not survive a callback-await race ──

    [Fact]
    public async Task GoToTodayAsync_Rechecks_Supersession_After_Awaiting_A_Controlled_Mode_Callback()
    {
        var task = new L.GanttTask("t1", "Task", D(2026, 1, 10), D(2026, 1, 20));
        var tasks = new List<L.GanttTask> { task };
        var callbackGate = new TaskCompletionSource();
        var viewModeChangedCalls = new List<L.GanttViewMode>();

        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, tasks)
            .Add(c => c.ViewMode, L.GanttViewMode.Day) // CONTROLLED
            .Add(c => c.ViewModeChanged, EventCallback.Factory.Create<L.GanttViewMode>(this, async mode =>
            {
                viewModeChangedCalls.Add(mode);
                await callbackGate.Task; // simulates a parent whose OWN handler is itself async
            }))
            .Add(c => c.ShowTreePane, false));

        var handleViewModeChanged = typeof(L.Gantt3).GetMethod("HandleViewModeChangedAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var goToToday = typeof(L.Gantt3).GetMethod("GoToTodayAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var shiftNext = typeof(L.Gantt3).GetMethod("ShiftToNextAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;

        // Toolbar picks Month; suspend its own live-center capture.
        var toolbarGate = new TaskCompletionSource<double?>();
        _interop.GanttV3ScrollCenterXGate = toolbarGate;
        Task toolbarReconcile = Task.CompletedTask;
        await cut.InvokeAsync(() =>
        {
            toolbarReconcile = (Task)handleViewModeChanged.Invoke(cut.Instance, new object[] { L.GanttViewMode.Month })!;
        });
        Assert.False(toolbarReconcile.IsCompleted, "the toolbar's own mode reconcile should still be awaiting its capture");

        // Today supersedes the stuck toolbar reconcile and replays Month — its
        // OWN capture resolves immediately, so the reconcile fully COMMITS —
        // but the controlled ViewModeChanged echo that follows then yields on
        // callbackGate, so GoToTodayAsync's own Task is still incomplete.
        _interop.GanttV3ScrollCenterXGate = null;
        _interop.GanttV3ScrollCenterXToReturn = 0;
        Task todayTask = Task.CompletedTask;
        await cut.InvokeAsync(() =>
        {
            todayTask = (Task)goToToday.Invoke(cut.Instance, null)!;
        });
        Assert.Single(viewModeChangedCalls); // proves the replay committed BEFORE this await
        Assert.False(todayTask.IsCompleted, "GoToTodayAsync should still be awaiting its own ViewModeChanged echo");

        // A LATER Next click claims a newer generation and fully commits its
        // own state while the echo above is still suspended.
        await cut.InvokeAsync(async () => await (Task)shiftNext.Invoke(cut.Instance, null)!);

        // Clean up the long-superseded original toolbar suspension (harmless).
        toolbarGate.SetResult(0);
        await toolbarReconcile;

        await ForceRepaintAsync(cut);
        var afterShiftLabel = cut.Find("span.text-sm.font-medium").TextContent;

        // Resume the stuck echo — under the bug, GoToTodayAsync would still
        // see its own (now stale) Committed outcome and barrel into its final
        // recenter, overwriting the Next click's result.
        callbackGate.SetResult();
        await todayTask;
        await ForceRepaintAsync(cut);

        Assert.Equal(afterShiftLabel, cut.Find("span.text-sm.font-medium").TextContent);
    }

    // ── Finding B: a toolbar commit retires a superseded parameter's stale pending value ──

    [Fact]
    public async Task A_Toolbar_Commit_Superseding_A_Suspended_Parameter_Reconcile_Is_Not_Reverted_By_A_Later_Navigation()
    {
        var task = new L.GanttTask("t1", "Task", D(2026, 1, 10), D(2026, 1, 20));
        var tasks = new List<L.GanttTask> { task };
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, tasks)
            .Add(c => c.ViewMode, L.GanttViewMode.Day) // UNCONTROLLED
            .Add(c => c.ShowTreePane, false));

        // A parent Day -> Month parameter reconcile is suspended mid-capture.
        var paramGate = new TaskCompletionSource<double?>();
        _interop.GanttV3ScrollCenterXGate = paramGate;
        Task paramReconcile = Task.CompletedTask;
        await cut.InvokeAsync(() =>
        {
            paramReconcile = cut.Instance.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(L.Gantt3.Tasks)] = tasks,
                [nameof(L.Gantt3.ViewMode)] = L.GanttViewMode.Month,
                [nameof(L.Gantt3.ShowTreePane)] = false,
            }));
        });
        Assert.False(paramReconcile.IsCompleted, "the parameter reconcile should still be awaiting its capture");

        // WHILE it's still suspended, the toolbar picks Year — its own capture
        // resolves immediately, superseding and fully committing over the
        // stuck Month parameter reconcile.
        var handleViewModeChanged = typeof(L.Gantt3).GetMethod("HandleViewModeChangedAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        _interop.GanttV3ScrollCenterXGate = null;
        _interop.GanttV3ScrollCenterXToReturn = 0;
        await cut.InvokeAsync(async () => await (Task)handleViewModeChanged.Invoke(cut.Instance, new object[] { L.GanttViewMode.Year })!);
        await ForceRepaintAsync(cut);

        // Sanity: Year landed (its own letter-free "{y1}–{y2}" label format).
        Assert.DoesNotContain(cut.Find("span.text-sm.font-medium").TextContent, char.IsLetter);

        // Clean up the long-superseded parameter suspension (harmless no-op).
        paramGate.SetResult(0);
        await paramReconcile;

        // A plain navigation — nothing else pending — must not resurrect the
        // parent's abandoned "Month" parameter value. Under the bug,
        // _committedViewModeParam was never advanced by the toolbar's own
        // commit above, so this comparison would still see the live ViewMode
        // parameter ("Month") as different from the stale "Day" baseline and
        // re-apply it, reverting Year.
        var shiftNext = typeof(L.Gantt3).GetMethod("ShiftToNextAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await cut.InvokeAsync(async () => await (Task)shiftNext.Invoke(cut.Instance, null)!);
        await ForceRepaintAsync(cut);

        var label = cut.Find("span.text-sm.font-medium").TextContent;
        Assert.False(label.Any(char.IsLetter),
            $"expected Year mode's letter-free \"{{y1}}–{{y2}}\" label, got \"{label}\" — " +
            "the stale _committedViewModeParam resurrected the superseded parameter's Month value");
    }
}
