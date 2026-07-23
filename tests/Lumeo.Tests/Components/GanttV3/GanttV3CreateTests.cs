using Bunit;
using Lumeo.GanttV3;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.GanttV3;

/// <summary>
/// Gantt v3 Phase 2, T3 — drag-create on an empty track (<c>CommitCreate</c>).
/// Same headless-DOM limitation T1/T2's reports document: gantt-v3.js's
/// pointer/ghost geometry itself (the row-track hit-test, the empty-track
/// ghost paint) never executes in bUnit — that is T4's Playwright coverage.
/// This file exercises the .NET-side seams: the JSInvokable CommitCreate
/// resolves a row-key, builds the correct proposed GanttTask (id shape,
/// localized name, inherited group/parent per the row-context rules
/// GanttTimeline.ResolveCreateContext documents), fires OnTaskCreate alongside
/// OnTaskUpdate, folds into TasksChanged (append, not merge-by-id), and
/// respects AllowCreate/Readonly gating on the options payload + row-track markup.
/// </summary>
public class GanttV3CreateTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public GanttV3CreateTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static DateTime D(int y, int m, int d) => new(y, m, d);
    private static L.GanttTask Task1 => new("t1", "Design", D(2026, 1, 2), D(2026, 1, 6));

    // ── AllowCreate / Readonly gating (options payload + row-track markup) ───

    [Fact]
    public void BuildDragOptions_AllowCreate_False_By_Default()
    {
        _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10)));

        var options = Assert.IsType<Dictionary<string, object?>>(_interop.LastGanttV3DragOptions);
        Assert.Equal(false, options["allowCreate"]);
    }

    [Fact]
    public void BuildDragOptions_AllowCreate_True_When_Set()
    {
        _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.AllowCreate, true));

        var options = Assert.IsType<Dictionary<string, object?>>(_interop.LastGanttV3DragOptions);
        Assert.Equal(true, options["allowCreate"]);
        Assert.Equal("2026-01-01", options["originIso"]);
    }

    [Fact]
    public void No_Row_Track_Markup_When_AllowCreate_False()
    {
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10)));

        Assert.Empty(cut.FindAll("[data-gantt-row-track]"));
    }

    [Fact]
    public void Row_Track_Markup_Rendered_When_AllowCreate_True()
    {
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.AllowCreate, true));

        var tracks = cut.FindAll("[data-gantt-row-track]");
        Assert.Single(tracks);
        Assert.Equal("task:t1", tracks[0].GetAttribute("data-row-key"));
    }

    [Fact]
    public void Readonly_Wins_No_Row_Track_Markup_Even_When_AllowCreate_True()
    {
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.AllowCreate, true)
            .Add(c => c.Readonly, true));

        Assert.Empty(cut.FindAll("[data-gantt-row-track]"));
    }

    [Fact]
    public void Readonly_Wins_No_Drag_Interop_At_All_Even_When_AllowCreate_True()
    {
        _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.AllowCreate, true)
            .Add(c => c.Readonly, true));

        Assert.Equal(0, _interop.GanttV3RegisterDragCallCount);
    }

    // ── GanttTimeline.CommitCreate (JSInvokable) ─────────────────────────────

    [Fact]
    public async Task CommitCreate_NoOps_When_AllowCreate_False()
    {
        var fired = false;
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.OnTaskUpdate, (GanttTaskUpdate _) => { fired = true; }));

        await cut.InvokeAsync(() => cut.Instance.CommitCreate("task:t1", "2026-01-05", "2026-01-06"));

        Assert.False(fired);
    }

    [Fact]
    public async Task CommitCreate_Leaf_Row_Inherits_Sibling_ParentId()
    {
        // Hierarchy mode (T is a child of "parent"): creating on the LEAF row
        // "t1" must make the new task t1's SIBLING (same ParentId), not a
        // child of t1.
        GanttTaskUpdate? received = null;
        var tasks = new List<L.GanttTask>
        {
            new("root", "Root", D(2026, 1, 1), D(2026, 1, 10)),
            new("t1", "Design", D(2026, 1, 2), D(2026, 1, 6)) { ParentId = "root" },
        };
        var rows = GanttRowModel.BuildVisibleRows(tasks, new HashSet<string>());

        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, tasks)
            .Add(c => c.Rows, rows)
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 20))
            .Add(c => c.AllowCreate, true)
            .Add(c => c.OnTaskUpdate, (GanttTaskUpdate u) => { received = u; }));

        await cut.InvokeAsync(() => cut.Instance.CommitCreate("task:t1", "2026-01-05", "2026-01-06"));

        Assert.NotNull(received);
        Assert.Equal(GanttTaskUpdateSource.Create, received!.Source);
        Assert.Equal("root", received.Task.ParentId);
        Assert.Equal(D(2026, 1, 5), received.Task.Start);
        Assert.Equal(D(2026, 1, 6), received.Task.End);
        Assert.Equal(32, received.Task.Id.Length); // Guid "N" format
        Assert.Equal("New task", received.Task.Name);
    }

    [Fact]
    public async Task CommitCreate_Summary_Row_Becomes_ParentId_Of_New_Task()
    {
        // Hierarchy mode: creating on the SUMMARY row "root" (HasChildren=true)
        // must make the new task a CHILD of root, not root's sibling.
        GanttTaskUpdate? received = null;
        var tasks = new List<L.GanttTask>
        {
            new("root", "Root", D(2026, 1, 1), D(2026, 1, 10)),
            new("t1", "Design", D(2026, 1, 2), D(2026, 1, 6)) { ParentId = "root" },
        };
        var rows = GanttRowModel.BuildVisibleRows(tasks, new HashSet<string>());

        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, tasks)
            .Add(c => c.Rows, rows)
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 20))
            .Add(c => c.AllowCreate, true)
            .Add(c => c.OnTaskUpdate, (GanttTaskUpdate u) => { received = u; }));

        await cut.InvokeAsync(() => cut.Instance.CommitCreate("task:root", "2026-01-05", "2026-01-06"));

        Assert.NotNull(received);
        Assert.Equal("root", received!.Task.ParentId);
    }

    [Fact]
    public async Task CommitCreate_Group_Header_Row_Inherits_GroupLabel()
    {
        // Flat-grouping mode (no ParentId in play): creating on the group HEADER
        // row must set GroupLabel to that group, with no ParentId.
        GanttTaskUpdate? received = null;
        var tasks = new List<L.GanttTask>
        {
            new("t1", "Design", D(2026, 1, 2), D(2026, 1, 6)) { GroupLabel = "Phase 1" },
        };
        var rows = GanttRowModel.BuildVisibleRows(tasks, new HashSet<string>());
        var headerKey = rows.Single(r => r.Kind == GanttRowKind.GroupHeader).ToggleKey!;

        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, tasks)
            .Add(c => c.Rows, rows)
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 20))
            .Add(c => c.AllowCreate, true)
            .Add(c => c.OnTaskUpdate, (GanttTaskUpdate u) => { received = u; }));

        await cut.InvokeAsync(() => cut.Instance.CommitCreate(headerKey, "2026-01-05", "2026-01-06"));

        Assert.NotNull(received);
        Assert.Equal("Phase 1", received!.Task.GroupLabel);
        Assert.Null(received.Task.ParentId);
    }

    [Fact]
    public async Task CommitCreate_Leaf_Row_Flat_Group_Inherits_GroupLabel_Sibling()
    {
        GanttTaskUpdate? received = null;
        var tasks = new List<L.GanttTask>
        {
            new("t1", "Design", D(2026, 1, 2), D(2026, 1, 6)) { GroupLabel = "Phase 1" },
        };
        var rows = GanttRowModel.BuildVisibleRows(tasks, new HashSet<string>());

        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, tasks)
            .Add(c => c.Rows, rows)
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 20))
            .Add(c => c.AllowCreate, true)
            .Add(c => c.OnTaskUpdate, (GanttTaskUpdate u) => { received = u; }));

        await cut.InvokeAsync(() => cut.Instance.CommitCreate("task:t1", "2026-01-05", "2026-01-06"));

        Assert.NotNull(received);
        Assert.Equal("Phase 1", received!.Task.GroupLabel);
        Assert.Null(received.Task.ParentId);
    }

    [Fact]
    public async Task CommitCreate_Unknown_RowKey_Fires_Nothing()
    {
        var fired = false;
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.AllowCreate, true)
            .Add(c => c.OnTaskUpdate, (GanttTaskUpdate _) => { fired = true; }));

        await cut.InvokeAsync(() => cut.Instance.CommitCreate("task:nope", "2026-01-05", "2026-01-06"));

        Assert.False(fired);
    }

    [Fact]
    public async Task CommitCreate_Invalid_Date_String_Fires_Nothing()
    {
        var fired = false;
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.AllowCreate, true)
            .Add(c => c.OnTaskUpdate, (GanttTaskUpdate _) => { fired = true; }));

        await cut.InvokeAsync(() => cut.Instance.CommitCreate("task:t1", "not-a-date", "2026-01-06"));

        Assert.False(fired);
    }

    // ── Gantt3 end-to-end (bubbled through the nested GanttTimeline) ─────────

    [Fact]
    public async Task Gantt3_Create_Commit_Fires_OnTaskCreate_And_OnTaskUpdate_Not_OnDateChange()
    {
        L.GanttTask? created = null;
        var dateChangedFired = false;
        GanttTaskUpdate? taskUpdate = null;

        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.AllowCreate, true)
            .Add(c => c.OnTaskCreate, (GanttTaskUpdate u) => { created = u.Task; })
            .Add(c => c.OnDateChange, (L.GanttTask _) => { dateChangedFired = true; })
            .Add(c => c.OnTaskUpdate, (GanttTaskUpdate u) => { taskUpdate = u; }));

        var timeline = cut.FindComponent<L.GanttTimeline>();
        await cut.InvokeAsync(() => timeline.Instance.CommitCreate("task:t1", "2026-01-05", "2026-01-06"));

        Assert.NotNull(created);
        Assert.False(dateChangedFired);
        Assert.NotNull(taskUpdate);
        Assert.Equal(GanttTaskUpdateSource.Create, taskUpdate!.Source);
    }

    [Fact]
    public async Task Gantt3_Create_Commit_Appends_To_TasksChanged_Uncontrolled()
    {
        IEnumerable<L.GanttTask>? pushed = null;

        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.AllowCreate, true)
            .Add(c => c.TasksChanged, (IEnumerable<L.GanttTask> t) => { pushed = t; }));

        var timeline = cut.FindComponent<L.GanttTimeline>();
        await cut.InvokeAsync(() => timeline.Instance.CommitCreate("task:t1", "2026-01-05", "2026-01-06"));

        Assert.NotNull(pushed);
        var list = pushed!.ToList();
        Assert.Equal(2, list.Count);
        Assert.Contains(list, t => t.Id == "t1");
        Assert.Single(list, t => t.Id != "t1");
    }

    [Fact]
    public async Task Gantt3_Create_Commit_Controlled_Veto_Reverts_No_Ghost_Task_Lingers()
    {
        // A controlled parent that ignores TasksChanged (keeps its own,
        // pre-create Tasks value) must roll the created task back — same veto
        // mechanism T1 already covers for a drag commit.
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.AllowCreate, true)
            .Add(c => c.TasksChanged, (IEnumerable<L.GanttTask> _) => { /* veto: parent keeps its own value */ }));

        var timeline = cut.FindComponent<L.GanttTimeline>();
        await cut.InvokeAsync(() => timeline.Instance.CommitCreate("task:t1", "2026-01-05", "2026-01-06"));

        // Re-render with the SAME (original, pre-create) Tasks parameter value —
        // mirrors an unrelated parent re-render that never accepted the push.
        cut.Render(p => p.Add(c => c.Tasks, new List<L.GanttTask> { Task1 }));

        Assert.Single(cut.FindAll("[data-task-id]"));
        Assert.Equal("t1", cut.Find("[data-task-id]").GetAttribute("data-task-id"));
    }

    [Fact]
    public void Gantt3_AllowCreate_Passthrough_Reaches_The_Nested_Timeline()
    {
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.AllowCreate, true));

        var timeline = cut.FindComponent<L.GanttTimeline>();
        Assert.True(timeline.Instance.AllowCreate);
        Assert.Single(cut.FindAll("[data-gantt-row-track]"));
    }
}
