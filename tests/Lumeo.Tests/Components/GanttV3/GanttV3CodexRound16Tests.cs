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
/// Codex round 16 (PR #379, feat/gantt-v3) — 6 findings (2 P1 + 4 P2), the
/// async-concurrency tail continuing from round 15's own generation guard.
///
///  #1 (P1) — ThemeService.OnThemeChanged fires for ANY theme change, not
///       just a direction flip. A pure color-mode notification (direction
///       unchanged) still unconditionally called ReconcileAsync, which claims
///       a generation before deciding anything needs to change — aborting a
///       genuinely in-flight, direction-relevant reconcile with nothing
///       useful replacing it.
///  #2 (P1) — a superseded reconcile's OWN caller (HandleViewModeChangedAsync's
///       ViewModeChanged echo; OnThemeChanged's StateHasChanged) kept firing
///       its side effects even when ReconcileAsync itself aborted — the
///       generation guard only ever covered the commit and scroll intent
///       INSIDE ReconcileAsync, not what callers do with its result.
///  #3 (P2) — the round-15 row @key used one flat string namespace for both
///       task rows (the task's own Id) and synthetic group-header rows
///       ("group-{index}") — a task whose own Id happened to literally BE
///       "group-0" collided with a real group header at that index.
///  #4 (P2) — a nonempty->nonempty task replacement into a totally disjoint
///       date range, with nothing else changing, used to leave the DOM's own
///       untouched scroll position in place (target=None) — meaningless once
///       the range itself moved somewhere unrelated.
///  #5 (P2) — the ThemeService-driven capture read the scroll pane's live
///       center using whatever RTL convention getComputedStyle(el).direction
///       reported AT THAT MOMENT — which can already reflect the NEW
///       direction by the time this capture runs, since ThemeService's own
///       flip mutates the DOM synchronously, independent of Blazor's render
///       pipeline (unlike a DirectionProvider-cascading-parameter change).
///  #6 (P2) — GanttBar's key-handler interop registration could still be
///       resolving when virtualization disposes the bar (scrolled out of
///       view), leaking the JS-side registration forever since DisposeAsync
///       had already run (and returned early, seeing nothing registered yet)
///       by the time the register call's own continuation resumes.
///
/// See docs/superpowers/gantt-v3-cx16-report.md for the full writeup.
/// </summary>
public class GanttV3CodexRound16Tests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public GanttV3CodexRound16Tests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<Lumeo.Services.IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static DateTime D(int y, int m, int d) => new(y, m, d);

    // ── Finding #1: a no-op theme notification must not abort an in-flight reconcile ──

    [Fact]
    public async Task Finding1_A_Color_Mode_Only_Theme_Notification_Does_Not_Abort_A_Suspended_Reconcile()
    {
        var task = new L.GanttTask("t1", "Task", D(2026, 1, 1), D(2026, 1, 10));
        var tasks = new List<L.GanttTask> { task };
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, tasks)
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.ShowTreePane, false));

        // Baseline: mount's own firstRender already issued one scroll-to-X
        // call (centering on Today) before any of the below.
        var scrollsAtMount = _interop.GanttV3ScrollToXCallCount;

        // Suspend a genuine, direction-relevant reconcile (a mode switch) mid-capture.
        var gate = new TaskCompletionSource<double?>();
        _interop.GanttV3ScrollCenterXGate = gate;

        Task reconcile = Task.CompletedTask;
        await cut.InvokeAsync(() =>
        {
            reconcile = cut.Instance.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(L.Gantt3.Tasks)] = tasks,
                [nameof(L.Gantt3.ViewMode)] = L.GanttViewMode.Week,
                [nameof(L.Gantt3.ShowTreePane)] = false,
            }));
        });
        Assert.False(reconcile.IsCompleted, "the mode-switch reconcile should still be awaiting its live-center capture");

        // A pure color-mode toggle — CurrentDirection is untouched by this.
        // OnThemeChanged's own handler dispatches its reconcile attempt via a
        // fire-and-forget InvokeAsync (matching the real component's own
        // code) — running the trigger itself through cut.InvokeAsync, plus an
        // explicit empty pump on the SAME renderer dispatcher right after,
        // forces that queued continuation to actually run to completion (the
        // color-mode-only path never needs to await an interop capture
        // itself, so it always resolves synchronously once it starts) before
        // this test inspects the outcome below.
        var themeService = _ctx.Services.GetRequiredService<Lumeo.Services.IThemeService>();
        await cut.InvokeAsync(async () => await themeService.SetModeAsync(Lumeo.Services.ThemeMode.Dark));
        await cut.InvokeAsync(() => { });

        // Resume the suspended reconcile. Under the bug, the no-op theme
        // notification above would have already claimed a newer generation
        // (despite doing nothing itself), so this would find itself
        // superseded and abandon its own commit entirely.
        gate.SetResult(0);
        await reconcile;

        // Simplest, most direct proof the mode switch actually committed: it
        // must have requested its own recenter (SelfCenteredOnCapture always
        // emits CapturedCenter) — a NEW scroll call beyond the mount baseline.
        Assert.True(_interop.GanttV3ScrollToXCallCount > scrollsAtMount,
            "the mode-switch reconcile must have committed and emitted its own recenter, not been silently aborted by the no-op theme notification");
    }

    // ── Finding #2: a superseded reconcile's caller must suppress its own side effects too ──

    [Fact]
    public async Task Finding2_A_Superseded_Toolbar_Reconcile_Fires_No_ViewModeChanged_Callback()
    {
        var task = new L.GanttTask("t1", "Task", D(2026, 1, 1), D(2026, 1, 10));
        var tasks = new List<L.GanttTask> { task };
        var viewModeChangedCalls = new List<L.GanttViewMode>();

        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, tasks)
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.ViewModeChanged, EventCallback.Factory.Create<L.GanttViewMode>(this, m => viewModeChangedCalls.Add(m)))
            .Add(c => c.ShowTreePane, false));

        var handleViewModeChanged = typeof(L.Gantt3).GetMethod(
            "HandleViewModeChangedAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;

        // Suspend a TOOLBAR-driven reconcile (Day -> Week) mid-capture.
        var gate = new TaskCompletionSource<double?>();
        _interop.GanttV3ScrollCenterXGate = gate;

        Task toolbarReconcile = Task.CompletedTask;
        await cut.InvokeAsync(() =>
        {
            toolbarReconcile = (Task)handleViewModeChanged.Invoke(cut.Instance, new object[] { L.GanttViewMode.Week })!;
        });
        Assert.False(toolbarReconcile.IsCompleted, "the toolbar reconcile should still be awaiting its live-center capture");

        // A PARENT-driven update (Day -> Month) supersedes it while it's stuck,
        // completing fully before the toolbar reconcile resumes.
        _interop.GanttV3ScrollCenterXGate = null;
        _interop.GanttV3ScrollCenterXToReturn = 2 * GanttScale.GetConfig(L.GanttViewMode.Day).ColumnWidth;
        await cut.InvokeAsync(async () =>
        {
            await cut.Instance.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(L.Gantt3.Tasks)] = tasks,
                [nameof(L.Gantt3.ViewMode)] = L.GanttViewMode.Month,
                [nameof(L.Gantt3.ViewModeChanged)] = EventCallback.Factory.Create<L.GanttViewMode>(this, m => viewModeChangedCalls.Add(m)),
                [nameof(L.Gantt3.ShowTreePane)] = false,
            }));
        });

        // Resume the superseded toolbar reconcile.
        gate.SetResult(0);
        await toolbarReconcile;

        // The superseded reconcile's OWN callback (echoing "Week" up) must
        // never fire — under the bug it would, misreporting a mode that was
        // never actually applied (the parent update committed Month instead).
        Assert.DoesNotContain(L.GanttViewMode.Week, viewModeChangedCalls);
        // The parent-driven update itself doesn't go through
        // HandleViewModeChangedAsync (it arrives via OnParametersSetAsync),
        // so ViewModeChanged is never invoked for it either — the callback
        // list should be empty entirely.
        Assert.Empty(viewModeChangedCalls);
    }

    // ── Finding #3: a task id matching the synthetic group-row key pattern must not collide ──

    [Fact]
    public void Finding3_A_Task_Whose_Id_Matches_The_Group_Header_Key_Pattern_Renders_Without_A_Key_Collision()
    {
        // "group-0" is exactly the OLD (pre-round-16) synthetic fallback key a
        // group-header row at index 0 used to get ($"group-{row.RowIndex}") —
        // a task whose own Id is literally that string used to collide with a
        // real group header landing at the same row index.
        var groupTask = new L.GanttTask("group-0", "Task With A Group-Like Id", D(2026, 1, 1), D(2026, 1, 5));
        var otherTask = new L.GanttTask("t2", "Other", D(2026, 2, 1), D(2026, 2, 5));

        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { groupTask, otherTask })
            .Add(c => c.GroupBy, (Func<L.GanttTask, string>)(_ => "G1"))
            .Add(c => c.ViewMode, L.GanttViewMode.Day));

        // Reaching this line at all (no duplicate-key RenderTreeDiffBuilder
        // exception) is half the proof for the INITIAL render.
        Assert.Single(cut.FindAll("[data-task-id='group-0']"));
        Assert.Single(cut.FindAll("[data-task-id='t2']"));

        // Re-render (same data) to exercise the DIFF path too — a duplicate-key
        // exception specifically fires during RenderTreeDiffBuilder's diff
        // against the PREVIOUS tree, which a first mount doesn't necessarily
        // exercise the same way a subsequent render does.
        cut.Render(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { groupTask, otherTask })
            .Add(c => c.GroupBy, (Func<L.GanttTask, string>)(_ => "G1"))
            .Add(c => c.ViewMode, L.GanttViewMode.Day));

        Assert.Single(cut.FindAll("[data-task-id='group-0']"));
        Assert.Single(cut.FindAll("[data-task-id='t2']"));
    }

    // ── Finding #4: a disjoint task-range replacement recenters; an overlapping one doesn't ──

    [Fact]
    public void Finding4_A_Disjoint_Nonempty_To_Nonempty_Task_Replacement_Recenters_Onto_The_New_Tasks()
    {
        var taskA = new L.GanttTask("a1", "A", D(2010, 1, 1), D(2010, 1, 5));   // far in the past
        var taskB = new L.GanttTask("b1", "B", D(2026, 6, 1), D(2026, 6, 5));   // wildly disjoint from A's own (padded) range

        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { taskA })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.ShowTreePane, false));

        var scrollsBefore = _interop.GanttV3ScrollToXCallCount;

        // Replace with a different, non-empty, DISJOINT task set — nothing
        // else (ViewMode/ShowTreePane) changes in the same render.
        cut.Render(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { taskB })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.ShowTreePane, false));

        Assert.True(_interop.GanttV3ScrollToXCallCount > scrollsBefore,
            "a disjoint task replacement must recenter — the DOM's old scroll position is meaningless under the brand-new range");

        // Ground truth: ComputeInitialRange's non-empty branch (taskB's own
        // min/max, Day-mode padding), then Today-or-midpoint exactly like an
        // emptiness transition already resolves it.
        var cfg = GanttScale.GetConfig(L.GanttViewMode.Day);
        var rangeStart = D(2026, 6, 1).AddDays(-cfg.PadBefore * cfg.Step);
        var rangeEnd = D(2026, 6, 5).AddDays(cfg.PadAfter * cfg.Step);
        var todayInRange = DateTime.Today >= rangeStart && DateTime.Today <= rangeEnd;
        var expectedTarget = todayInRange
            ? DateTime.Today
            : rangeStart + new TimeSpan((rangeEnd - rangeStart).Ticks / 2);
        var origin = GanttScale.BuildDateUnits(L.GanttViewMode.Day, rangeStart, rangeEnd)[0];
        var expectedX = GanttScale.DateToPixel(L.GanttViewMode.Day, origin, expectedTarget, cfg.ColumnWidth);
        Assert.Equal(expectedX, _interop.GanttV3ScrollToXCalls[^1], 1);
    }

    [Fact]
    public void Finding4_An_Overlapping_Nonempty_To_Nonempty_Task_Replacement_Does_Not_Recenter()
    {
        var taskA = new L.GanttTask("a1", "A", D(2026, 1, 1), D(2026, 1, 5));
        var taskB = new L.GanttTask("b1", "B", D(2026, 1, 3), D(2026, 1, 10)); // overlaps A's own padded range comfortably

        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { taskA })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.ShowTreePane, false));

        var scrollsBefore = _interop.GanttV3ScrollToXCallCount;

        cut.Render(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { taskB })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.ShowTreePane, false));

        // Overlapping replacement: the pre-round-16 "None" behavior is
        // unaffected — the new range still overlaps whatever the DOM's own
        // untouched scroll position was meaningfully showing.
        Assert.Equal(scrollsBefore, _interop.GanttV3ScrollToXCallCount);
    }

    // ── Finding #4 (pure decision level, mirroring GanttV3CodexRound14Tests' own style) ──

    private static GanttViewportSnapshot Snap(
        int tasksVersion = 1,
        bool renderableEmpty = false,
        L.GanttViewMode mode = L.GanttViewMode.Day,
        int columnWidth = 38,
        bool showTreePane = false,
        LayoutDirection direction = LayoutDirection.Ltr) =>
        new(tasksVersion, renderableEmpty, mode, columnWidth, showTreePane, direction);

    [Fact]
    public void Decide_Disjoint_Task_Range_With_Unchanged_Params_Targets_TodayOrMidpoint()
    {
        var prev = Snap(tasksVersion: 1);
        var next = Snap(tasksVersion: 2);
        var d = GanttViewportReconciler.Decide(prev, next, taskRangeDisjoint: true);
        Assert.Equal(new GanttViewportDecision(false, GanttRangeSource.TaskDerived, GanttScrollTarget.TodayOrMidpoint), d);
    }

    [Fact]
    public void Decide_Disjoint_Task_Range_Combined_With_A_Mode_Change_Still_Preserves_The_Captured_Center()
    {
        // Round 12/14's own case-4 behavior must NOT be overridden by a
        // disjoint task range — a combined tasks+mode change deliberately
        // keeps preserving continuity, never resets to Today just because
        // the new tasks happen to be far away.
        var prev = Snap(tasksVersion: 1, mode: L.GanttViewMode.Day);
        var next = Snap(tasksVersion: 2, mode: L.GanttViewMode.Week);
        var d = GanttViewportReconciler.Decide(prev, next, taskRangeDisjoint: true);
        Assert.Equal(new GanttViewportDecision(true, GanttRangeSource.TaskDerived, GanttScrollTarget.CapturedCenter), d);
    }

    // ── Finding #5: the theme-driven capture must decode under the OLD direction, explicitly ──

    [Fact]
    public async Task Finding5_A_ThemeService_Direction_Flip_Captures_Under_The_Explicit_Old_Direction()
    {
        var task = new L.GanttTask("t1", "Task", D(2026, 1, 1), D(2026, 1, 10));
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { task })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.ShowTreePane, false));

        var themeService = _ctx.Services.GetRequiredService<Lumeo.Services.IThemeService>();
        Assert.Equal(LayoutDirection.Ltr, themeService.CurrentDirection); // sanity: starts LTR

        await themeService.SetDirectionAsync(LayoutDirection.Rtl);
        cut.WaitForAssertion(() => Assert.NotEmpty(_interop.GanttV3GetScrollCenterXDirections));

        // The capture must have been decoded under "ltr" — the direction in
        // effect BEFORE the flip — explicitly, not by leaving the argument
        // null and trusting whatever the live DOM happens to report (which,
        // for a REAL ThemeService-driven flip, could already be "rtl" by the
        // time this capture runs).
        Assert.Contains("ltr", _interop.GanttV3GetScrollCenterXDirections);
    }

    // ── Finding #6: a dispose landing mid-registration must still unregister ──

    [Fact]
    public async Task Finding6_Disposing_A_Bar_While_Its_Key_Handler_Registration_Is_In_Flight_Still_Unregisters()
    {
        var task = new L.GanttTask("t1", "Task", D(2026, 1, 1), D(2026, 1, 5));
        var rows = new List<GanttVisibleRow> { new(GanttRowKind.Task, task, task.Name, 0, false, null, false) };

        // Gate the registration BEFORE the bar even mounts, so its own
        // automatic firstRender registration call (issued as part of the
        // render below, not something this test invokes directly) gets
        // stuck. OnAfterRenderAsync is fire-and-forget relative to the
        // render pipeline, so _ctx.Render(...) itself still returns normally
        // with the call recorded (TrackingInteropService records the call
        // synchronously, before consulting the gate) but not yet resolved.
        var gate = new TaskCompletionSource();
        _interop.RegisterPreventDefaultKeysGate = gate;

        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { task })
            .Add(c => c.Rows, rows)
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2025, 12, 1))
            .Add(c => c.RangeEnd, D(2026, 3, 1))
            .Add(c => c.OnTaskClick, EventCallback.Factory.Create<L.GanttTask>(this, _ => { })));

        var bar = cut.FindComponent<L.GanttBar>();

        Assert.Single(_interop.RegisterPreventDefaultKeysElementIds);
        var registeredId = _interop.RegisterPreventDefaultKeysElementIds[0];
        Assert.Empty(_interop.UnregisterPreventDefaultKeysElementIds);

        // Dispose the bar WHILE its own registration is still in flight —
        // mirrors virtualization tearing it down (scrolled out of view)
        // before the interop round-trip resolves.
        await bar.Instance.DisposeAsync();
        Assert.Empty(_interop.UnregisterPreventDefaultKeysElementIds); // nothing to tear down yet — not registered when Dispose ran

        // Resume the registration. Under the bug, this would set
        // _preventDefaultKeysRegistered = true on a disposed instance and
        // never unregister — leaking the JS-side entry forever. The resumed
        // continuation runs asynchronously with no task handle this test
        // holds directly, so poll for it instead of awaiting one.
        gate.SetResult();
        cut.WaitForAssertion(() => Assert.Single(_interop.UnregisterPreventDefaultKeysElementIds));
        Assert.Equal(registeredId, _interop.UnregisterPreventDefaultKeysElementIds[0]);
    }
}
