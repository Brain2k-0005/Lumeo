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
/// Codex round 15 (PR #379, feat/gantt-v3) — round 14's consolidation replaced
/// the accreted per-parameter tracking with a single snapshot-diff reconciler,
/// which retired the STATE-ACCRETION bug class entirely (rounds 11-13). What's
/// left is a new class: ASYNC CONCURRENCY around that reconciler's own capture
/// await, plus one straggler from round 14's own raw-count sweep and an
/// unrelated Virtualize identity gap.
///
///  #1 (P1) — an overlapping SECOND reconcile call (e.g. a theme flip firing
///       while a parameter-driven reconcile's own live-center capture is still
///       outstanding) could finish first and commit, only for the FIRST,
///       slower call to resume afterward and commit on top — clobbering the
///       newer, correct outcome with a stale one. Fixed via a generation guard:
///       whichever call claims the LATEST generation before its own capture
///       await is authoritative, regardless of which one's interop round-trip
///       resolves first.
///  #2 (P2) — RefreshBrowserTodayAsync's own "is the task list empty" gate
///       still read the RAW _state.Tasks.Count — the same raw-count class
///       round 14's own BuildSnapshot.RenderableEmpty already closed for the
///       reconcile pass, just missed here since this correction runs entirely
///       outside ReconcileAsync.
///  #3 (P2) — GanttTimeline's own scroll-apply marked the LIVE
///       ScrollToTodayRequestId consumed once its (awaited) interop call
///       resolved — if a NEWER request arrived while that call was still in
///       flight, the older, resuming apply would mark the newer request
///       consumed too, without ever having applied ITS target.
///  #4 (P2) — the Virtualize item template carried no @key, so a live task-set
///       reorder let Blazor reuse the SAME GanttBar instance (and its own
///       JS-interop-backed state) across what is now a DIFFERENT task in that
///       row slot.
///
/// See docs/superpowers/gantt-v3-cx15-report.md for the full writeup.
/// </summary>
public class GanttV3CodexRound15Tests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public GanttV3CodexRound15Tests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<Lumeo.Services.IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static DateTime D(int y, int m, int d) => new(y, m, d);

    // ── Finding #1: a superseded reconcile abandons its commit ──────────────

    [Fact]
    public async Task Finding1_A_Superseded_Reconcile_Abandons_Its_Commit_When_A_Newer_One_Already_Landed()
    {
        var task = new L.GanttTask("t1", "Task", D(2026, 1, 1), D(2026, 1, 10));
        var tasks = new List<L.GanttTask> { task };
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, tasks)
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.ShowTreePane, false));

        // Reconcile A: Day -> Week. Suspend its own live-center capture.
        var gateA = new TaskCompletionSource<double?>();
        _interop.GanttV3ScrollCenterXGate = gateA;

        Task reconcileA = Task.CompletedTask;
        await cut.InvokeAsync(() =>
        {
            reconcileA = cut.Instance.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(L.Gantt3.Tasks)] = tasks,
                [nameof(L.Gantt3.ViewMode)] = L.GanttViewMode.Week,
                [nameof(L.Gantt3.ShowTreePane)] = false,
            }));
        });

        Assert.False(reconcileA.IsCompleted, "reconcile A should still be awaiting its live-center capture");

        // Reconcile B: Day -> Month, racing from the SAME prior (Day) snapshot
        // since A hasn't committed anything yet. Ungated — runs to completion
        // WHILE A is still stuck.
        _interop.GanttV3ScrollCenterXGate = null;
        _interop.GanttV3ScrollCenterXToReturn = 3 * GanttScale.GetConfig(L.GanttViewMode.Day).ColumnWidth;

        await cut.InvokeAsync(async () =>
        {
            await cut.Instance.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(L.Gantt3.Tasks)] = tasks,
                [nameof(L.Gantt3.ViewMode)] = L.GanttViewMode.Month,
                [nameof(L.Gantt3.ShowTreePane)] = false,
            }));
        });

        // B fully committed: PeriodLabel's Month format ("MMMM yyyy") is
        // distinct from Day/Week's date-range format, so this alone proves
        // _state.ViewMode == Month at this point.
        var periodAfterB = cut.Find("span.text-sm.font-medium").TextContent;
        var scrollCallsAfterB = _interop.GanttV3ScrollToXCallCount;

        // Resume A. Under the bug, A would now clobber B's commit with its
        // own (stale) Week-mode range/recenter, decided against a
        // long-superseded snapshot. Under the fix, A finds its claimed
        // generation superseded (B claimed a later one) and abandons its
        // ENTIRE commit -- nothing changes as a result of A resuming.
        gateA.SetResult(0);
        await reconcileA;

        Assert.Equal(periodAfterB, cut.Find("span.text-sm.font-medium").TextContent);
        Assert.Equal(scrollCallsAfterB, _interop.GanttV3ScrollToXCallCount);
    }

    // ── Finding #2: the browser-today correction uses the FILTERED, not RAW, count ──

    [Fact]
    public void Finding2_An_Only_Invalid_Duration_Task_List_Still_Gets_The_Browser_Today_Correction()
    {
        // A non-milestone task whose End precedes its Start is dropped by
        // GanttRowModel.FilterValidDurationTasks (it renders no row) -- the
        // list is renderably EMPTY even though its RAW count is 1. The
        // empty-list branch of ComputeInitialRange seeds VisibleRange from
        // "today" -- first the SERVER's DateTime.Today (at OnInitialized,
        // before the browser date resolves), then, under the fix, corrected
        // once RefreshBrowserTodayAsync learns the browser's actual date. The
        // old bug's RAW _state.Tasks.Count == 0 gate never even attempted
        // that correction for a list like this one (raw count is 1, not 0),
        // leaving the range stuck on the server-seeded value forever.
        var invalidOnly = new L.GanttTask("bad", "Invalid", D(2026, 6, 10), D(2026, 6, 1)); // End < Start, non-milestone
        _interop.GanttV3LocalDateToReturn = "2099-06-15";

        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { invalidOnly })
            .Add(c => c.ViewMode, L.GanttViewMode.Month));

        // Sanity: the invalid task renders no row -- the list really is
        // renderably empty, not merely raw-empty.
        Assert.Empty(cut.FindAll("[data-task-id='bad']"));
        Assert.Single(cut.FindAll(".lumeo-gantt-v3-empty"));

        // ComputeInitialRange (empty-list branch): minDate = 2099-06-15 - 7d
        // = 2099-06-08; Month-unit padding: new DateTime(2099,6,1).AddMonths(-12)
        // = 2098-06-01 -- mirrors GanttV3CodexRound2Tests' own browser-today
        // resolution test, just with an only-invalid (not empty) task list.
        cut.WaitForAssertion(() => Assert.Equal("June 2098", cut.Find("span.text-sm.font-medium").TextContent));
    }

    // ── Finding #3: a superseded scroll apply must not consume a newer request's id ──

    [Fact]
    public async Task Finding3_A_Superseded_Scroll_Apply_Does_Not_Consume_A_Newer_Requests_Id()
    {
        var tasks = new List<L.GanttTask> { new("t1", "Task", D(2026, 1, 1), D(2026, 1, 20)) };
        var rangeStart = D(2025, 12, 1);
        var rangeEnd = D(2026, 3, 1);
        var origin = GanttScale.BuildDateUnits(L.GanttViewMode.Day, rangeStart, rangeEnd)[0];
        var colWidth = GanttScale.GetConfig(L.GanttViewMode.Day).ColumnWidth;
        var today = D(2026, 1, 15);
        var dateA = D(2026, 1, 20);
        var dateB = D(2026, 2, 1);

        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, tasks)
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, rangeStart)
            .Add(c => c.RangeEnd, rangeEnd)
            .Add(c => c.Today, today)
            .Add(c => c.ScrollToTodayRequestId, 0));

        var onAfterRenderAsync = typeof(L.GanttTimeline).GetMethod(
            "OnAfterRenderAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;

        // Baseline: mount's own firstRender already issued one scroll-to-X
        // call (centering on Today) before any of the below.
        var callsAtMount = _interop.GanttV3ScrollToXCalls.Count;

        // Deliberately bypassing the normal SetParametersAsync lifecycle here
        // (direct property writes + a manual reflection-driven
        // OnAfterRenderAsync call below) is what lets this test control the
        // exact interleaving precisely, rather than depending on bUnit's own
        // render-pump timing. The bug-reproducing ORDER matters: id 1's own
        // apply must resume and commit AFTER the parameter has already moved
        // to id 2, but BEFORE id 2's own OnAfterRenderAsync pass has had a
        // chance to run (i.e. id 2 arrived as a parameter push whose own
        // lifecycle invocation is still pending) -- that is exactly the
        // window in which the old bug read the LIVE (already-id-2)
        // ScrollToTodayRequestId from id 1's resuming call and marked id 2
        // "consumed" without id 2 ever having applied its own target.
#pragma warning disable BL0005
        // Request id 1's apply: capture-and-suspend it mid-interop-call.
        var gate = new TaskCompletionSource();
        _interop.GanttV3ScrollToXGate = gate;
        cut.Instance.ScrollTargetDate = dateA;
        cut.Instance.ScrollToTodayRequestId = 1;

        Task applyingId1 = Task.CompletedTask;
        await cut.InvokeAsync(() =>
        {
            applyingId1 = (Task)onAfterRenderAsync.Invoke(cut.Instance, new object[] { false })!;
        });
        Assert.False(applyingId1.IsCompleted, "request id 1's apply should still be awaiting the scroll interop call");
        Assert.Equal(callsAtMount + 1, _interop.GanttV3ScrollToXCalls.Count);

        // Id 2 arrives as a parameter push WHILE id 1 is still stuck -- its
        // own OnAfterRenderAsync pass has NOT run yet at this point.
        cut.Instance.ScrollTargetDate = dateB;
        cut.Instance.ScrollToTodayRequestId = 2;
#pragma warning restore BL0005

        // Resume id 1's apply now -- id 2's own pass still hasn't run.
        _interop.GanttV3ScrollToXGate = null;
        gate.SetResult();
        await applyingId1;

        // Id 1's own resumed commit must not read the (already-id-2) LIVE
        // ScrollToTodayRequestId to decide what it just consumed -- it only
        // ever applied dateA.
        Assert.Equal(callsAtMount + 1, _interop.GanttV3ScrollToXCalls.Count);

        // NOW id 2's own (deferred) OnAfterRenderAsync pass finally runs.
        // Under the bug, id 1's resumed commit already (wrongly) marked id 2
        // "consumed" -- this pass would see intentPending == false and skip
        // its own apply entirely, so dateB's target would NEVER be scrolled
        // to. Under the fix, id 1 only ever recorded ITS OWN id (1), so this
        // pass correctly still sees id 2 as pending and applies it.
        await cut.InvokeAsync(async () =>
        {
            await (Task)onAfterRenderAsync.Invoke(cut.Instance, new object[] { false })!;
        });

        var expectedB = GanttScale.DateToPixel(L.GanttViewMode.Day, origin, dateB, colWidth);
        Assert.Equal(callsAtMount + 2, _interop.GanttV3ScrollToXCalls.Count);
        Assert.Equal(expectedB, _interop.GanttV3ScrollToXCalls[^1], 1);

        // A later, unrelated render (same request id 2, Today/host unchanged)
        // must be a clean no-op -- proving _lastConsumedScrollRequestId now
        // correctly reflects id 2.
        cut.Render(p => p
            .Add(c => c.Tasks, tasks)
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, rangeStart)
            .Add(c => c.RangeEnd, rangeEnd)
            .Add(c => c.Today, today)
            .Add(c => c.ScrollTargetDate, dateB)
            .Add(c => c.ScrollToTodayRequestId, 2));

        Assert.Equal(callsAtMount + 2, _interop.GanttV3ScrollToXCalls.Count);
    }

    // ── Finding #4: a task's bar instance follows its OWN identity across a reorder ──

    [Fact]
    public void Finding4_A_Tasks_Bar_Instance_Follows_Its_Own_Identity_Across_A_Reorder()
    {
        // Without @key, Blazor's Virtualize would match item templates
        // POSITIONALLY -- the component instance rendered at row slot 0 stays
        // the SAME instance across a render even when a DIFFERENT task now
        // occupies that slot, silently carrying over that instance's own
        // JS-interop-backed state (see GanttBar._barId's own remarks) onto
        // the wrong task. With @key="Task.Id" on the row wrapper, Blazor
        // reconciles by IDENTITY instead: each task keeps its OWN bar
        // instance (and _barId) wherever it moves in the list.
        var taskA = new L.GanttTask("A", "Alpha", D(2026, 1, 1), D(2026, 1, 5));
        var taskB = new L.GanttTask("B", "Bravo", D(2026, 2, 1), D(2026, 2, 5));

        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { taskA, taskB })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.ShowTreePane, false));

        var idA0 = BarInstanceId(cut, "A");
        var idB0 = BarInstanceId(cut, "B");
        Assert.NotEqual(idA0, idB0);

        // Reorder: B now occupies row slot 0 (A's old slot), A occupies slot 1.
        cut.Render(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { taskB, taskA })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.ShowTreePane, false));

        var idA1 = BarInstanceId(cut, "A");
        var idB1 = BarInstanceId(cut, "B");

        // Each task's OWN bar instance persisted across the reorder -- had
        // the item template been matched positionally instead (no @key), B's
        // bar at the new slot 0 would have reused A's OLD instance, so idB1
        // would equal idA0 instead of staying distinct from it.
        Assert.Equal(idA0, idA1);
        Assert.Equal(idB0, idB1);
        Assert.NotEqual(idA1, idB1);
    }

    private static string BarInstanceId<TComponent>(Bunit.IRenderedComponent<TComponent> cut, string taskId)
        where TComponent : Microsoft.AspNetCore.Components.IComponent
    {
        var element = cut.Find($"[data-task-id='{taskId}']");
        var ownId = element.GetAttribute("id");
        if (ownId is not null && ownId.StartsWith("gantt-bar-", StringComparison.Ordinal)) return ownId;
        var descendant = element.QuerySelector("[id^='gantt-bar-']")
            ?? throw new InvalidOperationException($"no gantt-bar id found for task {taskId}");
        return descendant.GetAttribute("id")!;
    }
}
