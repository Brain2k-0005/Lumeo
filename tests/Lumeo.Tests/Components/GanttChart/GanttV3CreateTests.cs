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

    // ── Codex P2 finding ("Make group-header tracks hittable") ──────────────
    //
    // The group-header stripe (.lumeo-gantt-v3-group-header) paints ON TOP OF
    // and is a DOM SIBLING of (not a descendant of) its own row's underlying
    // [data-gantt-row-track] div — gantt-v3.js's onPointerDown hit-tests via
    // e.target.closest('[data-gantt-row-track]'), which a pointer on the
    // stripe could never satisfy, so AllowCreate's drag-create path was
    // unreachable for any grouped row. The stripe itself must now carry the
    // SAME data-gantt-row-track/data-row-key pair, gated identically.

    [Fact]
    public void Group_Header_Stripe_Carries_Row_Track_Markup_When_AllowCreate_True()
    {
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
            .Add(c => c.AllowCreate, true));

        var stripe = cut.Find(".lumeo-gantt-v3-group-header");
        Assert.Equal("true", stripe.GetAttribute("data-gantt-row-track"));
        Assert.Equal(headerKey, stripe.GetAttribute("data-row-key"));
    }

    [Fact]
    public void Group_Header_Stripe_Carries_No_Row_Track_Markup_When_AllowCreate_False()
    {
        // Predicted wrong value if the fix's gating condition were dropped
        // (always attaching the attributes): "true"/the header key, even
        // though this chart never opted into AllowCreate at all — mirrors
        // No_Row_Track_Markup_When_AllowCreate_False's own guarantee that the
        // feature costs nothing when off.
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
            .Add(c => c.RangeEnd, D(2026, 1, 20)));

        var stripe = cut.Find(".lumeo-gantt-v3-group-header");
        Assert.Null(stripe.GetAttribute("data-gantt-row-track"));
        Assert.Null(stripe.GetAttribute("data-row-key"));
    }

    [Fact]
    public void Group_Header_Stripe_Carries_No_Row_Track_Markup_When_Readonly_Even_If_AllowCreate_True()
    {
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
            .Add(c => c.Readonly, true));

        var stripe = cut.Find(".lumeo-gantt-v3-group-header");
        Assert.Null(stripe.GetAttribute("data-gantt-row-track"));
        Assert.Null(stripe.GetAttribute("data-row-key"));
    }

    [Fact]
    public void Group_Header_Stripe_Is_The_Rows_Only_Row_Track_Element_Not_A_Duplicate_Of_The_Underlying_Div()
    {
        // E2E follow-up (Playwright strict-mode violation: a locator for
        // "[data-gantt-row-track][data-row-key='group::Phase 1']" resolved to
        // TWO elements — the stripe AND a blind per-row div underneath it that
        // no pointer could ever actually reach, since the stripe paints on
        // top and covers the identical rectangle). RowTrackItems now excludes
        // GroupHeader rows outright, so the group row has exactly ONE
        // [data-gantt-row-track] element: the stripe itself. A plain task row
        // is unaffected — it still gets its own underlying div (GanttBar
        // carries no such attribute of its own).
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
            .Add(c => c.AllowCreate, true));

        // Exactly 2 matches total: one per row (group header + task) — never
        // 3, which would mean the group row is claimed by two elements again.
        Assert.Equal(2, cut.FindAll("[data-gantt-row-track]").Count);

        // The specific key-scoped query an E2E/JS consumer actually issues
        // (Playwright's strict mode, and gantt-v3.js's own closest() lookup)
        // must resolve to exactly one element: the stripe.
        var headerKey = rows.Single(r => r.Kind == GanttRowKind.GroupHeader).ToggleKey!;
        var headerTrack = cut.FindAll($"[data-gantt-row-track][data-row-key='{headerKey}']");
        var single = Assert.Single(headerTrack);
        Assert.Contains("lumeo-gantt-v3-group-header", single.ClassList);
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

    // ── GanttChart end-to-end (bubbled through the nested GanttTimeline) ─────────

    [Fact]
    public async Task GanttChart_Create_Commit_Fires_OnTaskCreate_And_OnTaskUpdate_Not_OnDateChange()
    {
        L.GanttTask? created = null;
        var dateChangedFired = false;
        GanttTaskUpdate? taskUpdate = null;

        var cut = _ctx.Render<L.GanttChart>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.AllowCreate, true)
            .Add(c => c.OnTaskCreate, (GanttTaskUpdate u) => { created = u.Task; })
            .Add(c => c.OnDateChange, (L.GanttTask _) => { dateChangedFired = true; })
            .Add(c => c.OnTaskUpdate, (GanttTaskUpdate u) => { taskUpdate = u; return true; }));

        var timeline = cut.FindComponent<L.GanttTimeline>();
        await cut.InvokeAsync(() => timeline.Instance.CommitCreate("task:t1", "2026-01-05", "2026-01-06"));

        Assert.NotNull(created);
        Assert.False(dateChangedFired);
        Assert.NotNull(taskUpdate);
        Assert.Equal(GanttTaskUpdateSource.Create, taskUpdate!.Source);
    }

    [Fact]
    public async Task GanttChart_Create_Commit_Appends_To_TasksChanged_Uncontrolled()
    {
        IEnumerable<L.GanttTask>? pushed = null;

        var cut = _ctx.Render<L.GanttChart>(p => p
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
    public async Task GanttChart_Create_Commit_Controlled_Veto_Reverts_No_Ghost_Task_Lingers()
    {
        // A controlled parent that ignores TasksChanged (keeps its own,
        // pre-create Tasks value) must roll the created task back — same veto
        // mechanism T1 already covers for a drag commit.
        var cut = _ctx.Render<L.GanttChart>(p => p
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

    // ── Codex PR-383 finding (GanttChart.razor, blocked until PR #382 merged) ────

    [Fact]
    public async Task GanttChart_Create_Commit_Inserts_New_Task_Into_Its_Inherited_Groups_Existing_Run()
    {
        // [P2] "Insert created tasks into their inherited group" — for a
        // grouped chart (GroupBy set), HandleTaskUpdateAsync used to append
        // every Create at the very end of the list regardless of which group
        // it inherited. BuildFlatGroupRows detects a group from CONSECUTIVE
        // same-GroupLabel tasks, so appending a "Group A" task after a "Group B"
        // one split Group A into two runs (a duplicate header) instead of
        // joining it to Group A's existing one. An uncontrolled parent (this
        // test) never echoes a re-sorted TasksChanged value back to repair it.
        var tasks = new List<L.GanttTask>
        {
            new("g1a", "G1 Task A", D(2026, 1, 1), D(2026, 1, 3)) { GroupLabel = "Group A" },
            new("g2a", "G2 Task A", D(2026, 2, 1), D(2026, 2, 3)) { GroupLabel = "Group B" },
        };

        IEnumerable<L.GanttTask>? pushed = null;
        var cut = _ctx.Render<L.GanttChart>(p => p
            .Add(c => c.Tasks, tasks)
            .Add(c => c.GroupBy, (Func<L.GanttTask, string>)(t => t.GroupLabel ?? ""))
            .Add(c => c.AllowCreate, true)
            .Add(c => c.TasksChanged, (IEnumerable<L.GanttTask> t) => { pushed = t; }));

        var timeline = cut.FindComponent<L.GanttTimeline>();
        // Create beside the Group A leaf — the new task inherits GroupLabel
        // "Group A" (already covered by CommitCreate_Leaf_Row_Flat_Group_
        // Inherits_GroupLabel_Sibling above).
        await cut.InvokeAsync(() => timeline.Instance.CommitCreate("task:g1a", "2026-01-05", "2026-01-06"));

        Assert.NotNull(pushed);
        var list = pushed!.ToList();
        Assert.Equal(3, list.Count);

        // The new task must land WITHIN Group A's own run (immediately after
        // g1a, before Group B's g2a) — not appended after everything, which
        // would put g2a ("Group B") at index 1 instead of the new task.
        Assert.Equal("Group A", list[0].GroupLabel);
        Assert.Equal("Group A", list[1].GroupLabel);
        Assert.Equal("g2a", list[2].Id);
        Assert.Equal("Group B", list[2].GroupLabel);
    }

    [Fact]
    public void GanttChart_AllowCreate_Passthrough_Reaches_The_Nested_Timeline()
    {
        var cut = _ctx.Render<L.GanttChart>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.AllowCreate, true));

        var timeline = cut.FindComponent<L.GanttTimeline>();
        Assert.True(timeline.Instance.AllowCreate);
        Assert.Single(cut.FindAll("[data-gantt-row-track]"));
    }

    // ── Codex full-review findings (post PR #382/#383 gates) ─────────────────

    [Fact]
    public async Task GanttChart_Uncontrolled_Drag_Edit_Survives_A_GroupBy_Change_With_The_Same_Stale_Tasks_Parameter()
    {
        // [P1] "Preserve uncontrolled edits when GroupBy changes" — parentMoved
        // is meant to detect whether the Tasks PARAMETER itself changed, but
        // hashing the SortedTasks (GroupBy-applied) list made a GroupBy
        // change alone — Tasks itself untouched — look like a genuine
        // parameter change (SortedTasks reorders by whatever GroupBy is
        // CURRENT), discarding the uncontrolled drag edit that had already
        // reconciled to _state.Tasks.
        var tasks = new List<L.GanttTask>
        {
            new("t1", "T1", D(2026, 1, 2), D(2026, 1, 6)) { GroupLabel = "A" },
            new("t2", "T2", D(2026, 2, 2), D(2026, 2, 6)) { GroupLabel = "B" },
        };
        Func<L.GanttTask, string> groupByAsc = t => t.GroupLabel ?? "";
        var cut = _ctx.Render<L.GanttChart>(p => p
            .Add(c => c.Tasks, tasks)
            .Add(c => c.GroupBy, groupByAsc));

        var timeline = cut.FindComponent<L.GanttTimeline>();
        await cut.InvokeAsync(() => timeline.Instance.CommitDrag("t1", "move", "2026-01-05", "2026-01-09"));
        Assert.Equal("2026-01-05", cut.Find("[data-task-id='t1']").GetAttribute("data-task-start"));

        // Re-render with the SAME (stale, pre-drag) Tasks list, but a
        // DIFFERENT GroupBy delegate that flips the two groups' relative
        // sort order (t1's key "Z" now sorts AFTER t2's key "A") — the exact
        // reordering that made SortedTasks-based hashing see a "change".
        Func<L.GanttTask, string> groupByDesc = t => t.GroupLabel == "A" ? "Z" : "A";
        cut.Render(p => p.Add(c => c.Tasks, tasks).Add(c => c.GroupBy, groupByDesc));

        Assert.Equal("2026-01-05", cut.Find("[data-task-id='t1']").GetAttribute("data-task-start"));
    }

    [Fact]
    public void BuildDragOptions_Includes_ScaleUnit_Matching_The_Active_View_Mode()
    {
        // [P2] "Map drag-create pixels through the active calendar scale" —
        // gantt-v3.js needs the active GanttScaleUnit to pick the correct
        // Month/Year-aware (vs linear day-based) column-to-date formula for
        // drag-create's commit dates; the exact JS math itself is covered by
        // the E2E suite (bUnit never executes gantt-v3.js), but the C# side
        // of the wire contract — this options bag actually carrying the
        // right value per mode — is directly testable here.
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.ViewMode, L.GanttViewMode.Month)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 6, 1))
            .Add(c => c.AllowCreate, true));

        var options = Assert.IsType<Dictionary<string, object?>>(_interop.LastGanttV3DragOptions);
        Assert.Equal("Month", options["scaleUnit"]);
    }

    [Fact]
    public void Row_Track_Div_Carries_Touch_Pan_Y_When_AllowCreate_True()
    {
        // [P2] "Opt touch drag targets out of native horizontal panning" —
        // see GanttBar.WrapperClass's own remarks on the identical bar-drag
        // fix. Unconditional here since this whole block is already gated on
        // AllowCreate && !Readonly.
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.AllowCreate, true));

        var track = cut.Find("[data-gantt-row-track]");
        Assert.Contains("touch-pan-y", track.ClassList);
    }
}
