using Bunit;
using Lumeo.GanttV3;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.GanttV3;

/// <summary>
/// Styling-hooks audit — a ReUI comparison found 0 of the 8 documented
/// <c>data-*</c> attributes (<c>data-selected</c>, <c>data-dragging</c>,
/// <c>data-progress</c>, <c>data-completed</c>, <c>data-past</c>,
/// <c>data-recurring</c>, <c>data-off</c>, <c>data-drop-invalid</c>) existed on
/// GanttV3 anywhere, under any name. This file covers the six that are pure
/// Blazor render state (<see cref="L.GanttBar"/>'s wrapper attributes, plus
/// <see cref="L.GanttTimeline"/>'s off-day header/canvas cells) and the
/// SelectedIds -&gt; GanttBar.Selected wiring for <c>data-selected</c>.
///
/// The other two are deliberately NOT covered here:
/// <list type="bullet">
///   <item><c>data-dragging</c> — set/cleared entirely by gantt-v3.js's live
///   pointer-event drag engine, with no Blazor round trip mid-gesture; a bUnit
///   render can't reach it at all. See
///   <c>GanttDragParityTests.Data_dragging_appears_once_the_drag_threshold_is_crossed_and_disappears_on_release</c>
///   (E2E) for the real proof.</item>
///   <item><c>data-drop-invalid</c> — lives on the JS-cloned drag-ghost
///   element, which only exists during a live gantt-v3.js gesture (no Blazor
///   markup for it at all). See <c>GanttDragParityTests</c>'s renamed
///   <c>CanDrop_*</c> specs (E2E) — the ghost previously carried the
///   equivalent state under the wrong name, <c>data-invalid="true"</c>.</item>
/// </list>
///
/// Every boolean-presence attribute below asserts PRESENCE/ABSENCE via
/// <c>GetAttribute</c> being non-null/null — never a class string, and never
/// a truthy/falsy STRING VALUE (a real regression this suite specifically
/// guards: an attribute rendered as <c>data-selected="False"</c> would still
/// make a naive <c>Assert.Contains("data-selected", markup)</c> pass while
/// being a broken <c>[data-selected]</c> CSS selector for a consumer).
/// </summary>
public class GanttV3StylingHooksTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public GanttV3StylingHooksTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static DateTime D(int y, int m, int d) => new(y, m, d);

    // ── data-progress (value-carrying, not boolean) ──────────────────────────

    [Fact]
    public void Data_Progress_Carries_The_Clamped_Numeric_Value()
    {
        var task = new L.GanttTask("t1", "Design", D(2026, 1, 2), D(2026, 1, 4), Progress: 40);
        var cut = _ctx.Render<L.GanttBar>(p => p
            .Add(c => c.Task, task).Add(c => c.X, 0d).Add(c => c.Width, 100d).Add(c => c.RowIndex, 0));

        // Predicted-wrong value if the hook were missing entirely: null (no
        // attribute at all) — this is exactly what "0 of 8 exist" meant before
        // this pass.
        Assert.Equal("40", cut.Find("[data-task-id='t1']").GetAttribute("data-progress"));
    }

    [Fact]
    public void Data_Progress_Clamps_An_Out_Of_Range_Value_The_Same_Way_The_Fill_Does()
    {
        var task = new L.GanttTask("t1", "Design", D(2026, 1, 2), D(2026, 1, 4), Progress: 150);
        var cut = _ctx.Render<L.GanttBar>(p => p
            .Add(c => c.Task, task).Add(c => c.X, 0d).Add(c => c.Width, 100d).Add(c => c.RowIndex, 0));

        // Predicted-wrong value from an UNCLAMPED read: "150". A consumer
        // reading this for a progress-bar-shaped CSS var would silently
        // overflow past 100% on any task whose Progress exceeds range.
        Assert.Equal("100", cut.Find("[data-task-id='t1']").GetAttribute("data-progress"));
    }

    // ── data-completed ────────────────────────────────────────────────────────

    [Fact]
    public void Data_Completed_Present_Only_At_Exactly_100_Percent()
    {
        var done = new L.GanttTask("t1", "Design", D(2026, 1, 2), D(2026, 1, 4), Progress: 100);
        var almost = new L.GanttTask("t2", "Design", D(2026, 1, 2), D(2026, 1, 4), Progress: 99);

        var doneCut = _ctx.Render<L.GanttBar>(p => p
            .Add(c => c.Task, done).Add(c => c.X, 0d).Add(c => c.Width, 100d).Add(c => c.RowIndex, 0));
        var almostCut = _ctx.Render<L.GanttBar>(p => p
            .Add(c => c.Task, almost).Add(c => c.X, 0d).Add(c => c.Width, 100d).Add(c => c.RowIndex, 0));

        Assert.Equal("", doneCut.Find("[data-task-id='t1']").GetAttribute("data-completed"));
        // Predicted-wrong value for a disable-check on the ClampedProgress==100
        // gate: "" present here too (i.e. any progress reads as completed).
        Assert.Null(almostCut.Find("[data-task-id='t2']").GetAttribute("data-completed"));
    }

    // ── data-past ─────────────────────────────────────────────────────────────

    [Fact]
    public void Data_Past_Present_When_The_Task_End_Precedes_Now_Absent_Otherwise()
    {
        var now = D(2026, 6, 15);
        var pastTask = new L.GanttTask("past", "Old", D(2026, 1, 1), D(2026, 1, 5));
        var futureTask = new L.GanttTask("future", "Upcoming", D(2026, 12, 1), D(2026, 12, 5));

        var pastCut = _ctx.Render<L.GanttBar>(p => p
            .Add(c => c.Task, pastTask).Add(c => c.X, 0d).Add(c => c.Width, 100d).Add(c => c.RowIndex, 0)
            .Add(c => c.Now, now));
        var futureCut = _ctx.Render<L.GanttBar>(p => p
            .Add(c => c.Task, futureTask).Add(c => c.X, 0d).Add(c => c.Width, 100d).Add(c => c.RowIndex, 0)
            .Add(c => c.Now, now));

        Assert.Equal("", pastCut.Find("[data-task-id='past']").GetAttribute("data-past"));
        // Predicted-wrong value for a Start-instead-of-End mixup: "" present
        // here too (futureTask's Start is also, trivially, not "now" — only
        // comparing against End distinguishes "finished" from "in progress").
        Assert.Null(futureCut.Find("[data-task-id='future']").GetAttribute("data-past"));
    }

    [Fact]
    public void Data_Past_Uses_The_Same_Now_GanttTimeline_Forwards_Not_A_Second_Independent_Clock()
    {
        // Regression for the plumbing itself, not just the boolean math:
        // GanttTimeline.EffectiveNow (Now ?? DateTime.Now) must reach the bar,
        // proven here by making the CALLER-supplied Now the ONLY thing that
        // could make this task read as past.
        var now = D(2026, 6, 15);
        var task = new L.GanttTask("t1", "Design", D(2026, 3, 1), D(2026, 3, 5));
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { task })
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 12, 31))
            .Add(c => c.Now, now));

        Assert.Equal("", cut.Find("[data-task-id='t1']").GetAttribute("data-past"));
    }

    // ── data-recurring ────────────────────────────────────────────────────────

    [Fact]
    public void Data_Recurring_Present_Only_When_IsRecurring_Is_Set()
    {
        var recurring = new L.GanttTask("t1", "Standup", D(2026, 1, 5), D(2026, 1, 5)) { IsRecurring = true };
        var plain = new L.GanttTask("t2", "Standup", D(2026, 1, 5), D(2026, 1, 5));

        var recurringCut = _ctx.Render<L.GanttBar>(p => p
            .Add(c => c.Task, recurring).Add(c => c.X, 0d).Add(c => c.Width, 22d).Add(c => c.RowIndex, 0));
        var plainCut = _ctx.Render<L.GanttBar>(p => p
            .Add(c => c.Task, plain).Add(c => c.X, 0d).Add(c => c.Width, 22d).Add(c => c.RowIndex, 0));

        Assert.Equal("", recurringCut.Find("[data-task-id='t1']").GetAttribute("data-recurring"));
        // Predicted-wrong value for a disable-check on the IsRecurring gate:
        // "" present here too (every task reads as recurring regardless).
        Assert.Null(plainCut.Find("[data-task-id='t2']").GetAttribute("data-recurring"));
    }

    // ── data-selected (plumbing regression — see GanttV3RowSelectionReorderTests
    //    for the "driven by a real checkbox click" E2E proof) ─────────────────

    [Fact]
    public void Data_Selected_Reflects_SelectedIds_Membership_Forwarded_Through_GanttTimeline()
    {
        var selected = new L.GanttTask("t1", "Design", D(2026, 1, 2), D(2026, 1, 4));
        var unselected = new L.GanttTask("t2", "Build", D(2026, 1, 5), D(2026, 1, 8));
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { selected, unselected })
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 31))
            .Add(c => c.SelectedIds, (IReadOnlySet<string>)new HashSet<string> { "t1" }));

        Assert.Equal("", cut.Find("[data-task-id='t1']").GetAttribute("data-selected"));
        // Predicted-wrong value if SelectedIds never reached GanttBar at all
        // (the exact "GanttBar does not currently receive selection state"
        // gap this audit closes): both bars null here, t1 included.
        Assert.Null(cut.Find("[data-task-id='t2']").GetAttribute("data-selected"));
    }

    [Fact]
    public void Data_Selected_Absent_By_Default_When_No_SelectedIds_Are_Supplied()
    {
        var task = new L.GanttTask("t1", "Design", D(2026, 1, 2), D(2026, 1, 4));
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { task })
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 31)));

        Assert.Null(cut.Find("[data-task-id='t1']").GetAttribute("data-selected"));
    }

    // ── data-off ──────────────────────────────────────────────────────────────

    [Fact]
    public void Data_Off_Present_On_Both_The_Header_Cell_And_The_Canvas_Band_For_A_Weekend_Column()
    {
        var original = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
        try
        {
            // 2026-01-05 (Mon) .. 2026-01-11 (Sun) — Sat (index 5) / Sun (index 6)
            // are the default off-days; same fixture GanttV3Phase3T2Tests already
            // pins for its own MarkOffDays specs.
            var cut = _ctx.Render<L.GanttTimeline>(p => p
                .Add(c => c.ViewMode, L.GanttViewMode.Day)
                .Add(c => c.RangeStart, D(2026, 1, 5))
                .Add(c => c.RangeEnd, D(2026, 1, 11))
                .Add(c => c.MarkOffDays, true));

            var lowerCells = cut.FindAll("div.shrink-0.text-center.text-xs");
            Assert.True(lowerCells.Count >= 7, $"expected at least 7 lower-header cells, found {lowerCells.Count}");
            for (var i = 0; i < 7; i++)
            {
                var isOffDay = i == 5 || i == 6;
                var attr = lowerCells[i].GetAttribute("data-off");
                if (isOffDay) Assert.Equal("", attr);
                // Predicted-wrong value for a gate that fired unconditionally:
                // "" present on every weekday cell too.
                else Assert.Null(attr);
            }

            var canvasBands = cut.FindAll(".lumeo-gantt-v3-off-day");
            Assert.Equal(2, canvasBands.Count);
            Assert.All(canvasBands, b => Assert.Equal("", b.GetAttribute("data-off")));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Data_Off_Absent_Everywhere_When_MarkOffDays_Is_False()
    {
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 5))
            .Add(c => c.RangeEnd, D(2026, 1, 11))
            .Add(c => c.MarkOffDays, false));

        var lowerCells = cut.FindAll("div.shrink-0.text-center.text-xs");
        Assert.All(lowerCells, c => Assert.Null(c.GetAttribute("data-off")));
        Assert.Empty(cut.FindAll(".lumeo-gantt-v3-off-day"));
    }

    [Fact]
    public void Toggling_Only_IsRecurring_Reaches_The_Rendered_Bar()
    {
        // Regression (Codex review of this PR, P2): GanttChart.ComputeTasksHash
        // did not fold IsRecurring in, so a task list whose ONLY change was
        // this flag hashed identically to the previous one. OnParametersSetAsync
        // then treated the parameter as unchanged, kept the old _state.Tasks,
        // and data-recurring stayed stale. Predicted-wrong value on the second
        // assert without the hash fix: null (attribute never appears).
        var plain = new List<L.GanttTask> { new("t1", "Standup", D(2026, 1, 5), D(2026, 1, 5)) };
        var cut = _ctx.Render<L.GanttChart>(p => p.Add(c => c.Tasks, plain));
        Assert.Null(cut.Find("[data-task-id='t1']").GetAttribute("data-recurring"));

        var recurring = new List<L.GanttTask>
        {
            new("t1", "Standup", D(2026, 1, 5), D(2026, 1, 5)) { IsRecurring = true },
        };
        cut.Render(p => p.Add(c => c.Tasks, recurring));

        Assert.Equal("", cut.Find("[data-task-id='t1']").GetAttribute("data-recurring"));
    }

    [Fact]
    public void The_Drag_Options_Carry_The_Timelines_Own_Now_So_The_Ghost_Cannot_Use_A_Second_Clock()
    {
        // Regression (Codex review of this PR, P2): the ghost's data-past
        // refresh in gantt-v3.js originally read `new Date()`. GanttBar derives
        // data-past from GanttTimeline.Now when a consumer supplies one (a
        // historical or simulated timeline), so the ghost could contradict the
        // very bar it was cloned from mid-drag. The engine now reads this
        // option instead; asserting the CONSUMER-supplied date (deliberately
        // not today) is what proves no second clock can creep back in.
        var interop = new TrackingInteropService();
        using var ctx = new BunitContext();
        ctx.AddLumeoServices();
        ctx.Services.AddSingleton<Lumeo.Services.IComponentInteropService>(interop);

        ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { new("t1", "Design", D(2026, 1, 2), D(2026, 1, 6)) })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.Now, new DateTime(2026, 1, 4, 9, 30, 0)));

        var options = Assert.IsType<Dictionary<string, object?>>(interop.LastGanttV3DragOptions);
        Assert.Equal("2026-01-04", options["nowDate"]);
    }

    // ── data-selected is leaf-only (Codex review of this PR, P2) ─────────────

    [Fact]
    public void Data_Selected_Is_Not_Applied_To_A_Hierarchy_Parent_Bar()
    {
        // A selected leaf can BECOME a parent on a later task update, and task
        // replacement does not normalize SelectedIds — so the stale id survives.
        // The tree derives a parent's checkbox purely from its descendant
        // leaves and can legitimately show it unchecked, so a plain membership
        // test made data-selected contradict the checkbox on the same row.
        var tasks = new List<L.GanttTask>
        {
            new("p1", "Epic", D(2026, 1, 2), D(2026, 1, 10)),
            new("c1", "Child", D(2026, 1, 3), D(2026, 1, 5)) { ParentId = "p1" },
        };
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, tasks)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 31))
            .Add(c => c.SelectedIds, new HashSet<string> { "p1", "c1" }));

        // Predicted-wrong value before the fix: "" on the parent too.
        Assert.Null(cut.Find("[data-task-id='p1']").GetAttribute("data-selected"));
        Assert.Equal("", cut.Find("[data-task-id='c1']").GetAttribute("data-selected"));
    }

    [Fact]
    public void The_Leaf_Only_Gate_Honours_A_Caller_Supplied_Rows_List_Not_The_Ignored_Tasks_Parameter()
    {
        // Regression (Codex review of this PR, P2): the first version of the
        // gate rebuilt hierarchy from the Tasks parameter. `Rows` takes
        // priority over `Tasks` when a caller supplies it, and Tasks is then
        // legitimately EMPTY — so a supplied parent row was stamped as a leaf
        // and could take data-selected, recreating exactly the parent/checkbox
        // contradiction the gate exists to prevent. Parent-ness now comes from
        // the authoritative GanttVisibleRow.HasChildren.
        var parent = new L.GanttTask("p1", "Epic", D(2026, 1, 2), D(2026, 1, 10));
        var rows = new List<GanttVisibleRow>
        {
            new(GanttRowKind.Task, parent, "Epic", 0, true, "p1", false),
        };

        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask>()) // deliberately empty — Rows wins
            .Add(c => c.Rows, rows)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 31))
            .Add(c => c.SelectedIds, new HashSet<string> { "p1" }));

        Assert.Null(cut.Find("[data-task-id='p1']").GetAttribute("data-selected"));
    }

    [Fact]
    public void The_Leaf_Only_Gate_Does_Not_Depend_On_ShowSummaryBars()
    {
        // Trap this nearly fell into: `row.Rollup` looks like a ready-made
        // "is this a parent" test, but Rollups is EMPTY whenever
        // ShowSummaryBars is off — keying the hook off it would have made
        // data-selected flip with an unrelated display toggle. Asserted for
        // BOTH values of that toggle.
        var tasks = new List<L.GanttTask>
        {
            new("p1", "Epic", D(2026, 1, 2), D(2026, 1, 10)),
            new("c1", "Child", D(2026, 1, 3), D(2026, 1, 5)) { ParentId = "p1" },
        };

        foreach (var showSummaryBars in new[] { false, true })
        {
            using var ctx = new BunitContext();
            ctx.AddLumeoServices();
            var cut = ctx.Render<L.GanttTimeline>(p => p
                .Add(c => c.Tasks, tasks)
                .Add(c => c.RangeStart, D(2026, 1, 1))
                .Add(c => c.RangeEnd, D(2026, 1, 31))
                .Add(c => c.ShowSummaryBars, showSummaryBars)
                .Add(c => c.SelectedIds, new HashSet<string> { "p1" }));

            Assert.Null(cut.Find("[data-task-id='p1']").GetAttribute("data-selected"));
        }
    }

    [Fact]
    public void A_Standalone_Timeline_Asks_For_The_Browser_Clock_At_Most_Once_Even_When_Interop_Yields_Nothing()
    {
        // Regression (CI, caught on this PR): the resolve was guarded on
        // "_selfResolvedNow is not null", which only trips on SUCCESS. When the
        // interop legitimately yields nothing — prerendering, a torn-down
        // circuit, a custom service whose default no-op returns null — the field
        // stayed null and the call repeated on EVERY render, an unbounded
        // per-render round trip whose awaits dispatched further render and
        // registration cycles. Predicted-wrong value before the fix: a count
        // that keeps climbing with the render count.
        var interop = new TrackingInteropService { GanttV3LocalDateTimeToReturn = null };
        using var ctx = new BunitContext();
        ctx.AddLumeoServices();
        ctx.Services.AddSingleton<Lumeo.Services.IComponentInteropService>(interop);

        var tasks = new List<L.GanttTask> { new("t1", "Design", D(2026, 1, 2), D(2026, 1, 6)) };
        var cut = ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, tasks)
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 31)));

        for (var i = 0; i < 5; i++)
        {
            cut.Render(p => p
                .Add(c => c.Tasks, tasks)
                .Add(c => c.ViewMode, L.GanttViewMode.Day)
                .Add(c => c.RangeStart, D(2026, 1, 1))
                .Add(c => c.RangeEnd, D(2026, 1, 31)));
        }

        Assert.Equal(1, interop.GanttV3GetLocalDateTimeCallCount);
    }

    [Fact]
    public void Returning_To_An_Uncontrolled_Now_Re_Queries_Instead_Of_Restoring_A_Stale_Clock()
    {
        // Regression (Codex review of this PR, P2): after one successful
        // self-resolve, supplying an explicit Now and later clearing it reused
        // the cached value, so a long-lived timeline could restore a clock
        // cached days earlier. Predicted-wrong value before the fix: the call
        // count stays at 1 because the attempt flag was never cleared.
        var interop = new TrackingInteropService { GanttV3LocalDateTimeToReturn = "2026-01-04T09:30:00" };
        using var ctx = new BunitContext();
        ctx.AddLumeoServices();
        ctx.Services.AddSingleton<Lumeo.Services.IComponentInteropService>(interop);

        var tasks = new List<L.GanttTask> { new("t1", "Design", D(2026, 1, 2), D(2026, 1, 6)) };
        var cut = ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, tasks)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 31)));
        Assert.Equal(1, interop.GanttV3GetLocalDateTimeCallCount);

        // Controlled for a while...
        cut.Render(p => p
            .Add(c => c.Tasks, tasks)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 31))
            .Add(c => c.Now, new DateTime(2026, 6, 1, 8, 0, 0)));

        // ...then handed back to the component.
        cut.Render(p => p
            .Add(c => c.Tasks, tasks)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 31))
            .Add(c => c.Now, (DateTime?)null));

        Assert.Equal(2, interop.GanttV3GetLocalDateTimeCallCount);
    }

    // ── data-past boundary (Codex review of this PR, P2) ──────────────────────
    // The original implementation compared the RAW Task.End, which disagreed
    // with the bar the user is looking at: GanttScale.BarGeometry treats End as
    // INCLUSIVE and renders through End.Date.AddDays(1). Both tests below fail
    // against that version and pass against the rendered-endpoint one.

    [Fact]
    public void Data_Past_Absent_While_Now_Is_Still_Inside_The_Task_S_Own_Final_Day()
    {
        // End is 2026-06-15 (inclusive) and "now" is midday on that same day —
        // the bar still covers all of today, so the hook must NOT say past.
        // Predicted-wrong value under a raw `Task.End < EffectiveNow` compare:
        // "" (present), because midnight-plus-anything already exceeds
        // 2026-06-15T00:00:00.
        var task = new L.GanttTask("t1", "Design", D(2026, 6, 10), D(2026, 6, 15));
        var cut = _ctx.Render<L.GanttBar>(p => p
            .Add(c => c.Task, task).Add(c => c.X, 0d).Add(c => c.Width, 100d).Add(c => c.RowIndex, 0)
            .Add(c => c.Now, new DateTime(2026, 6, 15, 12, 0, 0)));

        Assert.Null(cut.Find("[data-task-id='t1']").GetAttribute("data-past"));
    }

    [Fact]
    public void Data_Past_Appears_Once_Now_Crosses_Into_The_Day_After_The_Inclusive_End()
    {
        var task = new L.GanttTask("t1", "Design", D(2026, 6, 10), D(2026, 6, 15));
        var cut = _ctx.Render<L.GanttBar>(p => p
            .Add(c => c.Task, task).Add(c => c.X, 0d).Add(c => c.Width, 100d).Add(c => c.RowIndex, 0)
            .Add(c => c.Now, new DateTime(2026, 6, 16, 0, 0, 0)));

        Assert.Equal("", cut.Find("[data-task-id='t1']").GetAttribute("data-past"));
    }

    [Theory]
    [InlineData(true)]  // milestone — endpoint derives from Start
    [InlineData(false)] // duration bar — endpoint derives from End
    public void Data_Past_Does_Not_Overflow_For_A_Max_Value_Dated_Task(bool isMilestone)
    {
        // Regression (Codex review of this PR, P2): the first version of the
        // endpoint fix computed `.AddDays(1)` on the task's own date, which
        // throws ArgumentOutOfRangeException at DateTime.MaxValue.Date — a
        // legitimate "no deadline" sentinel. That took a bar which rendered
        // fine BEFORE this PR and made it crash on render, so the styling hook
        // would have introduced a hard failure rather than a wrong class.
        var max = DateTime.MaxValue.Date;
        var task = isMilestone
            ? new L.GanttTask("t1", "Someday", max, max, IsMilestone: true)
            : new L.GanttTask("t1", "Someday", D(2026, 1, 1), max);

        var ex = Record.Exception(() => _ctx.Render<L.GanttBar>(p => p
            .Add(c => c.Task, task).Add(c => c.X, 0d).Add(c => c.Width, 40d).Add(c => c.RowIndex, 0)
            .Add(c => c.Now, new DateTime(2026, 6, 15, 12, 0, 0))));

        Assert.Null(ex);
    }

    [Fact]
    public void Data_Past_For_A_Milestone_Follows_Its_Rendered_Start_Not_A_Mismatched_End()
    {
        // BarGeometry positions a milestone from Start alone and ignores End
        // entirely, so a milestone carrying an inconsistent End (nothing in the
        // data model forbids it) must not report a past-ness its own diamond
        // contradicts. Predicted-wrong value under the raw-End compare: null,
        // because the stale End is still in the future.
        var milestone = new L.GanttTask("m1", "Launch", D(2026, 6, 10), D(2026, 12, 31), IsMilestone: true);
        var cut = _ctx.Render<L.GanttBar>(p => p
            .Add(c => c.Task, milestone).Add(c => c.X, 0d).Add(c => c.Width, 22d).Add(c => c.RowIndex, 0)
            .Add(c => c.Now, new DateTime(2026, 6, 11, 0, 0, 0)));

        Assert.Equal("", cut.Find("[data-task-id='m1']").GetAttribute("data-past"));
    }
}
