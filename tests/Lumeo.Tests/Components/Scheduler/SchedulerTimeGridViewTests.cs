using Bunit;
using Microsoft.AspNetCore.Components.Web;
using System.Globalization;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Scheduler;

/// <summary>
/// bUnit coverage for <see cref="L.SchedulerTimeGridView"/> (shared Week/Day renderer) —
/// markup, ARIA grid semantics, parameter wiring, and the fail-closed
/// <c>ValidateDrop</c>/<c>CommitDrag</c>/<c>CommitCreate</c> JSInvokable logic. Pointer/ghost
/// geometry itself is Playwright's job (spec §7.2) — see <c>tests/Lumeo.Tests.E2E</c>.
/// </summary>
public class SchedulerTimeGridViewTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public Task InitializeAsync()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static DateTime D(int y, int m, int d, int h = 0, int mi = 0) => new(y, m, d, h, mi, 0);

    private static string Key(string eventId, DateTime start) => $"{eventId}|{start:O}";

    // ── Grid / ARIA semantics ────────────────────────────────────────────────

    [Fact]
    public void Week_View_Renders_7_Day_Columns()
    {
        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, D(2026, 3, 11))
            .Add(c => c.Days, 7));

        Assert.Equal(7, cut.FindAll("[data-daycol]").Count);
    }

    [Fact]
    public void Day_View_Renders_A_Single_Day_Column()
    {
        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, D(2026, 3, 11))
            .Add(c => c.Days, 1));

        var cols = cut.FindAll("[data-daycol]");
        Assert.Single(cols);
        Assert.Equal("2026-03-11", cols[0].GetAttribute("data-daycol"));
    }

    [Fact]
    public void SlotMinMax_Clamps_The_Rendered_Hour_Rows()
    {
        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, D(2026, 3, 11))
            .Add(c => c.Days, 1)
            .Add(c => c.SlotMinTime, new TimeOnly(8, 0))
            .Add(c => c.SlotMaxTime, new TimeOnly(18, 0)));

        // One gridcell per hour per day (spec §5.1's coarser-grid design choice) — 10 hours.
        Assert.Equal(10, cut.FindAll("[role='gridcell']").Count);
    }

    [Fact]
    public void Renders_Timed_Event_With_Title_And_Time()
    {
        var events = new[] { new L.SchedulerEvent("e1", "Design Review", D(2026, 3, 11, 14, 0), D(2026, 3, 11, 15, 0)) };
        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, D(2026, 3, 11))
            .Add(c => c.Days, 1)
            .Add(c => c.Events, events));

        var pill = cut.Find("[data-event-id='e1']");
        Assert.Contains("Design Review", pill.TextContent);
    }

    [Fact]
    public void Renders_AllDay_Event_In_The_AllDay_Lane()
    {
        var events = new[] { new L.SchedulerEvent("e1", "Offsite", D(2026, 3, 11), D(2026, 3, 12), AllDay: true) };
        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, D(2026, 3, 11))
            .Add(c => c.Days, 7)
            .Add(c => c.Events, events));

        Assert.Contains("Offsite", cut.Markup);
    }

    // ── Callback wiring ──────────────────────────────────────────────────────

    [Fact]
    public void Clicking_An_Event_Fires_OnEventClick()
    {
        L.SchedulerEvent? clicked = null;
        var events = new[] { new L.SchedulerEvent("e1", "Standup", D(2026, 3, 11, 9, 0), D(2026, 3, 11, 9, 30)) };
        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, D(2026, 3, 11))
            .Add(c => c.Days, 1)
            .Add(c => c.Events, events)
            .Add(c => c.OnEventClick, (L.SchedulerEvent e) => clicked = e));

        cut.Find("[data-event-id='e1']").Click();

        Assert.NotNull(clicked);
        Assert.Equal("e1", clicked!.Id);
    }

    [Fact]
    public void DoubleClicking_An_Empty_Slot_Fires_OnDateSelect()
    {
        L.SchedulerDateRange? selected = null;
        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, D(2026, 3, 11))
            .Add(c => c.Days, 1)
            .Add(c => c.OnDateSelect, (L.SchedulerDateRange r) => selected = r));

        cut.Find("[data-slot-hour='10']").DoubleClick();

        Assert.NotNull(selected);
        Assert.Equal(D(2026, 3, 11, 10, 0), selected!.Start);
        Assert.False(selected.AllDay);
    }

    // ── Fail-closed CanDrop / ValidateDrop (spec §3.2) ───────────────────────

    [Fact]
    public void ValidateDrop_Returns_True_When_CanDrop_Is_Null()
    {
        var events = new[] { new L.SchedulerEvent("e1", "Standup", D(2026, 3, 11, 9, 0), D(2026, 3, 11, 9, 30)) };
        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p.Add(c => c.Events, events));

        Assert.True(cut.Instance.ValidateDrop(Key("e1", D(2026, 3, 11, 9, 0)), "move", "2026-03-12", 0));
    }

    [Fact]
    public void ValidateDrop_Fail_Closed_When_CanDrop_Returns_False()
    {
        var events = new[] { new L.SchedulerEvent("e1", "Standup", D(2026, 3, 11, 9, 0), D(2026, 3, 11, 9, 30)) };
        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.Events, events)
            .Add(c => c.CanDrop, (L.SchedulerEvent _, L.SchedulerScheduleDropContext _) => false));

        Assert.False(cut.Instance.ValidateDrop(Key("e1", D(2026, 3, 11, 9, 0)), "move", "2026-03-12", 0));
    }

    [Fact]
    public void ValidateDrop_Fail_Closed_For_Unresolvable_Instance_Key()
    {
        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.CanDrop, (L.SchedulerEvent _, L.SchedulerScheduleDropContext _) => true));

        Assert.False(cut.Instance.ValidateDrop("garbage-key-no-pipe", "move", "2026-03-12", 0));
        Assert.False(cut.Instance.ValidateDrop("unknown-event|2026-03-11T09:00:00.0000000", "move", "2026-03-12", 0));
    }

    [Fact]
    public void ValidateDrop_Move_Computes_New_Day_Plus_Minute_Delta()
    {
        L.SchedulerScheduleDropContext? seen = null;
        var events = new[] { new L.SchedulerEvent("e1", "Standup", D(2026, 3, 11, 9, 0), D(2026, 3, 11, 9, 30)) };
        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.Events, events)
            .Add(c => c.CanDrop, (L.SchedulerEvent _, L.SchedulerScheduleDropContext ctx) => { seen = ctx; return true; }));

        cut.Instance.ValidateDrop(Key("e1", D(2026, 3, 11, 9, 0)), "move", "2026-03-12", 15);

        Assert.NotNull(seen);
        Assert.Equal(new DateTime(2026, 3, 12, 9, 15, 0), seen!.ProposedStart);
        Assert.Equal(new DateTime(2026, 3, 12, 9, 45, 0), seen.ProposedEnd);
        Assert.Equal(L.SchedulerEventUpdateSource.Move, seen.Source);
    }

    [Fact]
    public void ValidateDrop_ResizeStart_Shifts_Only_The_Start()
    {
        L.SchedulerScheduleDropContext? seen = null;
        var events = new[] { new L.SchedulerEvent("e1", "Standup", D(2026, 3, 11, 9, 0), D(2026, 3, 11, 10, 0)) };
        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.Events, events)
            .Add(c => c.CanDrop, (L.SchedulerEvent _, L.SchedulerScheduleDropContext ctx) => { seen = ctx; return true; }));

        cut.Instance.ValidateDrop(Key("e1", D(2026, 3, 11, 9, 0)), "resize-start", null, -30);

        Assert.NotNull(seen);
        Assert.Equal(new DateTime(2026, 3, 11, 8, 30, 0), seen!.ProposedStart);
        Assert.Equal(new DateTime(2026, 3, 11, 10, 0, 0), seen.ProposedEnd);
        Assert.Equal(L.SchedulerEventUpdateSource.ResizeStart, seen.Source);
    }

    [Fact]
    public void ValidateDrop_ResizeEnd_Shifts_Only_The_End()
    {
        L.SchedulerScheduleDropContext? seen = null;
        var events = new[] { new L.SchedulerEvent("e1", "Standup", D(2026, 3, 11, 9, 0), D(2026, 3, 11, 10, 0)) };
        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.Events, events)
            .Add(c => c.CanDrop, (L.SchedulerEvent _, L.SchedulerScheduleDropContext ctx) => { seen = ctx; return true; }));

        cut.Instance.ValidateDrop(Key("e1", D(2026, 3, 11, 9, 0)), "resize-end", null, 30);

        Assert.NotNull(seen);
        Assert.Equal(new DateTime(2026, 3, 11, 9, 0, 0), seen!.ProposedStart);
        Assert.Equal(new DateTime(2026, 3, 11, 10, 30, 0), seen.ProposedEnd);
        Assert.Equal(L.SchedulerEventUpdateSource.ResizeEnd, seen.Source);
    }

    [Fact]
    public async Task CommitDrag_Fires_OnEventChange_With_The_New_Window()
    {
        L.SchedulerEvent? changed = null;
        var events = new[] { new L.SchedulerEvent("e1", "Standup", D(2026, 3, 11, 9, 0), D(2026, 3, 11, 9, 30)) };
        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.Events, events)
            .Add(c => c.OnEventChange, (L.SchedulerEvent e) => changed = e));

        await cut.InvokeAsync(() => cut.Instance.CommitDrag(Key("e1", D(2026, 3, 11, 9, 0)), "move", "2026-03-13", 60));

        Assert.NotNull(changed);
        Assert.Equal(new DateTime(2026, 3, 13, 10, 0, 0), changed!.Start);
        Assert.Equal(new DateTime(2026, 3, 13, 10, 30, 0), changed.End);
    }

    [Fact]
    public async Task CommitCreate_Fires_OnDateSelect_With_The_Drawn_Window()
    {
        L.SchedulerDateRange? selected = null;
        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.OnDateSelect, (L.SchedulerDateRange r) => selected = r));

        await cut.InvokeAsync(() => cut.Instance.CommitCreate("2026-03-11", 9 * 60, 10 * 60));

        Assert.NotNull(selected);
        Assert.Equal(new DateTime(2026, 3, 11, 9, 0, 0), selected!.Start);
        Assert.Equal(new DateTime(2026, 3, 11, 10, 0, 0), selected.End);
        Assert.False(selected.AllDay);
    }

    // ── Drag registration options (hasCanDrop gate) ──────────────────────────

    [Fact]
    public void Registers_Drag_With_HasCanDrop_False_When_CanDrop_Unset()
    {
        _ctx.Render<L.SchedulerTimeGridView>(p => p.Add(c => c.AnchorDate, D(2026, 3, 11)));

        var options = Assert.IsType<Dictionary<string, object?>>(_interop.LastSchedulerViewsTimeGridDragOptions);
        Assert.Equal(false, options["hasCanDrop"]);
    }

    [Fact]
    public void Registers_Drag_With_HasCanDrop_True_When_CanDrop_Set()
    {
        _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, D(2026, 3, 11))
            .Add(c => c.CanDrop, (L.SchedulerEvent _, L.SchedulerScheduleDropContext _) => true));

        var options = Assert.IsType<Dictionary<string, object?>>(_interop.LastSchedulerViewsTimeGridDragOptions);
        Assert.Equal(true, options["hasCanDrop"]);
    }

    // ── Now-indicator: ONE registration on the grid host, never gated on a
    // server-computed "today" (spec §2.2 — the browser decides whether the line shows,
    // from the visible dates it is handed). ──

    [Fact]
    public void NowIndicator_True_Registers_Once_For_The_Whole_Grid()
    {
        // This asserted one registration PER DAY COLUMN until the line was reported as
        // spanning only today's column instead of the grid. A line appended into a column
        // can only ever be that column wide, so seven columns grew seven stubs. It is now
        // one line on the grid host, which also removes the unbound-ElementReference case
        // that surfaced as "containerEl.appendChild is not a function".
        _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, D(2026, 3, 11))
            .Add(c => c.Days, 7)
            .Add(c => c.NowIndicator, true));

        Assert.Equal(1, _interop.SchedulerViewsRegisterNowIndicatorCallCount);

        // It needs every visible date, not one: that is how it decides whether today is on
        // screen at all.
        var options = Assert.IsType<Dictionary<string, object?>>(
            Assert.Single(_interop.SchedulerViewsNowIndicatorOptions));
        Assert.Equal(7, Assert.IsType<string[]>(options["days"]).Length);
    }

    [Fact]
    public void NowIndicator_False_Registers_Nothing()
    {
        _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, D(2026, 3, 11))
            .Add(c => c.Days, 7)
            .Add(c => c.NowIndicator, false));

        Assert.Equal(0, _interop.SchedulerViewsRegisterNowIndicatorCallCount);
    }

    // ── CanDrop three-way: reject / accept / accept-with-adjustment ──────────

    [Fact]
    public void ValidateDrop_Accepts_When_CanDrop_Returns_SchedulerDropResult_Accept()
    {
        var events = new[] { new L.SchedulerEvent("e1", "Standup", D(2026, 3, 11, 9, 0), D(2026, 3, 11, 9, 30)) };
        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.Events, events)
            .Add(c => c.CanDrop, (L.SchedulerEvent _, L.SchedulerScheduleDropContext _) => L.SchedulerDropResult.Accept));

        Assert.True(cut.Instance.ValidateDrop(Key("e1", D(2026, 3, 11, 9, 0)), "move", "2026-03-12", 0));
    }

    [Fact]
    public void ValidateDrop_Rejects_When_CanDrop_Returns_SchedulerDropResult_Reject()
    {
        var events = new[] { new L.SchedulerEvent("e1", "Standup", D(2026, 3, 11, 9, 0), D(2026, 3, 11, 9, 30)) };
        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.Events, events)
            .Add(c => c.CanDrop, (L.SchedulerEvent _, L.SchedulerScheduleDropContext _) => L.SchedulerDropResult.Reject));

        Assert.False(cut.Instance.ValidateDrop(Key("e1", D(2026, 3, 11, 9, 0)), "move", "2026-03-12", 0));
    }

    [Fact]
    public async Task CommitDrag_Applies_The_CanDrop_Adjustment_Instead_Of_The_Raw_Proposal()
    {
        // Predicted: a plain move-by-60-minutes on 2026-03-13 would normally land at
        // 10:00-10:30 (matches CommitDrag_Fires_OnEventChange_With_The_New_Window's own raw
        // math). CanDrop here snaps the commit 15 minutes later instead — proving the
        // adjustment reaches the committed event, not just the accept/reject verdict.
        L.SchedulerEvent? changed = null;
        var events = new[] { new L.SchedulerEvent("e1", "Standup", D(2026, 3, 11, 9, 0), D(2026, 3, 11, 9, 30)) };
        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.Events, events)
            .Add(c => c.OnEventChange, (L.SchedulerEvent e) => changed = e)
            .Add(c => c.CanDrop, (L.SchedulerEvent _, L.SchedulerScheduleDropContext ctx) =>
                L.SchedulerDropResult.AcceptWith(new L.SchedulerDropAdjustment(Start: ctx.ProposedStart.AddMinutes(15), End: ctx.ProposedEnd.AddMinutes(15)))));

        await cut.InvokeAsync(() => cut.Instance.CommitDrag(Key("e1", D(2026, 3, 11, 9, 0)), "move", "2026-03-13", 60));

        Assert.NotNull(changed);
        Assert.Equal(new DateTime(2026, 3, 13, 10, 15, 0), changed!.Start); // predicted 10:15, NOT the raw 10:00 proposal
        Assert.Equal(new DateTime(2026, 3, 13, 10, 45, 0), changed.End);
    }

    [Fact]
    public async Task CommitDrag_Re_Validates_CanDrop_And_Refuses_A_Rejected_Commit()
    {
        // Same reasoning as SchedulerMonthView's identical test: CommitDrag now re-validates
        // CanDrop itself rather than trusting a prior JS-side ValidateDrop poll — this is the
        // mutation-test surface for the TimeGrid half of the widened contract.
        var fired = false;
        var events = new[] { new L.SchedulerEvent("e1", "Standup", D(2026, 3, 11, 9, 0), D(2026, 3, 11, 9, 30)) };
        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.Events, events)
            .Add(c => c.OnEventChange, (L.SchedulerEvent _) => fired = true)
            .Add(c => c.CanDrop, (L.SchedulerEvent _, L.SchedulerScheduleDropContext _) => L.SchedulerDropResult.Reject));

        await cut.InvokeAsync(() => cut.Instance.CommitDrag(Key("e1", D(2026, 3, 11, 9, 0)), "move", "2026-03-13", 60));

        Assert.False(fired);
    }

    // ── Business-hours off-slot shading (spec's business-hours regression fix) ───

    [Fact]
    public void BusinessHours_False_By_Default_Never_Emits_Data_Off()
    {
        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, D(2026, 3, 11))
            .Add(c => c.Days, 1));

        Assert.Empty(cut.FindAll("[data-off]"));
    }

    [Fact]
    public void BusinessHours_True_Marks_Slots_Outside_9_To_17_Off()
    {
        // 2026-03-11 is a Wednesday (a business day) — predicted: the 8:00 slot is off
        // (before BusinessHoursStart), the 12:00 slot is on-hours, the 17:00 slot is off
        // (at/after the exclusive BusinessHoursEnd).
        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, D(2026, 3, 11))
            .Add(c => c.Days, 1)
            .Add(c => c.BusinessHours, true));

        Assert.Equal("true", cut.Find("[data-slot-hour='8']").GetAttribute("data-off"));
        Assert.Null(cut.Find("[data-slot-hour='12']").GetAttribute("data-off"));
        Assert.Equal("true", cut.Find("[data-slot-hour='17']").GetAttribute("data-off"));
    }

    [Fact]
    public void BusinessHours_True_Marks_A_Whole_Weekend_Day_Off_Regardless_Of_Hour()
    {
        // 2026-03-14 is a Saturday — every slot in that day column is off, even the ones
        // that would be within business HOURS on a weekday.
        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, D(2026, 3, 11)) // week containing Sat 2026-03-14
            .Add(c => c.Days, 7)
            .Add(c => c.BusinessHours, true));

        var saturdayColumn = cut.Find("[data-daycol='2026-03-14']");
        var slotsInColumn = saturdayColumn.QuerySelectorAll("[data-slot-hour]");
        Assert.NotEmpty(slotsInColumn);
        Assert.All(slotsInColumn, el => Assert.Equal("true", el.GetAttribute("data-off")));
    }

    // ── Resize-handle visual affordance (audit: "a hit zone nobody can see is not a feature") ─

    [Fact]
    public void Editable_Timed_Pill_Carries_Data_Resizable()
    {
        var events = new[] { new L.SchedulerEvent("e1", "Standup", D(2026, 3, 11, 9, 0), D(2026, 3, 11, 9, 30)) };
        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, D(2026, 3, 11))
            .Add(c => c.Days, 1)
            .Add(c => c.Events, events)
            .Add(c => c.Editable, true));

        Assert.Equal("true", cut.Find("[data-event-id='e1']").GetAttribute("data-resizable"));
    }

    [Fact]
    public void NonEditable_Timed_Pill_Has_No_Data_Resizable()
    {
        // Predicted: pre-fix markup never emits data-resizable at all (the attribute didn't
        // exist), so this assertion would also incidentally pass against the OLD code for the
        // wrong reason — paired with the test above (which DOES fail against old markup, since
        // the attribute is simply absent there) to actually pin the Editable-gating behavior.
        var events = new[] { new L.SchedulerEvent("e1", "Standup", D(2026, 3, 11, 9, 0), D(2026, 3, 11, 9, 30)) };
        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, D(2026, 3, 11))
            .Add(c => c.Days, 1)
            .Add(c => c.Events, events)
            .Add(c => c.Editable, false));

        Assert.Null(cut.Find("[data-event-id='e1']").GetAttribute("data-resizable"));
    }

    // ── All-day strip lane packing (structural fix: reuse SchedulerMonthLayout.PackRow) ──

    [Fact]
    public void MultiDay_AllDay_Event_Keeps_The_Same_Lane_Across_Day_Columns()
    {
        // The exact regression the audit found: A (single-day, day1 only), B (multi-day,
        // day1+day2), C (single-day, day2 only). The OLD per-column-independent `.Where(...)`
        // filter placed each day column's own events in list order — day1 renders [A, B] (B at
        // lane/DOM-index 1, after A), day2 renders [B, C] (B at lane/DOM-index 0, since A isn't
        // present there) — B's vertical position SHIFTS between columns. Predicted wrong value
        // against the pre-fix code: day1's B has data-lane="1", day2's B has data-lane="0" (a
        // concrete, different, wrong pair of values) — this test fails against that state.
        // SchedulerMonthLayout.PackRow computes ONE shared lane per event across the whole row
        // instead, so the fixed code keeps B at the SAME data-lane on both columns.
        var events = new[]
        {
            new L.SchedulerEvent("a", "A", D(2026, 3, 11), D(2026, 3, 12), AllDay: true),
            new L.SchedulerEvent("b", "B", D(2026, 3, 11), D(2026, 3, 13), AllDay: true),
            new L.SchedulerEvent("c", "C", D(2026, 3, 12), D(2026, 3, 13), AllDay: true),
        };
        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, D(2026, 3, 11))
            .Add(c => c.Days, 7)
            .Add(c => c.Events, events));

        // The all-day strip's day columns render in the same left-to-right order as
        // _dayColumns, so the two "b" pill occurrences in DOM order correspond to day1 then
        // day2 — assert their data-lane values match.
        var bPills = cut.FindAll("[data-event-id='b']");
        Assert.Equal(2, bPills.Count);
        Assert.Equal(bPills[0].GetAttribute("data-lane"), bPills[1].GetAttribute("data-lane"));
    }

    [Fact]
    public void AllDay_Strip_Renders_A_Lane_Placeholder_When_A_Days_Lane_Is_Empty()
    {
        // A (day1 only) and C (day2 only) share lane 0 in the packer (their spans don't
        // overlap on any shared day cell) — day1's own strip column must NOT render C's pill,
        // proving lane reuse doesn't leak an event onto a day it doesn't touch.
        var events = new[]
        {
            new L.SchedulerEvent("a", "A", D(2026, 3, 11), D(2026, 3, 12), AllDay: true),
            new L.SchedulerEvent("c", "C", D(2026, 3, 12), D(2026, 3, 13), AllDay: true),
        };
        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, D(2026, 3, 11))
            .Add(c => c.Days, 7)
            .Add(c => c.Events, events));

        Assert.Single(cut.FindAll("[data-event-id='a']"));
        Assert.Single(cut.FindAll("[data-event-id='c']"));
    }

    // -- the day header, reported missing against the live docs -----------------

    [Fact]
    public void The_week_grid_labels_every_column_with_its_day()
    {
        // Week and Day had no header at all: the grid opened straight on the all-day strip, so
        // nothing said whether a column was Monday or Thursday, and the view read as though its
        // top had been cut off. Month has carried its weekday row from the start.
        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, D(2026, 4, 15))
            .Add(c => c.Days, 7)
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Monday)
            .Add(c => c.Events, Array.Empty<L.SchedulerEvent>()));

        var headers = cut.FindAll("[data-dayheader]");
        Assert.Equal(7, headers.Count);

        // The dates the columns actually draw, in order — Monday-first from the anchor's week.
        Assert.Equal(
            new[] { "2026-04-13", "2026-04-14", "2026-04-15", "2026-04-16", "2026-04-17", "2026-04-18", "2026-04-19" },
            headers.Select(h => h.GetAttribute("data-dayheader")).ToArray());

        // And each header carries its day NUMBER, not just a weekday name: a time grid shows one
        // specific week, where the month grid shows a repeating pattern.
        Assert.Contains("15", headers[2].TextContent);
    }

    [Fact]
    public void A_header_column_lines_up_with_the_grid_column_below_it()
    {
        // The header is a separate row from the grid, so the two carry their own column
        // definitions — the one thing that can silently drift apart.
        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, D(2026, 4, 15))
            .Add(c => c.Days, 7)
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Monday)
            .Add(c => c.Events, Array.Empty<L.SchedulerEvent>()));

        var headerDates = cut.FindAll("[data-dayheader]").Select(h => h.GetAttribute("data-dayheader")).ToArray();
        var columnDates = cut.FindAll("[data-daycol]").Select(c => c.GetAttribute("data-daycol")).ToArray();

        Assert.Equal(columnDates, headerDates);
    }

    [Fact]
    public void The_day_view_gets_one_header()
    {
        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, D(2026, 4, 15))
            .Add(c => c.Days, 1)
            .Add(c => c.Events, Array.Empty<L.SchedulerEvent>()));

        var headers = cut.FindAll("[data-dayheader]");
        Assert.Single(headers);
        Assert.Equal("2026-04-15", headers[0].GetAttribute("data-dayheader"));
    }

    [Fact]
    public void Today_is_marked_in_the_header_and_only_today()
    {
        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, DateTime.Today)
            .Add(c => c.Days, 7)
            .Add(c => c.Events, Array.Empty<L.SchedulerEvent>()));

        var marked = cut.FindAll("[data-dayheader][data-today='true']");
        Assert.Single(marked);
        Assert.Equal(DateTime.Today.ToString("yyyy-MM-dd"), marked[0].GetAttribute("data-dayheader"));
    }

    [Fact]
    public void The_header_names_the_weekday_the_way_the_culture_does()
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("de-DE");
            var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
                .Add(c => c.AnchorDate, D(2026, 4, 15))
                .Add(c => c.Days, 1)
                .Add(c => c.Events, Array.Empty<L.SchedulerEvent>()));

            // 2026-04-15 is a Wednesday; German abbreviates it "Mi".
            Assert.Contains("Mi", cut.Find("[data-dayheader]").TextContent);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void The_first_hour_label_is_not_lifted_above_the_scrollers_edge()
    {
        // Every other label is lifted onto its own gridline by a negative offset. The first has
        // no gridline above it, only the scroller's clipping edge, so the lift cut its digits in
        // half — which is what "the week view is cut off at the top" turned out to be.
        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, D(2026, 4, 15))
            .Add(c => c.Days, 7)
            .Add(c => c.Events, Array.Empty<L.SchedulerEvent>()));

        var labels = cut.FindAll("span.absolute.right-1\\.5");
        Assert.NotEmpty(labels);

        var first = labels[0].GetAttribute("class") ?? string.Empty;
        Assert.DoesNotContain("-top-1.5", first);
        Assert.Contains("top-0", first);

        // The rest keep the lift — the fix is for the edge case, not a change of alignment.
        var second = labels[1].GetAttribute("class") ?? string.Empty;
        Assert.Contains("-top-1.5", second);
    }

    // -- review round 1 --------------------------------------------------------

    [Fact]
    public void The_header_marks_the_day_the_scheduler_is_showing_not_the_hosts_own()
    {
        // A scheduler projected into another zone can be a whole day away from the server
        // drawing it. The timeline already takes its today from the wrapper for exactly this
        // reason; the header does now too.
        var elsewhere = DateTime.Today.AddDays(1);

        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, elsewhere)
            .Add(c => c.Days, 7)
            .Add(c => c.Today, elsewhere)
            .Add(c => c.Events, Array.Empty<L.SchedulerEvent>()));

        var marked = cut.FindAll("[data-dayheader][data-today='true']");
        Assert.Single(marked);
        Assert.Equal(elsewhere.ToString("yyyy-MM-dd"), marked[0].GetAttribute("data-dayheader"));

        // And the host's own today is NOT marked, which is the half that would still pass if
        // the parameter were read but the fallback left in place.
        Assert.DoesNotContain(
            DateTime.Today.ToString("yyyy-MM-dd"),
            marked.Select(m => m.GetAttribute("data-dayheader")));
    }

    [Fact]
    public void The_header_and_the_grid_share_one_scroller()
    {
        // Laid out against the full component width while the grid below lost the scrollbar
        // width to it, every header drifted off the column it labels on any platform with
        // non-overlay scrollbars. Sharing the scroller makes the widths equal by construction,
        // which no arithmetic in the markup can be trusted to reproduce.
        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, D(2026, 4, 15))
            .Add(c => c.Days, 7)
            .Add(c => c.Events, new[]
            {
                // An all-day event, so the strip renders and is covered by the same assertion.
                new L.SchedulerEvent("a1", "Offsite", D(2026, 4, 15), D(2026, 4, 16)) { AllDay = true },
            }));

        // Queried FROM the scroller: bUnit hands back wrapper instances, so comparing
        // elements by identity across two queries never matches.
        var scroller = cut.Find("[style*='overflow-y: auto']");

        Assert.NotNull(scroller.QuerySelector("[data-testid='timegrid-day-header']"));
        Assert.NotNull(scroller.QuerySelector("[data-event-id='a1']"));
        Assert.NotNull(scroller.QuerySelector("[role='grid']"));

        // And there is exactly ONE scroller, so "inside it" cannot mean two different boxes.
        Assert.Single(cut.FindAll("[style*='overflow-y: auto']"));
    }

    [Fact]
    public void The_header_block_sticks_while_the_hours_scroll()
    {
        // The other half of moving it in: a header that scrolls away over 24 hours would answer
        // the question once and then stop.
        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, D(2026, 4, 15))
            .Add(c => c.Days, 7)
            .Add(c => c.Events, Array.Empty<L.SchedulerEvent>()));

        var header = cut.Find("[data-testid='timegrid-day-header']");
        var sticky = header.ParentElement;

        Assert.NotNull(sticky);
        Assert.Contains("sticky", sticky!.GetAttribute("class") ?? string.Empty);
        Assert.Contains("top-0", sticky.GetAttribute("class") ?? string.Empty);
    }

    // -- review round 2 --------------------------------------------------------

    [Fact]
    public void Today_is_announced_and_not_only_coloured()
    {
        // The marker was CSS classes and a data- attribute, so a screen reader read today as the
        // same weekday and number as every other column.
        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, D(2026, 4, 15))
            .Add(c => c.Days, 7)
            .Add(c => c.Today, D(2026, 4, 15))
            .Add(c => c.Events, Array.Empty<L.SchedulerEvent>()));

        var current = cut.FindAll("[data-dayheader][aria-current]");
        Assert.Single(current);
        Assert.Equal("date", current[0].GetAttribute("aria-current"));
        Assert.Equal("2026-04-15", current[0].GetAttribute("data-dayheader"));

        // And nothing else claims it — aria-current on every column would announce nothing.
        Assert.Equal(7, cut.FindAll("[data-dayheader]").Count);
    }

    [Fact]
    public void The_day_number_follows_the_cultures_own_calendar()
    {
        // DateTime.Day is always the GREGORIAN day. Under a non-Gregorian calendar the header
        // printed a number that disagreed with the culture-formatted title above it.
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            var arabic = new CultureInfo("ar-SA");
            CultureInfo.CurrentUICulture = arabic;

            var day = D(2026, 4, 15);
            var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
                .Add(c => c.AnchorDate, day)
                .Add(c => c.Days, 1)
                .Add(c => c.Events, Array.Empty<L.SchedulerEvent>()));

            var expected = day.ToString("%d", arabic);
            Assert.Contains(expected, cut.Find("[data-dayheader]").TextContent);

            // Only meaningful if the culture's calendar actually disagrees with the Gregorian
            // one — otherwise this test would pass against the bug it exists for.
            Assert.NotEqual(day.Day.ToString(CultureInfo.InvariantCulture), expected);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void A_tall_all_day_strip_scrolls_instead_of_covering_the_grid()
    {
        // The strip grows a lane per overlapping all-day event. Inside a sticky block that grew
        // past the viewport it covered the timed rows for the whole scroll range, leaving every
        // slot behind it invisible and unclickable.
        var day = D(2026, 4, 15);
        var events = Enumerable.Range(0, 24)
            .Select(i => new L.SchedulerEvent($"a{i}", $"All day {i}", day, day.AddDays(1)) { AllDay = true })
            .ToArray();

        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, day)
            .Add(c => c.Days, 7)
            .Add(c => c.Events, events));

        // Capped by LANES, not by a scrollbar: a scroller here takes its width out of this row
        // alone, which pulls the strip's columns out from under the header above and the grid
        // below — the same width mismatch the header fix removed, one level down.
        var strip = cut.Find("[data-testid='timegrid-allday-strip']");
        var cls = strip.GetAttribute("class") ?? string.Empty;
        Assert.DoesNotContain("overflow-y-auto", cls);
        Assert.DoesNotContain("overflow-auto", cls);

        // Three lanes drawn, the rest counted.
        var firstDay = cut.FindAll("[data-testid='allday-more']");
        Assert.NotEmpty(firstDay);
        Assert.Equal(24 - 3, int.Parse(firstDay[0].GetAttribute("data-hidden-count")!, CultureInfo.InvariantCulture));

        // Every day column shows at most the lane budget.
        var lanes = cut.FindAll("[data-testid='timegrid-allday-strip'] [data-lane]");
        Assert.All(lanes, l => Assert.True(
            int.Parse(l.GetAttribute("data-lane")!, CultureInfo.InvariantCulture) < 3,
            "an all-day lane was drawn past the budget"));
    }

    [Fact]
    public void The_hidden_all_day_events_can_be_opened_and_clicked()
    {
        // Capping the lanes takes the surplus out of the DOM. A bare count would leave the fourth
        // and later appointments visible as a number and openable by nothing (Codex review,
        // PR #427) — so the overflow is a button with the month grid's own popover behind it.
        var day = D(2026, 4, 15);
        var events = Enumerable.Range(0, 6)
            .Select(i => new L.SchedulerEvent($"a{i}", $"All day {i}", day, day.AddDays(1)) { AllDay = true })
            .ToArray();

        L.SchedulerEvent? clicked = null;
        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, day)
            .Add(c => c.Days, 7)
            .Add(c => c.Events, events)
            .Add(c => c.OnEventClick, (L.SchedulerEvent e) => clicked = e));

        var more = cut.Find("[data-testid='allday-more']");
        Assert.Equal("3", more.GetAttribute("data-hidden-count"));
        Assert.Empty(cut.FindAll("[data-testid='allday-more-popover']"));

        more.Click();

        var popover = cut.Find("[data-testid='allday-more-popover']");
        var listed = popover.QuerySelectorAll("[data-event-id]");
        Assert.Equal(3, listed.Length);

        // The three the lanes could not hold, and no others.
        Assert.Equal(
            new[] { "a3", "a4", "a5" },
            listed.Select(l => l.GetAttribute("data-event-id")).OrderBy(x => x, StringComparer.Ordinal).ToArray());

        cut.Find("[data-testid='allday-more-popover'] [data-event-id='a4']").Click();

        Assert.Equal("a4", clicked?.Id);
        // Clicking one closes the popover, the way the month grid's does.
        Assert.Empty(cut.FindAll("[data-testid='allday-more-popover']"));
    }

    [Fact]
    public void Two_time_grids_do_not_share_one_overflow_popover_registration()
    {
        // The popover id keys the GLOBAL click-outside registry, and side-by-side calendar panes
        // put two time grids on one page — the lesson from the month grid's own version.
        var day = D(2026, 4, 15);
        var events = Enumerable.Range(0, 6)
            .Select(i => new L.SchedulerEvent($"a{i}", $"All day {i}", day, day.AddDays(1)) { AllDay = true })
            .ToArray();

        var first = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, day).Add(c => c.Days, 7).Add(c => c.Events, events));
        var second = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, day).Add(c => c.Days, 7).Add(c => c.Events, events));

        first.Find("[data-testid='allday-more']").Click();
        second.Find("[data-testid='allday-more']").Click();

        var a = first.Find("[data-testid='allday-more-popover']").Id;
        var b = second.Find("[data-testid='allday-more-popover']").Id;

        Assert.NotEqual(a, b);
    }

    // -- review round 4: the overflow popover's lifecycle ----------------------

    private L.SchedulerTimeGridView RenderOverflowGrid(out IRenderedComponent<L.SchedulerTimeGridView> cut)
    {
        var day = D(2026, 4, 15);
        var events = Enumerable.Range(0, 6)
            .Select(i => new L.SchedulerEvent($"a{i}", $"All day {i}", day, day.AddDays(1)) { AllDay = true })
            .ToArray();

        cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, day)
            .Add(c => c.Days, 7)
            .Add(c => c.Events, events));
        return cut.Instance;
    }

    [Fact]
    public void The_overflow_registration_names_its_trigger()
    {
        // Without it the document-level mousedown reads a second press on the button as an
        // OUTSIDE click and closes the popover before @onclick runs — so the click reopened what
        // it meant to close, and the button never closed anything.
        RenderOverflowGrid(out var cut);
        var trigger = cut.Find("[data-testid='allday-more']");
        var triggerId = trigger.Id;
        Assert.False(string.IsNullOrEmpty(triggerId), "the trigger has no id to register");

        trigger.Click();

        var registration = _interop.ClickOutsideRegistrations
            .Single(r => r.ElementId == cut.Find("[data-testid='allday-more-popover']").Id);
        Assert.Equal(triggerId, registration.TriggerElementId);
    }

    [Fact]
    public async Task Dismissing_from_outside_unregisters_the_handler()
    {
        // Clearing only the bookkeeping flag left the JS handler map and the interop dictionary
        // holding a callback for a dialog that had left the DOM — and with the flag cleared, no
        // later close path could reach it.
        RenderOverflowGrid(out var cut);
        cut.Find("[data-testid='allday-more']").Click();

        var popoverId = cut.Find("[data-testid='allday-more-popover']").Id;
        var registration = _interop.ClickOutsideRegistrations.Single(r => r.ElementId == popoverId);

        await cut.InvokeAsync(() => registration.Handler());

        Assert.Contains(popoverId, _interop.ClickOutsideUnregistrations);
        Assert.Empty(cut.FindAll("[data-testid='allday-more-popover']"));
    }

    [Fact]
    public void Escape_on_the_trigger_closes_the_popover()
    {
        // A keyboard user who opens with Enter still holds focus on the BUTTON, where the
        // dialog's own keydown handler cannot hear anything.
        RenderOverflowGrid(out var cut);
        var trigger = cut.Find("[data-testid='allday-more']");
        trigger.Click();
        Assert.Single(cut.FindAll("[data-testid='allday-more-popover']"));

        cut.Find("[data-testid='allday-more']").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Empty(cut.FindAll("[data-testid='allday-more-popover']"));
    }

    [Fact]
    public void The_last_columns_popover_opens_towards_the_inside()
    {
        // A 14rem panel hung off the last day column lands past the grid's right edge, inside a
        // scroller that clips — so part of the list is only reachable by discovering a horizontal
        // scrollbar.
        var day = D(2026, 4, 15);
        var lastDay = day.AddDays(4);   // the anchor week's Sunday, with the overflow on it
        var events = Enumerable.Range(0, 6)
            .Select(i => new L.SchedulerEvent($"z{i}", $"All day {i}", lastDay, lastDay.AddDays(1)) { AllDay = true })
            .ToArray();

        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, day)
            .Add(c => c.Days, 7)
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Monday)
            .Add(c => c.Events, events));

        cut.Find("[data-testid='allday-more']").Click();

        var cls = cut.Find("[data-testid='allday-more-popover']").GetAttribute("class") ?? string.Empty;
        Assert.Contains("end-0", cls);
        Assert.DoesNotContain("start-0", cls);
    }

    [Fact]
    public void A_ten_day_window_renders_an_all_day_event_on_its_ninth_day()
    {
        // The all-day strip packs through the MONTH row packer, which is hard-coded to seven
        // columns: an event starting on the eighth or a later day clamped its start to 7 and then
        // asked Math.Clamp for a range of [8, 7], which throws — so a supported MultiDay window
        // failed to render at all as soon as such an event was present (Codex review, PR #427).
        var start = D(2026, 4, 13);
        var ninth = start.AddDays(8);

        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, start)
            .Add(c => c.Days, 10)
            .Add(c => c.Events, new[]
            {
                new L.SchedulerEvent("late", "Late offsite", ninth, ninth.AddDays(1)) { AllDay = true },
                new L.SchedulerEvent("later", "Later offsite", ninth.AddDays(1), ninth.AddDays(2)) { AllDay = true },
            }));

        Assert.Equal(10, cut.FindAll("[data-dayheader]").Count);
        Assert.Single(cut.FindAll("[data-event-id='late']"));

        // Both on lane 0, which is what packing against the window's real width buys. Days
        // nine and ten are consecutive and overlap nothing, but a seven-wide packer clamps
        // every day-eight-or-later start onto the SAME last column — so it reads them as
        // overlapping and stacks the second onto a lane of its own.
        Assert.Equal("0", cut.Find("[data-event-id='late']").GetAttribute("data-lane"));
        Assert.Equal("0", cut.Find("[data-event-id='later']").GetAttribute("data-lane"));
    }

    [Fact]
    public void The_overflow_dialog_scrolls_rather_than_running_off_the_bottom()
    {
        // The lane cap can hide two dozen events. An uncapped list ran past the bottom of a
        // scroller that clips while the sticky dialog stayed pinned, so the last entries were
        // unreachable (Codex review, PR #427).
        var day = D(2026, 4, 15);
        var events = Enumerable.Range(0, 24)
            .Select(i => new L.SchedulerEvent($"a{i}", $"All day {i}", day, day.AddDays(1)) { AllDay = true })
            .ToArray();

        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, day).Add(c => c.Days, 7).Add(c => c.Events, events));

        cut.Find("[data-testid='allday-more']").Click();

        var list = cut.Find("[data-testid='allday-more-popover'] div");
        var cls = list.GetAttribute("class") ?? string.Empty;
        Assert.Contains("max-h-", cls);
        Assert.Contains("overflow-y-auto", cls);
    }

    [Fact]
    public async Task Disposing_the_grid_releases_an_open_overflow_registration()
    {
        // Switching views tears the grid down, dialog and all. Without a teardown path the JS
        // handler map and the interop dictionary keep the callback - and the component behind it.
        var day = D(2026, 4, 15);
        var events = Enumerable.Range(0, 6)
            .Select(i => new L.SchedulerEvent($"a{i}", $"All day {i}", day, day.AddDays(1)) { AllDay = true })
            .ToArray();

        var ctx = new BunitContext();
        var interop = new TrackingInteropService();
        try
        {
            ctx.AddLumeoServices();
            ctx.Services.AddSingleton<IComponentInteropService>(interop);

            var cut = ctx.Render<L.SchedulerTimeGridView>(p => p
                .Add(c => c.AnchorDate, day).Add(c => c.Days, 7).Add(c => c.Events, events));

            cut.Find("[data-testid='allday-more']").Click();
            var popoverId = cut.Find("[data-testid='allday-more-popover']").Id;
            Assert.DoesNotContain(popoverId, interop.ClickOutsideUnregistrations);

            await cut.Instance.DisposeAsync();

            Assert.Contains(popoverId, interop.ClickOutsideUnregistrations);
        }
        finally
        {
            await ctx.DisposeAsync();
        }
    }

    // -- review round 6: what the shared scroller changed ----------------------

    [Fact]
    public void Handing_the_scroll_away_stops_the_root_being_a_scrollport()
    {
        // overflow-hidden clips to the rounded border AND makes the root a scroll container, which
        // is what a sticky header is positioned against. Once the scrolling belongs to the
        // scheduler's shared box, that container never moves, so the header travelled up with the
        // content. overflow: clip clips identically without being one (Codex review, PR #427).
        var self = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, D(2026, 4, 15)).Add(c => c.Days, 7)
            .Add(c => c.Events, Array.Empty<L.SchedulerEvent>()));
        Assert.Contains("overflow-hidden", self.Find("div").GetAttribute("class"));

        var shared = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, D(2026, 4, 15)).Add(c => c.Days, 7)
            .Add(c => c.SelfScrolling, false)
            .Add(c => c.Events, Array.Empty<L.SchedulerEvent>()));
        var cls = shared.Find("div").GetAttribute("class") ?? string.Empty;
        Assert.Contains("overflow-clip", cls);
        Assert.DoesNotContain("overflow-hidden", cls);
    }

    [Fact]
    public void A_reserved_lane_floor_gives_an_empty_pane_the_same_height()
    {
        // Panes scroll as ONE box, so a pane with all-day lanes starts its hours lower than a pane
        // with none — and the same hour can never line up, which is what side by side is for.
        var day = D(2026, 4, 15);

        var empty = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, day).Add(c => c.Days, 7)
            .Add(c => c.ReserveFullAllDayStrip, true)
            .Add(c => c.Events, Array.Empty<L.SchedulerEvent>()));

        // The strip renders even with nothing in it, holding the reserved lanes open.
        var strip = empty.Find("[data-testid='timegrid-allday-strip']");
        Assert.Equal("3", strip.GetAttribute("data-reserved-lanes"));
        Assert.Equal(7 * 3, empty.FindAll("[data-testid='timegrid-allday-strip'] [data-lane]").Count);

        // And without the floor there is no strip at all — which is the height difference.
        var unreserved = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, day).Add(c => c.Days, 7)
            .Add(c => c.Events, Array.Empty<L.SchedulerEvent>()));
        Assert.Empty(unreserved.FindAll("[data-testid='timegrid-allday-strip']"));
    }

    [Fact]
    public void An_events_refresh_that_removes_the_overflow_closes_its_dialog()
    {
        // The dialog would otherwise leave the DOM with its click-outside registration live —
        // nothing can invoke the callback any more, and if the overflow returns it reopens by
        // itself, with no user action behind it.
        var day = D(2026, 4, 15);
        var many = Enumerable.Range(0, 6)
            .Select(i => new L.SchedulerEvent($"a{i}", $"All day {i}", day, day.AddDays(1)) { AllDay = true })
            .ToArray();

        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, day).Add(c => c.Days, 7).Add(c => c.Events, many));

        cut.Find("[data-testid='allday-more']").Click();
        var popoverId = cut.Find("[data-testid='allday-more-popover']").Id;

        // A refresh drops the day back under the cap.
        cut.Render(p => p.Add(c => c.Events, many.Take(2).ToArray()));

        Assert.Empty(cut.FindAll("[data-testid='allday-more-popover']"));
        Assert.Contains(popoverId, _interop.ClickOutsideUnregistrations);
    }

    [Fact]
    public void Reserving_the_strip_survives_a_day_that_overflows_it()
    {
        // The floor used to be a caller-supplied COUNT, and the wrapper handed it the number of
        // all-day events — so four of them reached Math.Clamp(x, 4, 3), whose minimum above its
        // maximum throws and took the whole scheduler down (Codex review, PR #427). A switch
        // cannot express a floor above the cap.
        var day = D(2026, 4, 15);
        var events = Enumerable.Range(0, 5)
            .Select(i => new L.SchedulerEvent($"a{i}", $"All day {i}", day, day.AddDays(1)) { AllDay = true })
            .ToArray();

        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, day)
            .Add(c => c.Days, 7)
            .Add(c => c.ReserveFullAllDayStrip, true)
            .Add(c => c.Events, events));

        Assert.Equal("3", cut.Find("[data-testid='timegrid-allday-strip']").GetAttribute("data-reserved-lanes"));
        Assert.Equal("2", cut.Find("[data-testid='allday-more']").GetAttribute("data-hidden-count"));
    }

    // -- review round 7 --------------------------------------------------------

    [Fact]
    public void A_reserved_strip_holds_the_overflow_row_open_too()
    {
        // The trigger is a row of its own, so a pane that overflows stands one row taller than a
        // pane that does not — and the lane floor alone still left their hours offset.
        var day = D(2026, 4, 15);
        var few = Enumerable.Range(0, 2)
            .Select(i => new L.SchedulerEvent($"a{i}", $"All day {i}", day, day.AddDays(1)) { AllDay = true })
            .ToArray();

        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, day).Add(c => c.Days, 7)
            .Add(c => c.ReserveFullAllDayStrip, true)
            .Add(c => c.Events, few));

        // No overflow on any day, yet every column holds the trigger's row.
        Assert.Empty(cut.FindAll("[data-testid='allday-more']"));
        Assert.Equal(7, cut.FindAll("[data-testid='allday-more-placeholder']").Count);

        // And a day that DOES overflow gets the trigger instead of the placeholder, not both.
        var many = Enumerable.Range(0, 6)
            .Select(i => new L.SchedulerEvent($"b{i}", $"All day {i}", day, day.AddDays(1)) { AllDay = true })
            .ToArray();
        cut.Render(p => p.Add(c => c.Events, many));

        Assert.Single(cut.FindAll("[data-testid='allday-more']"));
        Assert.Equal(6, cut.FindAll("[data-testid='allday-more-placeholder']").Count);
    }

    [Fact]
    public void Navigating_closes_a_popover_whose_column_now_shows_another_day()
    {
        // The open popover is keyed by column INDEX, and navigation rebuilds the columns under it
        // — so an index that still overflows in the new range kept the popover open over a
        // different day's events (CodeRabbit review, PR #427).
        var day = D(2026, 4, 15);
        var week = Enumerable.Range(0, 6)
            .Select(i => new L.SchedulerEvent($"a{i}", $"All day {i}", day, day.AddDays(1)) { AllDay = true });
        var nextWeek = Enumerable.Range(0, 6)
            .Select(i => new L.SchedulerEvent($"n{i}", $"Next {i}", day.AddDays(7), day.AddDays(8)) { AllDay = true });
        var events = week.Concat(nextWeek).ToArray();

        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, day).Add(c => c.Days, 7).Add(c => c.Events, events));

        cut.Find("[data-testid='allday-more']").Click();
        Assert.Single(cut.FindAll("[data-testid='allday-more-popover']"));

        // The very same column index overflows next week too.
        cut.Render(p => p.Add(c => c.AnchorDate, day.AddDays(7)));

        Assert.Empty(cut.FindAll("[data-testid='allday-more-popover']"));
    }

    [Fact]
    public void Escape_puts_focus_back_on_the_trigger_before_closing()
    {
        // Escape usually arrives from a button INSIDE the popover, and closing removes the focused
        // element — which drops focus onto the document body.
        var day = D(2026, 4, 15);
        var events = Enumerable.Range(0, 6)
            .Select(i => new L.SchedulerEvent($"a{i}", $"All day {i}", day, day.AddDays(1)) { AllDay = true })
            .ToArray();

        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, day).Add(c => c.Days, 7).Add(c => c.Events, events));

        var triggerId = cut.Find("[data-testid='allday-more']").Id;
        cut.Find("[data-testid='allday-more']").Click();

        cut.Find("[data-testid='allday-more-popover']").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Contains(triggerId, _interop.FocusElementCalls);
        Assert.Empty(cut.FindAll("[data-testid='allday-more-popover']"));
    }

    // -- review round 8 --------------------------------------------------------

    [Fact]
    public void Losing_every_all_day_event_closes_the_overflow_dialog_too()
    {
        // The rebuild's early return skipped the reconciliation the long path ends with, so a
        // refresh that removed EVERY all-day event left the dialog's state and its click-outside
        // registration behind (Codex review, PR #427).
        var day = D(2026, 4, 15);
        var events = Enumerable.Range(0, 6)
            .Select(i => new L.SchedulerEvent($"a{i}", $"All day {i}", day, day.AddDays(1)) { AllDay = true })
            .ToArray();

        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, day).Add(c => c.Days, 7).Add(c => c.Events, events));

        cut.Find("[data-testid='allday-more']").Click();
        var popoverId = cut.Find("[data-testid='allday-more-popover']").Id;

        // Every all-day event goes; one timed event stays so the view still renders.
        cut.Render(p => p.Add(c => c.Events, new[]
        {
            new L.SchedulerEvent("t1", "Standup", day.AddHours(9), day.AddHours(10)),
        }));

        Assert.Empty(cut.FindAll("[data-testid='allday-more-popover']"));
        Assert.Contains(popoverId, _interop.ClickOutsideUnregistrations);
    }

    [Theory]
    [InlineData(7, 4, false)]    // a week: only the last two columns overhang
    [InlineData(7, 5, true)]
    [InlineData(14, 9, false)]   // fourteen days: the panel covers four of them
    [InlineData(14, 10, true)]
    public void The_overflow_popover_opens_inward_from_however_many_columns_it_overhangs(
        int days, int column, bool opensInward)
    {
        // The panel is a fixed 14rem against columns of gridWidth/Days, so it covers more of them
        // the more days are shown — a fixed two was right for a week and left an eight-to-fourteen
        // day window opening outward from its third and fourth columns (Codex review, PR #427).
        var start = D(2026, 4, 13);
        var overflowDay = start.AddDays(column);
        var events = Enumerable.Range(0, 6)
            .Select(i => new L.SchedulerEvent($"a{i}", $"All day {i}", overflowDay, overflowDay.AddDays(1)) { AllDay = true })
            .ToArray();

        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, start)
            .Add(c => c.Days, days)
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Monday)
            .Add(c => c.Events, events));

        cut.Find("[data-testid='allday-more']").Click();
        var cls = cut.Find("[data-testid='allday-more-popover']").GetAttribute("class") ?? string.Empty;

        Assert.Contains(opensInward ? "end-0" : "start-0", cls);
    }

}
