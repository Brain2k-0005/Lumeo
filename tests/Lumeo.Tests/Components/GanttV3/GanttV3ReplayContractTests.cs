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
/// What a REPLAY owes (Codex PR-382 review round 4) — three findings against the
/// round-3 ownership consolidation, all about the same boundary: a replay is
/// triggered from INSIDE the component (a navigation, a theme flip, a parameter
/// pass that superseded something), not by the consumer handing us parameters,
/// so it must re-execute the pending work faithfully without re-reading things
/// only a genuine parameter pass may read.
///
///  X (P2, Gantt3.razor:499) — Tasks is an IEnumerable, so it may be a
///      single-pass sequence. Re-enumerating it from a navigation drains it: the
///      reconcile then sees an empty task set and clears the chart. Fixed by
///      materializing on parameter passes only and replaying that snapshot.
///      NOTE: pre-existing since round 18-f2 introduced the replay path — see
///      _materializedTasks' own remarks; the consolidation neither added an
///      enumeration site nor changed how often one runs.
///  Y (P1, Gantt3.razor:561) — a parameter pass that says nothing about the mode
///      but DOES change tasks or geometry still commits, and by committing it
///      supersedes a suspended mode reconcile — which then abandons, stranding
///      its intent in Applying forever with the requested mode never applied.
///      Fixed at the rule level: a NON-CLAIMING pass must CARRY a pending
///      intent, so having superseded one it now owes it the same replay a
///      navigation owes.
///  Z (P2, Gantt3.razor:1165) — ShiftAsync re-applies its page step on top of a
///      replayed task-derived range (round 19 finding #3) but left the scroll
///      intent that reconcile emitted describing the UN-shifted range, so the
///      timeline centered one step behind the final viewport and visually
///      cancelled the Previous/Next. Fixed by shifting the emitted target with
///      the range, through the same helper.
/// </summary>
public class GanttV3ReplayContractTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public GanttV3ReplayContractTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<Lumeo.Services.IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static DateTime D(int y, int m, int d) => new(y, m, d);

    private static MethodInfo Method(string name) =>
        typeof(L.Gantt3).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!;

    // Repaints via the PROTECTED ComponentBase.StateHasChanged, never bUnit's
    // parameterless cut.Render() — that overload re-issues SetParametersAsync,
    // which for these tests would re-materialize Tasks (masking X entirely) and
    // re-run the parameter lifecycle (masking Y).
    private static async Task ForceRepaintAsync(IRenderedComponent<L.Gantt3> cut)
    {
        var stateHasChanged = typeof(ComponentBase).GetMethod("StateHasChanged", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await cut.InvokeAsync(() => stateHasChanged.Invoke(cut.Instance, null));
    }

    private static GanttViewModeIntent Intent(IRenderedComponent<L.Gantt3> cut) =>
        (GanttViewModeIntent)typeof(L.Gantt3)
            .GetField("_viewModeIntent", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(cut.Instance)!;

    private static GanttState State(IRenderedComponent<L.Gantt3> cut) =>
        (GanttState)typeof(L.Gantt3)
            .GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(cut.Instance)!;

    private static Task InvokeAsync(IRenderedComponent<L.Gantt3> cut, string method) =>
        cut.InvokeAsync(async () => await (Task)Method(method).Invoke(cut.Instance, null)!);

    // ── Finding X: a single-pass Tasks sequence survives internal actions ────

    /// A sequence that THROWS if anything enumerates it twice — the loud
    /// variant of a consuming enumerable (e.g. a reader-backed iterator).
    private static IEnumerable<L.GanttTask> SinglePass(params L.GanttTask[] tasks)
    {
        var enumerations = 0;
        IEnumerable<L.GanttTask> Iterate()
        {
            if (++enumerations > 1)
                throw new InvalidOperationException("the Tasks parameter was enumerated more than once");
            foreach (var t in tasks) yield return t;
        }
        return Iterate();
    }

    /// A sequence that silently yields NOTHING on a second pass — the quiet,
    /// more damaging variant (a LINQ query over an already-consumed source):
    /// no exception, the chart just empties.
    private static IEnumerable<L.GanttTask> Draining(params L.GanttTask[] tasks)
    {
        var drained = false;
        IEnumerable<L.GanttTask> Iterate()
        {
            if (drained) yield break;
            drained = true;
            foreach (var t in tasks) yield return t;
        }
        return Iterate();
    }

    [Fact]
    public async Task Internal_Actions_Never_Re_Enumerate_A_Single_Pass_Tasks_Sequence()
    {
        var task = new L.GanttTask("t1", "Task", D(2026, 1, 10), D(2026, 1, 20));
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, SinglePass(task))
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.ShowTreePane, false));

        Assert.Single(cut.FindAll("[data-task-id='t1']")); // mount consumed it exactly once

        _interop.GanttV3ScrollCenterXToReturn = 0;

        // Every internal trigger that routes through the replay path. Under the
        // bug the FIRST of these throws out of the navigation.
        await InvokeAsync(cut, "ShiftToNextAsync");
        await InvokeAsync(cut, "ShiftToPreviousAsync");
        await InvokeAsync(cut, "GoToTodayAsync");

        var themeService = _ctx.Services.GetRequiredService<IThemeService>();
        await cut.InvokeAsync(async () => await themeService.SetDirectionAsync(LayoutDirection.Rtl));
        await cut.InvokeAsync(() => { }); // pump the fire-and-forget OnThemeChanged continuation

        await ForceRepaintAsync(cut);
        Assert.Single(cut.FindAll("[data-task-id='t1']"));
    }

    [Fact]
    public async Task A_Navigation_Does_Not_Silently_Empty_A_Draining_Tasks_Sequence()
    {
        var task = new L.GanttTask("t1", "Task", D(2026, 1, 10), D(2026, 1, 20));
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, Draining(task))
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.ShowTreePane, false));

        Assert.Single(cut.FindAll("[data-task-id='t1']"));

        _interop.GanttV3ScrollCenterXToReturn = 0;
        await InvokeAsync(cut, "ShiftToNextAsync");
        await ForceRepaintAsync(cut);

        // Under the bug the replay re-enumerates, gets an empty sequence, and
        // commits it as a genuine populated->empty transition: the chart clears.
        Assert.Single(cut.FindAll("[data-task-id='t1']"));
        Assert.Single(State(cut).Tasks);
    }

    // ── Finding Y: a non-claiming parameter pass carries a pending mode ──────

    [Theory]
    [InlineData("tasks")]
    [InlineData("geometry")]
    public async Task A_Non_Claiming_Parameter_Pass_That_Commits_Still_Lands_A_Pending_Toolbar_Mode(string change)
    {
        var taskA = new L.GanttTask("a", "A", D(2026, 1, 10), D(2026, 1, 20));
        var taskB = new L.GanttTask("b", "B", D(2026, 3, 1), D(2026, 3, 10));
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { taskA })
            .Add(c => c.ViewMode, L.GanttViewMode.Day) // UNCONTROLLED
            .Add(c => c.ShowTreePane, false));

        // The toolbar picks Month; suspend its live-center capture so the intent
        // is still Applying when the parameter pass below arrives.
        var gate = new TaskCompletionSource<double?>();
        _interop.GanttV3ScrollCenterXGate = gate;
        Task toolbarReconcile = Task.CompletedTask;
        await cut.InvokeAsync(() =>
        {
            toolbarReconcile = (Task)Method("HandleViewModeChangedAsync").Invoke(cut.Instance, new object[] { L.GanttViewMode.Month })!;
        });
        Assert.False(toolbarReconcile.IsCompleted, "the toolbar's own reconcile should still be awaiting its capture");
        Assert.Equal(GanttViewModePhase.Applying, Intent(cut).Phase);

        // A parameter pass that says NOTHING about the mode (ViewMode unchanged)
        // but changes something else, so it commits — and by committing it
        // supersedes the suspended toolbar reconcile.
        _interop.GanttV3ScrollCenterXGate = null;
        _interop.GanttV3ScrollCenterXToReturn = 0;
        await cut.InvokeAsync(() => cut.Instance.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(L.Gantt3.Tasks)] = change == "tasks"
                ? new List<L.GanttTask> { taskB }
                : new List<L.GanttTask> { taskA },
            [nameof(L.Gantt3.ViewMode)] = L.GanttViewMode.Day, // unchanged — this pass claims nothing
            [nameof(L.Gantt3.ShowTreePane)] = change == "geometry",
        })));

        gate.SetResult(0);
        await toolbarReconcile; // the superseded toolbar pass abandons cleanly
        await ForceRepaintAsync(cut);

        // The toolbar's Month must have landed anyway — the committing pass owed
        // it a replay. Under the bug it was discarded and the intent stranded.
        Assert.Equal(L.GanttViewMode.Month, State(cut).ViewMode);
        Assert.Equal(GanttViewModePhase.Settled, Intent(cut).Phase);
        var label = cut.Find("span.text-sm.font-medium").TextContent;
        Assert.False(label.Contains('–', StringComparison.Ordinal),
            $"expected Month's \"MMMM yyyy\" label, got \"{label}\" — the pending toolbar mode was discarded by an unrelated parameter update");

        // And the pass's own change landed too (they are not exclusive).
        if (change == "tasks")
        {
            Assert.Empty(cut.FindAll("[data-task-id='a']"));
            Assert.Single(cut.FindAll("[data-task-id='b']"));
        }
    }

    [Fact]
    public async Task A_Carried_Pending_Mode_Is_Measured_Against_The_Viewport_That_Is_Rendered()
    {
        // Codex PR-382 review round 7, P1. Round 4 landed the carry as a SECOND
        // reconcile chained after the pass's own: the first committed the new
        // range and snapshot, then the second immediately captured the live
        // scroll centre — before Blazor had rendered any of it, so it read the
        // OLD DOM and decoded it against the NEW range. Carrying the mode
        // through the pass's single reconcile removes the window entirely: the
        // capture happens before anything commits, exactly as for every other
        // trigger.
        var taskA = new L.GanttTask("a", "A", D(2026, 1, 10), D(2026, 1, 20));
        var taskB = new L.GanttTask("b", "B", D(2026, 3, 1), D(2026, 3, 10));
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { taskA })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.ShowTreePane, false));

        // A toolbar Day -> Month pick, suspended in its capture.
        var gate = new TaskCompletionSource<double?>();
        _interop.GanttV3ScrollCenterXGate = gate;
        Task toolbarReconcile = Task.CompletedTask;
        await cut.InvokeAsync(() =>
        {
            toolbarReconcile = (Task)Method("HandleViewModeChangedAsync").Invoke(cut.Instance, new object[] { L.GanttViewMode.Month })!;
        });
        Assert.False(toolbarReconcile.IsCompleted);

        // A TASKS-ONLY parameter pass interrupts it (ViewMode unchanged), so it
        // carries the pending Month.
        _interop.GanttV3ScrollCenterXGate = null;
        _interop.GanttV3ScrollCenterXToReturn = 0;
        await cut.InvokeAsync(() => cut.Instance.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(L.Gantt3.Tasks)] = new List<L.GanttTask> { taskB },
            [nameof(L.Gantt3.ViewMode)] = L.GanttViewMode.Day,
            [nameof(L.Gantt3.ShowTreePane)] = false,
        })));

        gate.SetResult(0);
        await toolbarReconcile;
        await ForceRepaintAsync(cut);

        // Ground truth: ONE reconcile carrying Month alongside the new tasks is
        // GanttViewportReconciler's canonical tasks+mode decision — the range
        // comes from the NEW tasks under Month padding (PadBefore/PadAfter = 12
        // months around taskB's own Mar 2026), and the captured centre is only
        // the scroll target:  [2025-03-01, 2027-03-01].
        //
        // Chained instead, the first reconcile would commit taskB's DAY-derived
        // range [2026-01-01, 2026-05-09] and the second would then self-centre
        // on a centre decoded against that un-rendered range, landing on
        // [2025-01-01, 2027-01-01] — a whole different viewport.
        Assert.Equal(D(2025, 3, 1), State(cut).VisibleRange.Start);
        Assert.Equal(D(2027, 3, 1), State(cut).VisibleRange.End);
        Assert.Equal(L.GanttViewMode.Month, State(cut).ViewMode);
        Assert.Single(cut.FindAll("[data-task-id='b']"));
    }

    [Fact]
    public async Task A_Parameter_Pass_With_Nothing_To_Do_Still_Leaves_A_Suspended_Mode_Reconcile_Alone()
    {
        // The counterweight to the fix above (round 17 finding #1's contract): a
        // pass that changes NOTHING must remain a total no-op — it claims no
        // generation, so it supersedes nothing and therefore owes no replay. If
        // the fix had been written as "always carry the pending mode here", this
        // pass would claim a generation and abort the very reconcile that is
        // about to succeed on its own.
        var task = new L.GanttTask("t1", "Task", D(2026, 1, 10), D(2026, 1, 20));
        var tasks = new List<L.GanttTask> { task };
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, tasks)
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.ShowTreePane, false)
            .Add(c => c.Class, "initial"));

        var gate = new TaskCompletionSource<double?>();
        _interop.GanttV3ScrollCenterXGate = gate;
        Task toolbarReconcile = Task.CompletedTask;
        await cut.InvokeAsync(() =>
        {
            toolbarReconcile = (Task)Method("HandleViewModeChangedAsync").Invoke(cut.Instance, new object[] { L.GanttViewMode.Month })!;
        });
        Assert.False(toolbarReconcile.IsCompleted);

        // Only Class differs — nothing the snapshot diff looks at. The gate is
        // still armed, so if this pass tried to capture anything it would hang.
        await cut.InvokeAsync(() => cut.Instance.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(L.Gantt3.Tasks)] = tasks,
            [nameof(L.Gantt3.ViewMode)] = L.GanttViewMode.Day,
            [nameof(L.Gantt3.ShowTreePane)] = false,
            [nameof(L.Gantt3.Class)] = "different",
        })));

        // The original reconcile is still the current one and commits on resume.
        gate.SetResult(0);
        await toolbarReconcile;
        await ForceRepaintAsync(cut);

        Assert.Equal(L.GanttViewMode.Month, State(cut).ViewMode);
        Assert.Equal(GanttViewModePhase.Settled, Intent(cut).Phase);
    }

    // ── Finding Z: the emitted scroll target moves with the re-shifted range ─

    [Fact]
    public async Task Round19_Finding3_Range_Reshift_Also_Moves_The_Emitted_Scroll_Target()
    {
        // Same setup as GanttV3CodexRound19Tests' own finding-#3 spec (which
        // pins the RANGE); this pins the one-shot scroll intent that ships with
        // it. A suspended pass carrying both a task replacement and a mode
        // change lands via GanttRangeSource.TaskDerived + CapturedCenter, so the
        // reconcile emits its target against the freshly task-derived range —
        // which ShiftAsync then moves one page step. Target and range describe
        // the same viewport, so they must move together.
        var taskA = new L.GanttTask("a", "A", D(2026, 1, 10), D(2026, 1, 20));
        var taskB = new L.GanttTask("b", "B", D(2026, 3, 1), D(2026, 3, 10));
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { taskA })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.ShowTreePane, false)); // no tree pane => zero leading offset

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

        // The replay's capture reads logical center 0 — i.e. exactly the origin
        // of whatever range is in effect when it runs.
        _interop.GanttV3ScrollCenterXGate = null;
        _interop.GanttV3ScrollCenterXToReturn = 0;

        await InvokeAsync(cut, "ShiftToNextAsync");
        gate.SetResult(0);
        await reconcile;
        await ForceRepaintAsync(cut);

        // Ground truth, step by step:
        //  1. mount range = ComputeInitialRange(Day, taskA)  -> Day padding 60/60
        //     around [2026-01-10, 2026-01-20] = [2025-11-11, 2026-03-21].
        //  2. ShiftAsync applies its Day step FIRST     -> [2025-11-12, 2026-03-22].
        //  3. the replay captures center x=0, decoded against THAT range's own
        //     origin under the OLD (Day) geometry       -> 2025-11-12.
        //  4. it commits the task-derived Month range   -> [2025-03-01, 2027-03-01],
        //     emitting the captured center as its target.
        //  5. round 19 #3 re-applies the page step, now in Month units
        //                                               -> [2025-04-01, 2027-04-01].
        //  6. THIS fix moves the target by the same step -> 2025-12-12.
        var expectedStart = D(2025, 4, 1);
        var expectedEnd = D(2027, 4, 1);
        var expectedTarget = D(2025, 11, 12).AddMonths(1);

        var monthColumnWidth = GanttScale.GetConfig(L.GanttViewMode.Month).ColumnWidth;
        var origin = GanttScale.BuildDateUnits(L.GanttViewMode.Month, expectedStart, expectedEnd)[0];
        var expectedX = GanttScale.DateToPixel(L.GanttViewMode.Month, origin, expectedTarget, monthColumnWidth);

        Assert.Equal(expectedStart, State(cut).VisibleRange.Start);
        Assert.Equal(expectedEnd, State(cut).VisibleRange.End);

        // Under the bug the intent still targets the UN-shifted center
        // (2025-11-12), i.e. exactly one Month column to the left — the page
        // step the user just pressed, silently undone.
        var buggyX = GanttScale.DateToPixel(L.GanttViewMode.Month, origin, D(2025, 11, 12), monthColumnWidth);
        Assert.Equal(monthColumnWidth, expectedX - buggyX, 1);
        Assert.Equal(expectedX, _interop.GanttV3ScrollToXCalls[^1], 1);
    }

    [Fact]
    public async Task A_Navigation_With_Nothing_Pending_Emits_No_Extra_Scroll_Intent()
    {
        // The guard on the fix above: when the replay commits nothing new there
        // is no emitted target to move, and the shift must NOT invent a scroll
        // of its own (GanttScrollTarget.None deliberately leaves the DOM alone).
        var task = new L.GanttTask("t1", "Task", D(2026, 1, 10), D(2026, 1, 20));
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { task })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.ShowTreePane, false));

        _interop.GanttV3ScrollCenterXToReturn = 0;
        var scrollsBefore = _interop.GanttV3ScrollToXCallCount;

        await InvokeAsync(cut, "ShiftToNextAsync");
        await ForceRepaintAsync(cut);

        Assert.Equal(scrollsBefore, _interop.GanttV3ScrollToXCallCount);
    }
}
