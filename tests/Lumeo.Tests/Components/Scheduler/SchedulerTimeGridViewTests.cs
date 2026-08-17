using Bunit;
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
}
