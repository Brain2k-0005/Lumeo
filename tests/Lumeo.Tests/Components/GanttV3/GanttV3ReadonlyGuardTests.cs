using Bunit;
using Lumeo.GanttV3;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.GanttV3;

/// <summary>
/// Gantt v3 Phase 2 fix-round — Readonly was gating JS-side REGISTRATION (no
/// listener attached) but not enforced server-side on the JSInvokable surface
/// itself. Real reachable path: <c>unregisterDrag</c> only detaches the
/// delegated pointerdown listener on the scroll host; an in-flight drag's
/// move/up handlers live in gantt-v3.js's own per-drag closure (attached
/// directly to the bar/track element at pointerdown time), so a Readonly flip
/// MID-drag doesn't stop that closure from calling CommitDrag/CommitProgress/
/// CommitCreate/NotifyTaskClick on release. This file exercises the guards
/// added to <c>GanttTimeline.CommitDrag</c>/<c>CommitProgress</c>/
/// <c>CommitCreate</c>/<c>NotifyTaskClick</c>/<c>ValidateDrop</c> by invoking
/// each JSInvokable DIRECTLY while Readonly — the E2E suite (T4) cannot catch
/// this class of bug, since it only proves gantt-v3.js itself never calls
/// these methods when readonly, not that the .NET side would reject a call
/// that reached it anyway.
/// </summary>
public class GanttV3ReadonlyGuardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public GanttV3ReadonlyGuardTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static DateTime D(int y, int m, int d) => new(y, m, d);
    private static L.GanttTask Task1 => new("t1", "Design", D(2026, 1, 2), D(2026, 1, 6));

    [Fact]
    public async Task CommitDrag_NoOps_When_Readonly()
    {
        var fired = false;
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.Readonly, true)
            .Add(c => c.OnTaskUpdate, (GanttTaskUpdate _) => { fired = true; }));

        await cut.InvokeAsync(() => cut.Instance.CommitDrag("t1", "move", "2026-01-05", "2026-01-09"));

        Assert.False(fired);
    }

    [Fact]
    public async Task CommitProgress_NoOps_When_Readonly()
    {
        var fired = false;
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.Readonly, true)
            .Add(c => c.OnTaskUpdate, (GanttTaskUpdate _) => { fired = true; }));

        await cut.InvokeAsync(() => cut.Instance.CommitProgress("t1", 75));

        Assert.False(fired);
    }

    [Fact]
    public async Task CommitCreate_NoOps_When_Readonly_Even_When_AllowCreate_True()
    {
        // The original guard checked ONLY AllowCreate — a chart with BOTH
        // Readonly=true AND AllowCreate=true was mutable via a direct
        // JSInvokable call. Readonly must win unconditionally.
        var fired = false;
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.Readonly, true)
            .Add(c => c.AllowCreate, true)
            .Add(c => c.OnTaskUpdate, (GanttTaskUpdate _) => { fired = true; }));

        await cut.InvokeAsync(() => cut.Instance.CommitCreate("task:t1", "2026-01-05", "2026-01-06"));

        Assert.False(fired);
    }

    [Fact]
    public async Task NotifyTaskClick_NoOps_When_Readonly()
    {
        var fired = false;
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.Readonly, true)
            .Add(c => c.OnTaskClick, (L.GanttTask _) => { fired = true; }));

        await cut.InvokeAsync(() => cut.Instance.NotifyTaskClick("t1"));

        Assert.False(fired);
    }

    [Fact]
    public void ValidateDrop_Returns_True_When_Readonly_Even_When_CanDrop_Would_Reject()
    {
        var canDropInvoked = false;
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.Readonly, true)
            .Add(c => c.CanDrop, (L.GanttTask _, GanttScheduleDropContext _) => { canDropInvoked = true; return false; }));

        var result = cut.Instance.ValidateDrop("t1", "move", "2026-01-05", "2026-01-09");

        Assert.True(result);
        Assert.False(canDropInvoked);
    }

    [Fact]
    public async Task Mid_Drag_Readonly_Flip_CommitDrag_Still_NoOps()
    {
        // Real reachable path: a drag starts while interactive, the chart
        // flips Readonly mid-drag (e.g. a concurrent save-in-progress state),
        // and the in-flight drag's own JS closure still calls CommitDrag on
        // release — this must no-op just like starting from Readonly=true.
        var fired = false;
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.Readonly, false)
            .Add(c => c.OnTaskUpdate, (GanttTaskUpdate _) => { fired = true; }));

        cut.Render(p => p.Add(c => c.Readonly, true));

        await cut.InvokeAsync(() => cut.Instance.CommitDrag("t1", "move", "2026-01-05", "2026-01-09"));

        Assert.False(fired);
    }
}
