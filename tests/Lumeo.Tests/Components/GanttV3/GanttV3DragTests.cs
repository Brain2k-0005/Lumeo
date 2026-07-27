using System.Reflection;
using Bunit;
using Lumeo.GanttV3;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.GanttV3;

/// <summary>
/// Gantt v3 Phase 2, T1 — the JS drag engine's .NET-side seam: interop
/// registration gating (Readonly must mean NO listener is ever attached, not
/// merely a listener that no-ops) and the CommitDrag JSInvokable ->
/// GanttTaskUpdate -> Gantt3 state-merge pipeline. gantt-v3.js's pointer/ghost
/// geometry itself never executes in bUnit's headless DOM (same limitation
/// GanttInteropTests documents for v2's gantt-v2.js) — that gets its coverage
/// from T4's Playwright suite.
/// </summary>
public class GanttV3DragTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public GanttV3DragTests()
    {
        _ctx.AddLumeoServices();
        // Override the real ComponentInteropService AddLumeoServices just
        // registered with the call-tracking test double (same pattern as
        // AffixDisposeLifecycleTests' GatedAffixInterop) so Readonly-gating and
        // registration-lifecycle assertions don't need a real JS runtime.
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static DateTime D(int y, int m, int d) => new(y, m, d);
    private static L.GanttTask Task1 => new("t1", "Design", D(2026, 1, 2), D(2026, 1, 6));

    private static GanttState State(IRenderedComponent<L.Gantt3> cut) =>
        (GanttState)typeof(L.Gantt3)
            .GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(cut.Instance)!;

    // ── Readonly gating (GanttTimeline) ──────────────────────────────────────

    [Fact]
    public void GanttTimeline_Registers_Drag_Interop_When_Not_Readonly()
    {
        _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.Readonly, false));

        Assert.Equal(1, _interop.GanttV3RegisterDragCallCount);
        Assert.Equal(0, _interop.GanttV3UnregisterDragCallCount);
    }

    [Fact]
    public void GanttTimeline_Readonly_Registers_No_Drag_Interop_At_All()
    {
        _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.Readonly, true));

        Assert.Equal(0, _interop.GanttV3RegisterDragCallCount);
        Assert.Equal(0, _interop.GanttV3UnregisterDragCallCount);
    }

    [Fact]
    public void GanttTimeline_Readonly_Runtime_Flip_Unregisters_Drag_Interop()
    {
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.Readonly, false));
        Assert.Equal(1, _interop.GanttV3RegisterDragCallCount);

        cut.Render(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.Readonly, true));

        Assert.Equal(1, _interop.GanttV3UnregisterDragCallCount);
    }

    [Fact]
    public void GanttTimeline_Reregisters_When_ColumnWidth_Changes_But_Not_Otherwise()
    {
        // Idempotent-registration contract (ganttV3.registerDrag's own remarks):
        // a ColumnWidth override change re-pushes options (columnWidth/pixelsPerDay
        // must never go stale — "JS never re-derives" the snap config), but an
        // unrelated re-render with UNCHANGED drag-relevant options must not
        // re-register at all.
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10)));
        Assert.Equal(1, _interop.GanttV3RegisterDragCallCount);

        // Unrelated re-render (e.g. TodayHighlight toggled) — no re-registration.
        cut.Render(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.TodayHighlight, false));
        Assert.Equal(1, _interop.GanttV3RegisterDragCallCount);

        // ColumnWidth override changes -> re-registers (idempotent options refresh).
        cut.Render(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.ColumnWidth, 76));
        Assert.Equal(2, _interop.GanttV3RegisterDragCallCount);
        Assert.Equal(0, _interop.GanttV3UnregisterDragCallCount); // never unregistered — idempotent swap-in-place
    }

    // ── CommitDrag (JSInvokable) ─────────────────────────────────────────────

    [Fact]
    public async Task CommitDrag_Move_Shifts_Both_Start_And_End()
    {
        GanttTaskUpdate? received = null;
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.OnTaskUpdate, (GanttTaskUpdate u) => { received = u; }));

        await cut.InvokeAsync(() => cut.Instance.CommitDrag("t1", "move", "2026-01-05", "2026-01-09"));

        Assert.NotNull(received);
        Assert.Equal(GanttTaskUpdateSource.Move, received!.Source);
        Assert.Equal(D(2026, 1, 5), received.Task.Start);
        Assert.Equal(D(2026, 1, 9), received.Task.End);
    }

    [Fact]
    public async Task CommitDrag_ResizeStart_Only_Changes_Start()
    {
        GanttTaskUpdate? received = null;
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.OnTaskUpdate, (GanttTaskUpdate u) => { received = u; }));

        await cut.InvokeAsync(() => cut.Instance.CommitDrag("t1", "resize-start", "2026-01-03", "2026-01-06"));

        Assert.NotNull(received);
        Assert.Equal(GanttTaskUpdateSource.ResizeStart, received!.Source);
        Assert.Equal(D(2026, 1, 3), received.Task.Start);
        Assert.Equal(D(2026, 1, 6), received.Task.End); // unchanged (v2/resize-end parity: only ONE edge moves)
    }

    [Fact]
    public async Task CommitDrag_ResizeEnd_Only_Changes_End()
    {
        GanttTaskUpdate? received = null;
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.OnTaskUpdate, (GanttTaskUpdate u) => { received = u; }));

        await cut.InvokeAsync(() => cut.Instance.CommitDrag("t1", "resize-end", "2026-01-02", "2026-01-08"));

        Assert.NotNull(received);
        Assert.Equal(GanttTaskUpdateSource.ResizeEnd, received!.Source);
        Assert.Equal(D(2026, 1, 2), received.Task.Start); // unchanged
        Assert.Equal(D(2026, 1, 8), received.Task.End);
    }

    [Fact]
    public async Task CommitDrag_ResizeEnd_Clamps_To_Start_When_Inverted()
    {
        // Defensive clamp (mirrors gantt-v2.js:755 `if (task.end < task.start)
        // task.end = task.start;`) — gantt-v3.js already applies the same clamp
        // before calling CommitDrag, but this JSInvokable is itself a public
        // surface, so the .NET side guards independently too.
        GanttTaskUpdate? received = null;
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.OnTaskUpdate, (GanttTaskUpdate u) => { received = u; }));

        await cut.InvokeAsync(() => cut.Instance.CommitDrag("t1", "resize-end", "2026-01-02", "2025-12-01"));

        Assert.NotNull(received);
        Assert.Equal(D(2026, 1, 2), received!.Task.End); // clamped to original Start
        Assert.Equal(D(2026, 1, 2), received.Task.Start);
    }

    [Fact]
    public async Task CommitDrag_ResizeStart_Clamps_To_End_When_Inverted()
    {
        GanttTaskUpdate? received = null;
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.OnTaskUpdate, (GanttTaskUpdate u) => { received = u; }));

        await cut.InvokeAsync(() => cut.Instance.CommitDrag("t1", "resize-start", "2026-02-01", "2026-01-06"));

        Assert.NotNull(received);
        Assert.Equal(D(2026, 1, 6), received!.Task.Start); // clamped to original End
        Assert.Equal(D(2026, 1, 6), received.Task.End);
    }

    [Fact]
    public async Task CommitDrag_Move_Preserves_Duration()
    {
        // Task1 spans Jan 2 - Jan 6 (5 days). A pure move must preserve that
        // exact span regardless of the day delta.
        GanttTaskUpdate? received = null;
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 2, 10))
            .Add(c => c.OnTaskUpdate, (GanttTaskUpdate u) => { received = u; }));

        await cut.InvokeAsync(() => cut.Instance.CommitDrag("t1", "move", "2026-01-20", "2026-01-24"));

        Assert.NotNull(received);
        Assert.Equal(4, (received!.Task.End - received.Task.Start).Days);
    }

    [Fact]
    public async Task CommitDrag_Unknown_TaskId_Fires_Nothing()
    {
        var fired = false;
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.OnTaskUpdate, (GanttTaskUpdate _) => { fired = true; }));

        await cut.InvokeAsync(() => cut.Instance.CommitDrag("nope", "move", "2026-01-05", "2026-01-09"));

        Assert.False(fired);
    }

    [Fact]
    public async Task CommitDrag_Invalid_Date_String_Fires_Nothing()
    {
        var fired = false;
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.OnTaskUpdate, (GanttTaskUpdate _) => { fired = true; }));

        await cut.InvokeAsync(() => cut.Instance.CommitDrag("t1", "move", "not-a-date", "2026-01-09"));

        Assert.False(fired);
    }

    // ── GanttBar data-* attributes (JS reads these to avoid a mid-drag round trip) ──

    [Fact]
    public void GanttBar_Renders_DataTaskStart_And_DataTaskEnd_Attributes()
    {
        var task = new L.GanttTask("t1", "Design", D(2026, 1, 2), D(2026, 1, 6));
        var cut = _ctx.Render<L.GanttBar>(p => p
            .Add(c => c.Task, task).Add(c => c.X, 0d).Add(c => c.Width, 100d).Add(c => c.RowIndex, 0));

        var wrapper = cut.Find("[data-task-id='t1']");
        Assert.Equal("2026-01-02", wrapper.GetAttribute("data-task-start"));
        Assert.Equal("2026-01-06", wrapper.GetAttribute("data-task-end"));
    }

    // ── Gantt3 end-to-end (bubbled through the real, nested GanttTimeline) ───

    [Fact]
    public async Task Gantt3_Drag_Commit_Merges_Task_And_Fires_TasksChanged_OnDateChange_OnTaskUpdate()
    {
        IEnumerable<L.GanttTask>? pushedTasks = null;
        L.GanttTask? dateChanged = null;
        GanttTaskUpdate? taskUpdate = null;

        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.TasksChanged, (IEnumerable<L.GanttTask> ts) => { pushedTasks = ts; })
            .Add(c => c.OnDateChange, (L.GanttTask t) => { dateChanged = t; })
            .Add(c => c.OnTaskUpdate, (GanttTaskUpdate u) => { taskUpdate = u; }));

        var timeline = cut.FindComponent<L.GanttTimeline>();
        await cut.InvokeAsync(() => timeline.Instance.CommitDrag("t1", "move", "2026-01-05", "2026-01-09"));

        Assert.NotNull(dateChanged);
        Assert.Equal(D(2026, 1, 5), dateChanged!.Start);
        Assert.Equal(D(2026, 1, 9), dateChanged.End);

        Assert.NotNull(taskUpdate);
        Assert.Equal(GanttTaskUpdateSource.Move, taskUpdate!.Source);
        Assert.Equal(D(2026, 1, 5), taskUpdate.Task.Start);

        Assert.NotNull(pushedTasks);
        var pushed = Assert.Single(pushedTasks!);
        Assert.Equal(D(2026, 1, 5), pushed.Start);
        Assert.Equal(D(2026, 1, 9), pushed.End);

        // The rendered bar reflects the committed dates too — GanttState was
        // actually mutated, not just the callback arguments.
        Assert.Equal("2026-01-05", cut.Find("[data-task-id='t1']").GetAttribute("data-task-start"));
    }

    [Fact]
    public async Task Gantt3_Uncontrolled_Drag_Commit_Survives_An_Unrelated_Rerender_With_Stale_Tasks_Parameter()
    {
        // Regression guard for the discriminator this task adds to
        // Gantt3.OnParametersSet (mirrors v2 Gantt.razor's
        // _lastParentHash/_lastPushedTasksHash): without it, the PRE-existing
        // code unconditionally re-applied the Tasks PARAMETER into GanttState on
        // every OnParametersSet, so any later re-render that handed Gantt3 the
        // SAME (uncontrolled, never-updated) Tasks reference would silently
        // revert a just-committed drag.
        var initialTasks = new List<L.GanttTask> { Task1 };
        var cut = _ctx.Render<L.Gantt3>(p => p.Add(c => c.Tasks, initialTasks));

        var timeline = cut.FindComponent<L.GanttTimeline>();
        await cut.InvokeAsync(() => timeline.Instance.CommitDrag("t1", "move", "2026-01-05", "2026-01-09"));
        Assert.Equal("2026-01-05", cut.Find("[data-task-id='t1']").GetAttribute("data-task-start"));

        // An unrelated re-render supplies the EXACT SAME (stale, pre-drag) Tasks
        // list an uncontrolled caller never updated.
        cut.Render(p => p.Add(c => c.Tasks, initialTasks));

        Assert.Equal("2026-01-05", cut.Find("[data-task-id='t1']").GetAttribute("data-task-start"));
    }

    [Fact]
    public async Task Gantt3_Controlled_Drag_Commit_Rolls_Back_On_A_Genuine_Parent_Veto()
    {
        // CONTROLLED counterpart: when TasksChanged IS bound, a parent that
        // deliberately supplies something OTHER than the echoed value (a veto)
        // must still win and roll the edit back — the discriminator must not
        // make the local edit permanently sticky.
        var initialTasks = new List<L.GanttTask> { Task1 };
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, initialTasks)
            .Add(c => c.TasksChanged, (IEnumerable<L.GanttTask> _) => { }));

        var timeline = cut.FindComponent<L.GanttTimeline>();
        await cut.InvokeAsync(() => timeline.Instance.CommitDrag("t1", "move", "2026-01-05", "2026-01-09"));
        Assert.Equal("2026-01-05", cut.Find("[data-task-id='t1']").GetAttribute("data-task-start"));

        // Parent vetoes: re-supplies the ORIGINAL (pre-drag) tasks explicitly.
        cut.Render(p => p
            .Add(c => c.Tasks, initialTasks)
            .Add(c => c.TasksChanged, (IEnumerable<L.GanttTask> _) => { }));

        Assert.Equal("2026-01-02", cut.Find("[data-task-id='t1']").GetAttribute("data-task-start"));
    }

    // ── Codex PR-383 findings (Gantt3.razor, blocked until PR #382 merged) ───

    [Fact]
    public async Task Gantt3_Drag_Commit_Expands_VisibleRange_To_Cover_A_Task_Moved_Outside_It()
    {
        // [P1] "Expand the visible range after date edits" — HandleTaskUpdateAsync
        // used to touch ONLY _state.Tasks, leaving VisibleRange (and
        // _tasksVersion/_lastSnapshot) describing the pre-drag task set. Task1
        // mounts at Jan 2-6, 2026; Day mode pads +/-60 days around it (see
        // GanttScale's own Day config), so the mount VisibleRange stops well
        // short of August. A move far past that padded End must grow the range
        // to cover it (a union with the current range — see the fix's own
        // remarks for why not a full ComputeInitialRange replacement).
        var cut = _ctx.Render<L.Gantt3>(p => p.Add(c => c.Tasks, new List<L.GanttTask> { Task1 }));

        var mountRange = State(cut).VisibleRange;
        var newEnd = D(2026, 8, 5);
        Assert.True(newEnd > mountRange.End, "test setup: the drag target must fall outside the mount-padded range");

        await cut.InvokeAsync(() => cut.FindComponent<L.GanttTimeline>().Instance
            .CommitDrag("t1", "move", "2026-08-01", "2026-08-05"));

        var padAfterDays = GanttScale.GetConfig(L.GanttViewMode.Day).PadAfter;
        var expectedEnd = newEnd.AddDays(padAfterDays);
        Assert.Equal(expectedEnd, State(cut).VisibleRange.End);
        // The Start boundary didn't need to move for THIS edit — expansion is a
        // union, never a wholesale replacement, so it stays exactly where mount
        // left it.
        Assert.Equal(mountRange.Start, State(cut).VisibleRange.Start);
    }

    [Fact]
    public async Task Gantt3_Drag_Commit_Fires_Edit_Callbacks_Before_TasksChanged()
    {
        // [P2] "Notify edit callbacks before TasksChanged" — v2 parity requires
        // the gesture-specific/unified edit callbacks (OnDateChange/
        // OnProgressChange/OnTaskCreate/OnTaskUpdate) to fire BEFORE
        // TasksChanged, not after: a controlled parent's TasksChanged handler
        // can synchronously normalize/veto and rerender, so anything that runs
        // afterward only ever observes whatever the chart already adopted or
        // rolled back to.
        var order = new List<string>();
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.TasksChanged, (IEnumerable<L.GanttTask> _) => order.Add("TasksChanged"))
            .Add(c => c.OnDateChange, (L.GanttTask _) => order.Add("OnDateChange"))
            .Add(c => c.OnTaskUpdate, (GanttTaskUpdate _) => order.Add("OnTaskUpdate")));

        var timeline = cut.FindComponent<L.GanttTimeline>();
        await cut.InvokeAsync(() => timeline.Instance.CommitDrag("t1", "move", "2026-01-05", "2026-01-09"));

        Assert.Equal(new[] { "OnDateChange", "OnTaskUpdate", "TasksChanged" }, order);
    }

    [Fact]
    public void Gantt3_Readonly_Suppresses_Drag_Registration_On_The_Nested_Timeline()
    {
        _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.Readonly, true));

        Assert.Equal(0, _interop.GanttV3RegisterDragCallCount);
    }

    [Fact]
    public void Gantt3_Not_Readonly_Registers_Drag_Interop_On_The_Nested_Timeline()
    {
        _ctx.Render<L.Gantt3>(p => p.Add(c => c.Tasks, new List<L.GanttTask> { Task1 }));

        Assert.Equal(1, _interop.GanttV3RegisterDragCallCount);
    }

    // ── Codex P2 finding ("Tear down drag registration after an in-flight
    // dispose") — mirrors GanttV3CodexRound17Tests' identical
    // Finding3_Disposing_While_Vertical_Scroll_Tracking_Registration_...
    // pattern one-for-one, for SyncDragRegistrationAsync's own analogous race. ──

    [Fact]
    public async Task Disposing_While_Drag_Registration_Is_In_Flight_Still_Unregisters_After_Resuming()
    {
        var gate = new TaskCompletionSource();
        _interop.GanttV3RegisterDragGate = gate;

        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.Readonly, false));

        // The register call landed (and _dragRegistered was set true
        // synchronously, before its own await) even though it's still
        // suspended on the gate.
        Assert.Equal(1, _interop.GanttV3RegisterDragCallCount);
        var unregisterCountBeforeDispose = _interop.GanttV3UnregisterDragCallCount;

        await cut.Instance.DisposeAsync();

        // DisposeAsync's own PRE-EXISTING, unconditional check already sees
        // _dragRegistered == true and fires its own unregister — this alone
        // is pre-existing behavior, not proof of the fix.
        var unregisterCountAfterDispose = _interop.GanttV3UnregisterDragCallCount;
        Assert.True(unregisterCountAfterDispose > unregisterCountBeforeDispose,
            "DisposeAsync's own existing check should have already attempted an unregister");

        // Resume the register call. Predicted WITHOUT the fix: nothing
        // further ever fires — if DisposeAsync's own unregister above raced
        // ahead of the JS side's still-in-flight register call and lost
        // (arriving first, no-op'ing against nothing registered yet), the
        // listener stays registered forever with nothing left in C# to ever
        // tear it down again, so GanttV3UnregisterDragCallCount would stay
        // frozen at unregisterCountAfterDispose (1) even after this loop.
        // WITH the fix: the resumed continuation re-checks _disposed
        // immediately after its own await and fires one more unregister,
        // taking the count to unregisterCountAfterDispose + 1 (2) regardless
        // of how the two calls raced.
        gate.SetResult();
        for (var i = 0; i < 100 && _interop.GanttV3UnregisterDragCallCount <= unregisterCountAfterDispose; i++)
            await Task.Delay(10);

        Assert.True(_interop.GanttV3UnregisterDragCallCount > unregisterCountAfterDispose,
            "the register call's own resumed continuation must fire one more unregister after observing disposed state");
    }

    [Fact]
    public async Task Readonly_Flip_While_Drag_Registration_Is_In_Flight_Still_Unregisters_After_Resuming()
    {
        // Same race, different trigger: a Readonly flip (not disposal) lands
        // on a LATER render while an EARLIER render's own register call is
        // still in flight. That later render's own Readonly branch already
        // fires its own unregister (since _dragRegistered was already true),
        // but the earlier call's resumed continuation must ALSO re-check
        // (now-live) Readonly and fire one more, for the identical
        // ordering-race reason as the disposal case above.
        var gate = new TaskCompletionSource();
        _interop.GanttV3RegisterDragGate = gate;

        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.Readonly, false));

        Assert.Equal(1, _interop.GanttV3RegisterDragCallCount);
        var unregisterCountBeforeFlip = _interop.GanttV3UnregisterDragCallCount;

        // Flip Readonly true on a later render while the first render's own
        // register call is still suspended on the gate.
        cut.Render(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.Readonly, true));

        var unregisterCountAfterFlip = _interop.GanttV3UnregisterDragCallCount;
        Assert.True(unregisterCountAfterFlip > unregisterCountBeforeFlip,
            "the Readonly-branch's own existing check should have already attempted an unregister");

        gate.SetResult();
        for (var i = 0; i < 100 && _interop.GanttV3UnregisterDragCallCount <= unregisterCountAfterFlip; i++)
            await Task.Delay(10);

        Assert.True(_interop.GanttV3UnregisterDragCallCount > unregisterCountAfterFlip,
            "the register call's own resumed continuation must observe the now-live Readonly flip and fire one more unregister");
    }

    // ── Codex full-review findings (post PR #382/#383 gates) ─────────────────

    [Fact]
    public async Task Gantt3_Concurrent_Drag_Commits_On_Different_Bars_Both_Survive_A_Range_Expansion_Capture()
    {
        // [P1] "Serialize task commits across the center capture" —
        // HandleTaskUpdateAsync derives newTasks from _state.Tasks BEFORE
        // awaiting the live-center capture (needed only when the edit
        // expands VisibleRange). The JS engine explicitly permits concurrent
        // drags on DIFFERENT bars (activeBarDrags only rejects a second
        // pointer on the SAME bar), so a second commit can land during that
        // await; the first must not then overwrite it with a stale,
        // pre-await snapshot.
        var taskA = new L.GanttTask("a", "A", D(2026, 1, 2), D(2026, 1, 6));
        var taskB = new L.GanttTask("b", "B", D(2026, 1, 10), D(2026, 1, 14));
        var cut = _ctx.Render<L.Gantt3>(p => p.Add(c => c.Tasks, new List<L.GanttTask> { taskA, taskB }));
        var timeline = cut.FindComponent<L.GanttTimeline>();

        // Drag A moves far past the mount-padded VisibleRange — forces the
        // range-expansion branch (and its live-center capture) to run and
        // suspend. Drag B, on a DIFFERENT bar, is started from the SAME
        // dispatched callback, immediately after A — mirrors how a real
        // circuit processes two back-to-back JSInvokable calls on its one
        // synchronization context: B starts running (and, needing no
        // expansion, completes) WHILE A's own continuation is still parked
        // on the gate. Two SEPARATE cut.InvokeAsync dispatches would instead
        // deadlock bUnit's own dispatcher while A's is still pending — this
        // single-dispatch shape is what actually reproduces the race.
        var gate = new TaskCompletionSource<double?>();
        _interop.GanttV3ScrollCenterXGate = gate;
        System.Threading.Tasks.Task dragA = System.Threading.Tasks.Task.CompletedTask;
        System.Threading.Tasks.Task dragB = System.Threading.Tasks.Task.CompletedTask;
        await cut.InvokeAsync(() =>
        {
            dragA = timeline.Instance.CommitDrag("a", "move", "2026-08-01", "2026-08-05");
            dragB = timeline.Instance.CommitDrag("b", "move", "2026-01-06", "2026-01-10");
        });
        Assert.False(dragA.IsCompleted, "drag A should still be awaiting its range-expansion capture");
        Assert.True(dragB.IsCompleted, "drag B needs no expansion and should have committed synchronously");
        Assert.Equal("2026-01-06", cut.Find("[data-task-id='b']").GetAttribute("data-task-start"));

        // Resume A.
        _interop.GanttV3ScrollCenterXGate = null;
        _interop.GanttV3ScrollCenterXToReturn = 0;
        gate.SetResult(0);
        await dragA;

        // Both edits must have survived — A's own (range-expanding) commit
        // must not have overwritten B's with a pre-await snapshot that
        // predates it.
        Assert.Equal("2026-08-01", cut.Find("[data-task-id='a']").GetAttribute("data-task-start"));
        Assert.Equal("2026-01-06", cut.Find("[data-task-id='b']").GetAttribute("data-task-start"));
    }

    [Fact]
    public void GanttBar_Wrapper_Carries_Touch_Pan_Y_Only_When_Not_Readonly()
    {
        // [P2] "Opt touch drag targets out of native horizontal panning" —
        // touch-action must be scoped to elements a drag gesture is actually
        // registered for (Readonly means no listener is ever attached at
        // all — see SyncDragRegistrationAsync's own contract), not applied
        // unconditionally (PR #381's own dead-scroll-zone regression came
        // from exactly that: touch-action:none applied broadly, always).
        var cutInteractive = _ctx.Render<L.GanttBar>(p => p
            .Add(c => c.Task, Task1).Add(c => c.X, 0d).Add(c => c.Width, 100d).Add(c => c.RowIndex, 0)
            .Add(c => c.Readonly, false));
        Assert.Contains("touch-pan-y", cutInteractive.Find("[data-task-id='t1']").ClassList);

        var cutReadonly = _ctx.Render<L.GanttBar>(p => p
            .Add(c => c.Task, Task1).Add(c => c.X, 0d).Add(c => c.Width, 100d).Add(c => c.RowIndex, 0)
            .Add(c => c.Readonly, true));
        Assert.DoesNotContain("touch-pan-y", cutReadonly.Find("[data-task-id='t1']").ClassList);
    }

    [Fact]
    public async Task Gantt3_Bar_Click_Stays_Native_When_Drag_Registration_Throws()
    {
        // [P2] "Keep pointer clicks when drag interop is unavailable" — a
        // register call that genuinely throws (e.g. a custom
        // IComponentInteropService override that fails) must not leave
        // every bar's native onclick permanently suppressed with no
        // delegated JS listener ever actually attached — pointer clicks
        // would otherwise be dead forever even though keyboard activation
        // (Enter/Space) keeps working.
        _interop.GanttV3RegisterDragException = new InvalidOperationException("simulated custom interop failure");

        L.GanttTask? clicked = null;
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.OnTaskClick, (L.GanttTask t) => { clicked = t; }));

        Assert.True(_interop.GanttV3RegisterDragCallCount > 0, "registration must still have been attempted");

        // Native onclick must remain wired — SuppressPointerClick stays
        // false when registration never confirmed. onclick lives on the
        // INNER content div (InnerAttributes), not the [data-task-id]
        // wrapper itself — same selector GanttBar's own click/keydown tests
        // use (see e.g. GanttV3CodexRound4Tests' identical pattern).
        cut.Find("[data-task-id='t1'] > div").Click();
        Assert.NotNull(clicked);
        Assert.Equal("t1", clicked!.Id);
    }
}
