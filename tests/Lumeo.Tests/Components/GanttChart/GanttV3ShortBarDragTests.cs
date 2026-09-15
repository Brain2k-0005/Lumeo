using System.Reflection;
using Bunit;
using Lumeo.GanttV3;
using Lumeo.Tests.Helpers;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.GanttV3;

/// <summary>
/// Field report #400 — "one bar (interviews, the shortest in its demo)
/// reproducibly refuses to drag". The report's own lead suspected a hit-test/
/// geometry problem (a minimum-width or resize-handle issue, or the
/// module-level active-drag-gesture counter in gantt-v3.js) specific to that
/// one bar.
///
/// Investigation (this file, plus tests/js/gantt-v3-hit-test.test.mjs):
///
/// 1. <see cref="Docs_Demo_Interviews_Bar_Is_Not_The_Narrowest_Bar_In_Its_Own_Demo"/>
///    renders the EXACT task list docs/Lumeo.Docs/Pages/Components/
///    GanttChartPage.razor's BuildBasicTasks() builds (the flagship "A
///    realistic project plan" demo AND the "Drag to reschedule" commit-gate
///    demo both use it) and reads the REAL rendered <c>--lumeo-gantt-bar-w</c>
///    geometry GanttScale.BarGeometry computed for every bar. At the demo's
///    own Day view (38px/column — GanttScale.ViewModes[Day]), "interviews"
///    renders 190px wide — NOT the narrowest bar in its own demo ("qa" is
///    152px; "requirements" ties it at 190px). So the report's own "it's the
///    shortest bar" premise does not hold for THIS demo at its own default
///    zoom, and resolveHitMode's RESIZE_HANDLE_PX=6px zones (12px combined)
///    are nowhere close to consuming a 190px-wide bar — the hit-test/
///    minimum-width theory is refuted for this specific report, with
///    evidence.
///
/// 2. <see cref="Docs_Gate_Demo_Rejects_An_Ordinary_Drag_Of_Interviews_Because_Its_New_Start_Is_Still_Before_Today"/>
///    finds the ACTUAL mechanism: the "Drag to reschedule — with a real
///    commit gate" demo's own <c>HandleGateTaskUpdate</c> (replicated here
///    verbatim from the docs page) rejects any commit whose proposed Start
///    precedes DateTime.Today — its own demo copy says so explicitly ("this
///    demo rejects anything that would start before today"). "interviews"
///    starts 10 days before today (<c>BuildBasicTasks</c>'s own
///    <c>d.AddDays(-10)</c>) — same as "discovery" and "requirements", both
///    also in that demo. An ordinary small drag (the kind a user testing
///    "does this bar drag" would naturally try) still lands the proposed
///    Start before today, so the gate correctly, deterministically REJECTS
///    it every single time: no state change, the bar visually snaps back —
///    exactly "refuses to drag" from the outside, with OnTaskUpdate firing
///    (and rejecting) exactly as designed, not a broken hit-test.
///
/// 3. <see cref="Docs_Gate_Demo_Accepts_A_Drag_Of_Interviews_That_Clears_Today"/>
///    is the control: the SAME bar, SAME gate, a drag large enough that the
///    proposed Start lands on/after today, commits successfully — proving
///    "interviews" itself is perfectly draggable; only drags that don't
///    clear the gate's own documented "before today" rule are rejected.
///
/// 4. <see cref="Docs_Gate_Demo_Rejects_The_Same_Ordinary_Drag_For_Requirements_Too_Not_Just_Interviews"/>
///    shows the rejection is NOT specific to "interviews" — "requirements"
///    (also past-dated, also 190px wide) is rejected by the identical
///    ordinary-drag test, confirming this is the gate's date rule acting on
///    every past-dated bar equally, not a bug isolated to one bar.
///
/// Verdict: #400 is NOT REPRODUCIBLE as a bug in the demo the report names —
/// it is the commit-gate demo's own documented, intentional behavior. See
/// this file's own report for the separate (real, but not the cause of
/// #400) narrow-bar hit-test defect fixed in gantt-v3.js's resolveHitMode
/// alongside this investigation.
/// </summary>
public class GanttV3ShortBarDragTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public GanttV3ShortBarDragTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // Verbatim port of docs/Lumeo.Docs/Pages/Components/GanttChartPage.razor's
    // own BuildBasicTasks() — the ONLY demo data set on that page containing
    // an "interviews" task, used by both the flagship demo (Tasks, no gate)
    // and the "Drag to reschedule" commit-gate demo (@bind-Tasks, OnTaskUpdate
    // = HandleGateTaskUpdate) this investigation is about.
    private static List<L.GanttTask> BuildBasicTasks()
    {
        var d = DateTime.Today;
        return new List<L.GanttTask>
        {
            new("discovery",  "Discovery & Planning", d.AddDays(-10), d.AddDays(-1), Progress: 100),
            new("interviews", "Stakeholder interviews", d.AddDays(-10), d.AddDays(-6), Progress: 100) { ParentId = "discovery" },
            new("requirements", "Requirements doc", d.AddDays(-7), d.AddDays(-3), Progress: 100, Dependencies: new[] { "interviews" }) { ParentId = "discovery" },
            new("m-kickoff", "Kickoff approved", d.AddDays(-1), d.AddDays(-1), IsMilestone: true, Dependencies: new[] { "requirements" }) { ParentId = "discovery" },
            new("design",     "Design", d.AddDays(-1), d.AddDays(8), Progress: 55),
            new("wireframes", "Wireframes", d.AddDays(-1), d.AddDays(3), Progress: 80, Dependencies: new[] { "m-kickoff" }) { ParentId = "design" },
            new("visualDesign", "Visual design", d.AddDays(2), d.AddDays(7), Progress: 40, Dependencies: new[] { "wireframes" }) { ParentId = "design" },
            new("m-designReview", "Design signed off", d.AddDays(8), d.AddDays(8), IsMilestone: true, Dependencies: new[] { "visualDesign" }) { ParentId = "design" },
            new("build",     "Build", d.AddDays(9), d.AddDays(25), Progress: 12),
            new("frontend",  "Frontend build", d.AddDays(9), d.AddDays(20), Progress: 15, Dependencies: new[] { "m-designReview" }) { ParentId = "build" },
            new("backend",   "Backend build", d.AddDays(9), d.AddDays(22), Progress: 20, Dependencies: new[] { "m-designReview" }) { ParentId = "build" },
            new("integration", "Integration", d.AddDays(20), d.AddDays(25), Progress: 0, Dependencies: new[] { "frontend", "backend" }) { ParentId = "build" },
            new("launch",    "Launch", d.AddDays(25), d.AddDays(30), Progress: 0),
            new("qa",        "QA & regression", d.AddDays(25), d.AddDays(28), Progress: 0, Dependencies: new[] { "integration" }) { ParentId = "launch" },
            new("m-golive",  "Go-live", d.AddDays(30), d.AddDays(30), IsMilestone: true, Dependencies: new[] { "qa" }) { ParentId = "launch" },
        };
    }

    // Verbatim port of GanttChartPage.razor's own HandleGateTaskUpdate (minus
    // the _gateLog/StateHasChanged side effects, which are purely UI logging
    // and irrelevant to the accept/reject/adjust verdict under test).
    private static GanttUpdateResult HandleGateTaskUpdate(GanttTaskUpdate update)
    {
        if (update.Task.Start < DateTime.Today) return false;

        if (update.Task.Start.DayOfWeek != DayOfWeek.Monday)
        {
            var offset = ((int)update.Task.Start.DayOfWeek + 6) % 7;
            var snappedStart = update.Task.Start.AddDays(-offset);
            var snappedEnd = update.Task.End.AddDays(-offset);
            return GanttUpdateResult.AcceptWith(new GanttUpdateAdjustment(Start: snappedStart, End: snappedEnd));
        }

        return true;
    }

    private static GanttState State(IRenderedComponent<L.GanttChart> cut) =>
        (GanttState)typeof(L.GanttChart)
            .GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(cut.Instance)!;

    [Fact]
    public void Docs_Demo_Interviews_Bar_Is_Not_The_Narrowest_Bar_In_Its_Own_Demo()
    {
        var cut = _ctx.Render<L.GanttChart>(p => p
            .Add(c => c.Tasks, BuildBasicTasks())
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.Height, "420px"));

        double WidthOf(string taskId)
        {
            var style = cut.Find($"[data-task-id='{taskId}']").GetAttribute("style") ?? "";
            var marker = "--lumeo-gantt-bar-w:";
            var start = style.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
            var end = style.IndexOf("px", start, StringComparison.Ordinal);
            return double.Parse(style[start..end]);
        }

        var interviewsWidth = WidthOf("interviews");
        var requirementsWidth = WidthOf("requirements");
        var qaWidth = WidthOf("qa");

        // At Day view (38px/column), a 5-rendered-day bar (end-inclusive +1
        // day, GanttScale.BarGeometry) is 190px — the SAME width as
        // "requirements", and WIDER than "qa" (3 rendered days, 152px). Both
        // numbers refute "interviews is the shortest bar in its demo".
        Assert.Equal(190d, interviewsWidth);
        Assert.Equal(190d, requirementsWidth);
        Assert.Equal(152d, qaWidth);
        Assert.True(qaWidth < interviewsWidth, "qa is narrower than interviews in this demo — interviews is not the shortest bar.");

        // Nowhere near resolveHitMode's RESIZE_HANDLE_PX*2 (12px) danger
        // line either way — the hit-test/minimum-width theory doesn't apply
        // to this bar at this demo's own (fixed, no zoom control) Day view.
        Assert.True(interviewsWidth > 12);
    }

    [Fact]
    public async Task Docs_Gate_Demo_Rejects_An_Ordinary_Drag_Of_Interviews_Because_Its_New_Start_Is_Still_Before_Today()
    {
        GanttTaskUpdate? taskUpdate = null;
        var cut = _ctx.Render<L.GanttChart>(p => p
            .Add(c => c.Tasks, BuildBasicTasks())
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.Height, "380px")
            .Add(c => c.OnTaskUpdate, (GanttTaskUpdate u) => { taskUpdate = u; return HandleGateTaskUpdate(u); }));

        var timeline = cut.FindComponent<L.GanttTimeline>();

        // A modest 3-day forward drag — the kind of small, exploratory drag
        // a user "just checking it works" would make. interviews started 10
        // days before today, so its new Start (7 days before today) is
        // STILL before today.
        var d = DateTime.Today;
        var newStart = d.AddDays(-7);
        var newEnd = d.AddDays(-3);
        await cut.InvokeAsync(() => timeline.Instance.CommitDrag(
            "interviews", "move", newStart.ToString("yyyy-MM-dd"), newEnd.ToString("yyyy-MM-dd")));

        // The gate WAS asked (OnTaskUpdate fired, with the real "interviews"
        // task and its proposed new dates) — this is not a dead hit-test.
        Assert.NotNull(taskUpdate);
        Assert.Equal("interviews", taskUpdate!.Task.Id);
        Assert.Equal(GanttTaskUpdateSource.Move, taskUpdate.Source);
        Assert.Equal(newStart.Date, taskUpdate.Task.Start.Date);

        // ...but the gate rejected it (Start still before today), so NOTHING
        // committed: the underlying task is untouched, and the rendered bar
        // never moved — exactly "refuses to drag" from the outside.
        var committed = State(cut).Tasks.Single(t => t.Id == "interviews");
        Assert.Equal(d.AddDays(-10).Date, committed.Start.Date);
        Assert.Equal(d.AddDays(-6).Date, committed.End.Date);
        Assert.Equal(d.AddDays(-10).ToString("yyyy-MM-dd"), cut.Find("[data-task-id='interviews']").GetAttribute("data-task-start"));
    }

    [Fact]
    public async Task Docs_Gate_Demo_Accepts_A_Drag_Of_Interviews_That_Clears_Today()
    {
        var cut = _ctx.Render<L.GanttChart>(p => p
            .Add(c => c.Tasks, BuildBasicTasks())
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.Height, "380px")
            .Add(c => c.OnTaskUpdate, (GanttTaskUpdate u) => HandleGateTaskUpdate(u)));

        var timeline = cut.FindComponent<L.GanttTimeline>();

        // A drag landing exactly on a FUTURE Monday: the gate's Monday-snap
        // branch snaps BACKWARD to the most recent Monday on/before the
        // proposed Start (see HandleGateTaskUpdate's own `AddDays(-offset)`),
        // so a proposal that merely clears "today" without itself being a
        // Monday can snap back to a date before today again — a real quirk
        // of the demo's own gate, not something this test is about. Landing
        // exactly on a Monday sidesteps the snap branch entirely (the
        // gate's final `else` — Accept, unmodified) for a clean, exact
        // assertion.
        var d = DateTime.Today;
        var daysToMonday = ((int)DayOfWeek.Monday - (int)d.DayOfWeek + 7) % 7;
        var nextMonday = d.AddDays(daysToMonday == 0 ? 7 : daysToMonday); // strictly future, always a Monday
        var newStart = nextMonday;
        var newEnd = nextMonday.AddDays(4);
        await cut.InvokeAsync(() => timeline.Instance.CommitDrag(
            "interviews", "move", newStart.ToString("yyyy-MM-dd"), newEnd.ToString("yyyy-MM-dd")));

        var committed = State(cut).Tasks.Single(t => t.Id == "interviews");
        // The drag genuinely committed, exactly as proposed — interviews is
        // perfectly draggable; only drags that don't clear "Start >= today"
        // get rejected (see the Reject test above).
        Assert.Equal(nextMonday.Date, committed.Start.Date);
        Assert.Equal(newEnd.Date, committed.End.Date);
    }

    [Fact]
    public async Task Docs_Gate_Demo_Rejects_The_Same_Ordinary_Drag_For_Requirements_Too_Not_Just_Interviews()
    {
        var cut = _ctx.Render<L.GanttChart>(p => p
            .Add(c => c.Tasks, BuildBasicTasks())
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.Height, "380px")
            .Add(c => c.OnTaskUpdate, (GanttTaskUpdate u) => HandleGateTaskUpdate(u)));

        var timeline = cut.FindComponent<L.GanttTimeline>();

        // requirements: d-7 to d-3, same 190px width as interviews. A
        // 3-day forward drag lands at d-4 — still before today.
        var d = DateTime.Today;
        await cut.InvokeAsync(() => timeline.Instance.CommitDrag(
            "requirements", "move", d.AddDays(-4).ToString("yyyy-MM-dd"), d.AddDays(0).ToString("yyyy-MM-dd")));

        var committed = State(cut).Tasks.Single(t => t.Id == "requirements");
        Assert.Equal(d.AddDays(-7).Date, committed.Start.Date); // unchanged — rejected exactly like interviews was
    }
}
