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
}
