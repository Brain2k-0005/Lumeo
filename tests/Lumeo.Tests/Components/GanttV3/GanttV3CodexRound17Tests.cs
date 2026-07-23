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
/// Codex round 17 (PR #379, feat/gantt-v3) — 5 findings, the lifecycle tail
/// of the async-concurrency class rounds 15-16 opened up.
///
///  #1 (P1) — OnParametersSetAsync calls ReconcileAsync UNCONDITIONALLY on
///       every parameter pass; an unrelated parent re-render (nothing in the
///       snapshot diff actually changed) still claimed a generation before
///       round 16's own fix (which only closed this for the theme path) —
///       aborting a genuinely in-flight, currently-suspended reconcile with
///       nothing useful committed to replace it.
///  #2 (P2) — Dispose() only ever unsubscribed from the theme event; a
///       reconcile suspended awaiting the live-center capture via ANY OTHER
///       trigger (a parameter pass, a toolbar click) had nothing invalidating
///       it on disposal, so its resumed continuation could still commit to
///       _state / call StateHasChanged on an already-disposed component.
///  #3 (P2) — GanttTimeline's vertical-scroll-tracking registration has the
///       same late-interop-vs-dispose race GanttBar's own round-16 fix
///       closed: the register call sets its OWN "registered" flag
///       synchronously (so Dispose's pre-existing check already fires ITS
///       own unregister attempt) but that attempt can race the JS side's own
///       still-in-flight register call and lose, leaving the listener
///       registered forever with nothing left to try again.
///  #4 (P2) — GanttBar's OnTaskClick delegate can be REMOVED in a later
///       render while an EARLIER render's own RegisterPreventDefaultKeys
///       call (issued when a delegate was still present) is still in
///       flight — the resumed continuation used to commit "registered"
///       against a desired state that's already stale.
///  #5 (P2) — v2 treats a 0 (or negative) ColumnWidth/BarHeight override as
///       UNSET via a plain JS truthy check; v3's `??` only checks for null,
///       so an explicit 0 silently broke rendering (zero-width columns,
///       zero-height bars) instead of falling back to the default.
///
/// See docs/superpowers/gantt-v3-cx17-report.md for the full writeup.
/// </summary>
public class GanttV3CodexRound17Tests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public GanttV3CodexRound17Tests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<Lumeo.Services.IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static DateTime D(int y, int m, int d) => new(y, m, d);

    // ── Finding #1: an unrelated parameter re-render must not claim a generation ──

    [Fact]
    public async Task Finding1_An_Unrelated_Parameter_ReRender_Does_Not_Abort_A_Suspended_Reconcile()
    {
        var task = new L.GanttTask("t1", "Task", D(2026, 1, 1), D(2026, 1, 10));
        var tasks = new List<L.GanttTask> { task };
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, tasks)
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.ShowTreePane, false)
            .Add(c => c.Class, "initial-class"));

        var scrollsAtMount = _interop.GanttV3ScrollToXCallCount;

        // Suspend a genuine, mode-relevant reconcile mid-capture.
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
                [nameof(L.Gantt3.Class)] = "initial-class",
            }));
        });
        Assert.False(reconcile.IsCompleted, "the mode-switch reconcile should still be awaiting its live-center capture");

        // An UNRELATED parent re-render — same Tasks/ViewMode/ShowTreePane,
        // only Class differs (a parameter Decide's own snapshot diff never
        // looks at at all) — must diff to a complete no-op and never even
        // enter the generation-claiming path.
        await cut.InvokeAsync(async () =>
        {
            await cut.Instance.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(L.Gantt3.Tasks)] = tasks,
                [nameof(L.Gantt3.ViewMode)] = L.GanttViewMode.Week, // matches _state.ViewMode already (uncontrolled, unchanged since last pass)
                [nameof(L.Gantt3.ShowTreePane)] = false,
                [nameof(L.Gantt3.Class)] = "different-class",
            }));
        });

        // Resume the suspended reconcile. Under the bug, the no-op re-render
        // above would have already claimed a newer generation, so this would
        // find itself superseded and abandon its own commit entirely.
        gate.SetResult(0);
        await reconcile;

        Assert.True(_interop.GanttV3ScrollToXCallCount > scrollsAtMount,
            "the mode-switch reconcile must have committed and emitted its own recenter, not been silently aborted by the unrelated no-op re-render");
    }

    // ── Finding #2: disposal must invalidate a suspended reconcile ──

    [Fact]
    public async Task Finding2_Disposing_During_A_Suspended_Reconcile_Prevents_Its_Post_Disposal_Commit()
    {
        var task = new L.GanttTask("t1", "Task", D(2026, 1, 1), D(2026, 1, 10));
        var tasks = new List<L.GanttTask> { task };
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, tasks)
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.ShowTreePane, false));

        var scrollsAtMount = _interop.GanttV3ScrollToXCallCount;

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

        // Dispose WHILE the reconcile is still suspended.
        cut.Instance.Dispose();

        // Resume it. Under the bug, this resumed continuation would still
        // commit to _state (and, via its own caller, potentially touch a
        // disposed render tree) — nothing invalidated it on disposal.
        gate.SetResult(0);
        await reconcile;

        Assert.Equal(scrollsAtMount, _interop.GanttV3ScrollToXCallCount);
    }

    // ── Finding #3: vertical-scroll-tracking registration vs. disposal ──

    [Fact]
    public async Task Finding3_Disposing_While_Vertical_Scroll_Tracking_Registration_Is_In_Flight_Still_Unregisters_After_Resuming()
    {
        var gate = new TaskCompletionSource();
        _interop.GanttV3RegisterVerticalScrollTrackingGate = gate;

        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask>())
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2025, 12, 1))
            .Add(c => c.RangeEnd, D(2026, 3, 1)));

        Assert.Equal(1, _interop.GanttV3RegisterVerticalScrollTrackingCallCount);
        var unregisterCountBeforeDispose = _interop.GanttV3UnregisterVerticalScrollTrackingCallCount;

        await cut.Instance.DisposeAsync();

        // DisposeAsync's own pre-existing, unconditional check already sees
        // _verticalScrollTrackingRegistered == true (set synchronously before
        // the register call's own await) and fires ITS OWN unregister
        // attempt — this alone isn't proof of the round-17 fix, only of
        // pre-existing behavior.
        var unregisterCountAfterDispose = _interop.GanttV3UnregisterVerticalScrollTrackingCallCount;
        Assert.True(unregisterCountAfterDispose > unregisterCountBeforeDispose,
            "DisposeAsync's own existing check should have already attempted an unregister");

        // Resume the register call. Under the bug, nothing further would
        // ever fire — if DisposeAsync's own unregister call above happened
        // to race ahead of the JS side's own (still in-flight) register
        // call and lose (arriving first, no-op'ing against nothing
        // registered yet), the listener would stay registered forever with
        // nothing left in C# to ever tear it down again. The round-17 fix
        // re-checks disposed state immediately after THIS call's own await
        // and fires one more unregister, deterministically covering that
        // ordering regardless of what DisposeAsync's own attempt above did.
        gate.SetResult();
        for (var i = 0; i < 100 && _interop.GanttV3UnregisterVerticalScrollTrackingCallCount <= unregisterCountAfterDispose; i++)
            await Task.Delay(10);

        Assert.True(_interop.GanttV3UnregisterVerticalScrollTrackingCallCount > unregisterCountAfterDispose,
            "the register call's own resumed continuation must fire one more unregister after observing disposed state");
    }

    // ── Finding #4: OnTaskClick removed mid-registration must unregister ──

    [Fact]
    public async Task Finding4_Removing_OnTaskClick_While_Registration_Is_In_Flight_Unregisters_Instead_Of_Committing()
    {
        var task = new L.GanttTask("t1", "Task", D(2026, 1, 1), D(2026, 1, 5));
        var rows = new List<GanttVisibleRow> { new(GanttRowKind.Task, task, task.Name, 0, false, null, false) };

        // Gate the registration BEFORE the bar even mounts, so its own
        // automatic firstRender registration call (triggered because
        // OnTaskClick has a delegate at mount time) gets stuck.
        var gate = new TaskCompletionSource();
        _interop.RegisterPreventDefaultKeysGate = gate;

        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { task })
            .Add(c => c.Rows, rows)
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2025, 12, 1))
            .Add(c => c.RangeEnd, D(2026, 3, 1))
            .Add(c => c.OnTaskClick, EventCallback.Factory.Create<L.GanttTask>(this, _ => { })));

        Assert.Single(_interop.RegisterPreventDefaultKeysElementIds);
        var registeredId = _interop.RegisterPreventDefaultKeysElementIds[0];
        Assert.Empty(_interop.UnregisterPreventDefaultKeysElementIds);

        // Remove OnTaskClick entirely (no delegate) WHILE the earlier
        // render's own registration call is still in flight — the parameter
        // mutation itself is synchronous, landing immediately regardless of
        // the suspended interop call. bUnit's own Render(...) MERGES with
        // previously-set parameters rather than resetting omitted ones to
        // their default, so the "no delegate" value must be passed
        // explicitly here, not just left out.
        cut.Render(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { task })
            .Add(c => c.Rows, rows)
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2025, 12, 1))
            .Add(c => c.RangeEnd, D(2026, 3, 1))
            .Add(c => c.OnTaskClick, default(EventCallback<L.GanttTask>)));

        Assert.Empty(_interop.UnregisterPreventDefaultKeysElementIds); // not yet -- the in-flight call hasn't resumed

        // Resume the registration. Under the bug, this commits
        // _preventDefaultKeysRegistered = true against a desired state
        // (OnTaskClick present) that's already stale.
        gate.SetResult();
        _interop.RegisterPreventDefaultKeysGate = null;
        for (var i = 0; i < 100 && _interop.UnregisterPreventDefaultKeysElementIds.Count == 0; i++)
            await Task.Delay(10);

        Assert.Single(_interop.UnregisterPreventDefaultKeysElementIds);
        Assert.Equal(registeredId, _interop.UnregisterPreventDefaultKeysElementIds[0]);
    }

    // ── Finding #5: a zero ColumnWidth/BarHeight override falls back to the default ──

    [Fact]
    public void Finding5_A_Zero_ColumnWidth_Override_Falls_Back_To_The_Modes_Own_Default()
    {
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask>())
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2025, 12, 1))
            .Add(c => c.RangeEnd, D(2026, 3, 1))
            .Add(c => c.ColumnWidth, 0)
            .Add(c => c.BarHeight, 0));

        var effectiveColumnWidthProp = typeof(L.GanttTimeline).GetProperty(
            "EffectiveColumnWidth", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var effectiveBarHeightProp = typeof(L.GanttTimeline).GetProperty(
            "EffectiveBarHeight", BindingFlags.NonPublic | BindingFlags.Instance)!;

        var expectedColumnWidth = GanttScale.GetConfig(L.GanttViewMode.Day).ColumnWidth;
        var expectedBarHeight = GanttScale.DefaultBarHeight;

        Assert.Equal(expectedColumnWidth, (int)effectiveColumnWidthProp.GetValue(cut.Instance)!);
        Assert.Equal(expectedBarHeight, (int)effectiveBarHeightProp.GetValue(cut.Instance)!);
    }

    [Fact]
    public void Finding5_A_Zero_ColumnWidth_Override_Also_Falls_Back_On_Gantt3s_Own_Reconcile_Facing_Property()
    {
        // Gantt3.EffectiveColumnWidthFor feeds the reconcile's own snapshot/
        // capture math -- if it disagreed with what GanttTimeline actually
        // renders (fixed above), the reconcile would decode captured scroll
        // positions against the WRONG width.
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask>())
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.ColumnWidth, 0));

        var method = typeof(L.Gantt3).GetMethod(
            "EffectiveColumnWidthFor", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var expected = GanttScale.GetConfig(L.GanttViewMode.Day).ColumnWidth;

        Assert.Equal(expected, (int)method.Invoke(cut.Instance, new object[] { L.GanttViewMode.Day })!);
    }
}
