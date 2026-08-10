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

    // ── Now-indicator: one registration per visible day column, never gated on
    // a server-computed "today" (spec §2.2 — the browser decides which column,
    // if any, actually shows the line; see scheduler-views.js's own dayIso check). ──

    [Fact]
    public void NowIndicator_True_Registers_One_Line_Per_Day_Column()
    {
        _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, D(2026, 3, 11))
            .Add(c => c.Days, 7)
            .Add(c => c.NowIndicator, true));

        Assert.Equal(7, _interop.SchedulerViewsRegisterNowIndicatorCallCount);
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
}
