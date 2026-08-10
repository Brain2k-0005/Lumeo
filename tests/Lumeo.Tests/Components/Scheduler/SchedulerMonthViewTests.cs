using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Scheduler;

/// <summary>
/// bUnit coverage for <see cref="L.SchedulerMonthView"/> — markup given a fixed
/// <see cref="L.SchedulerEvent"/> input, ARIA grid semantics, EventCallback wiring, and the
/// C#-side <c>ValidateDrop</c>/<c>CommitDrag</c> JSInvokable logic (spec §7.2's bUnit half of
/// the Gantt-mirrored split — the JS-side pointer/ghost geometry itself is Playwright's job,
/// see <c>tests/Lumeo.Tests.E2E</c>).
/// </summary>
public class SchedulerMonthViewTests : IAsyncLifetime
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

    // ── Grid / ARIA semantics ────────────────────────────────────────────────

    [Fact]
    public void Renders_ARIA_Grid_With_42_Gridcells()
    {
        var cut = _ctx.Render<L.SchedulerMonthView>(p => p.Add(c => c.AnchorDate, D(2026, 3, 15)));

        Assert.NotEmpty(cut.FindAll("[role='grid']"));
        Assert.Equal(42, cut.FindAll("[role='gridcell']").Count);
    }

    [Fact]
    public void Gridcell_AriaLabel_Contains_Date_And_Event_Count()
    {
        var events = new[] { new L.SchedulerEvent("e1", "Standup", D(2026, 3, 10, 9, 0), D(2026, 3, 10, 9, 30)) };
        var cut = _ctx.Render<L.SchedulerMonthView>(p => p
            .Add(c => c.AnchorDate, D(2026, 3, 15))
            .Add(c => c.Events, events));

        var cell = cut.Find("[data-cell-date='2026-03-10']");
        var label = cell.GetAttribute("aria-label");
        Assert.Contains("10", label);
        Assert.Contains("1", label); // event count
    }

    [Fact]
    public void Renders_Event_Title_On_Its_Pill()
    {
        var events = new[] { new L.SchedulerEvent("e1", "Design Sync", D(2026, 3, 10, 9, 0), D(2026, 3, 10, 9, 30)) };
        var cut = _ctx.Render<L.SchedulerMonthView>(p => p
            .Add(c => c.AnchorDate, D(2026, 3, 15))
            .Add(c => c.Events, events));

        Assert.Contains("Design Sync", cut.Find("[data-event-id='e1']").TextContent);
    }

    [Fact]
    public void MultiDay_Event_Renders_Continuation_Only_On_Its_True_Start_Cell()
    {
        // A 3-day event starting on the 10th must show its title once (on the 10th),
        // and render a continuation (no repeated title) on the 11th/12th.
        var events = new[] { new L.SchedulerEvent("e1", "Offsite", D(2026, 3, 10), D(2026, 3, 13), AllDay: true) };
        var cut = _ctx.Render<L.SchedulerMonthView>(p => p
            .Add(c => c.AnchorDate, D(2026, 3, 15))
            .Add(c => c.Events, events));

        var pills = cut.FindAll("[data-event-id='e1']");
        Assert.Equal(3, pills.Count);
        Assert.Single(pills, el => el.TextContent.Contains("Offsite"));
    }

    // ── Callback wiring ──────────────────────────────────────────────────────

    [Fact]
    public void Clicking_An_Event_Pill_Fires_OnEventClick()
    {
        L.SchedulerEvent? clicked = null;
        var events = new[] { new L.SchedulerEvent("e1", "Standup", D(2026, 3, 10, 9, 0), D(2026, 3, 10, 9, 30)) };
        var cut = _ctx.Render<L.SchedulerMonthView>(p => p
            .Add(c => c.AnchorDate, D(2026, 3, 15))
            .Add(c => c.Events, events)
            .Add(c => c.OnEventClick, (L.SchedulerEvent e) => clicked = e));

        cut.Find("[data-event-id='e1']").Click();

        Assert.NotNull(clicked);
        Assert.Equal("e1", clicked!.Id);
    }

    [Fact]
    public void DoubleClicking_An_Empty_Cell_Fires_OnDateSelect()
    {
        L.SchedulerDateRange? selected = null;
        var cut = _ctx.Render<L.SchedulerMonthView>(p => p
            .Add(c => c.AnchorDate, D(2026, 3, 15))
            .Add(c => c.OnDateSelect, (L.SchedulerDateRange r) => selected = r));

        cut.Find("[data-cell-date='2026-03-12']").DoubleClick();

        Assert.NotNull(selected);
        Assert.Equal(D(2026, 3, 12), selected!.Start);
        Assert.True(selected.AllDay);
    }

    [Fact]
    public void Clicking_A_Cell_Fires_SelectedDateChanged()
    {
        DateTime? selected = null;
        var cut = _ctx.Render<L.SchedulerMonthView>(p => p
            .Add(c => c.AnchorDate, D(2026, 3, 15))
            .Add(c => c.SelectedDateChanged, (DateTime d) => selected = d));

        cut.Find("[data-cell-date='2026-03-08']").Click();

        Assert.Equal(D(2026, 3, 8), selected);
    }

    // ── Fail-closed CanDrop / ValidateDrop (spec §3.2) ───────────────────────

    [Fact]
    public void ValidateDrop_Returns_True_When_CanDrop_Is_Null()
    {
        // hasCanDrop:false is what actually stops JS from calling this at all — the
        // C#-side default only needs to be non-blocking for the (unreachable in
        // practice) case JS calls it anyway.
        var events = new[] { new L.SchedulerEvent("e1", "Standup", D(2026, 3, 10, 9, 0), D(2026, 3, 10, 9, 30)) };
        var cut = _ctx.Render<L.SchedulerMonthView>(p => p.Add(c => c.Events, events));

        Assert.True(cut.Instance.ValidateDrop("e1", "2026-03-12"));
    }

    [Fact]
    public void ValidateDrop_Fail_Closed_When_CanDrop_Returns_False()
    {
        // The core fail-closed assertion: a CanDrop that rejects must produce a
        // `false` verdict — an accept-by-default bug would make this assert fail
        // (it would return `true` regardless of what CanDrop says).
        var events = new[] { new L.SchedulerEvent("e1", "Standup", D(2026, 3, 10, 9, 0), D(2026, 3, 10, 9, 30)) };
        var cut = _ctx.Render<L.SchedulerMonthView>(p => p
            .Add(c => c.Events, events)
            .Add(c => c.CanDrop, (L.SchedulerEvent _, L.SchedulerScheduleDropContext _) => false));

        Assert.False(cut.Instance.ValidateDrop("e1", "2026-03-12"));
    }

    [Fact]
    public void ValidateDrop_Fail_Closed_For_Unknown_EventId()
    {
        // Deliberately stricter than the unresolved-id case elsewhere in this repo's
        // Gantt precedent (which defaults to permit): the Scheduler's CanDrop is a
        // real consumer-facing gate, so an id that can't be resolved is treated as
        // "not validated", not "assume fine".
        var cut = _ctx.Render<L.SchedulerMonthView>(p => p
            .Add(c => c.CanDrop, (L.SchedulerEvent _, L.SchedulerScheduleDropContext _) => true));

        Assert.False(cut.Instance.ValidateDrop("does-not-exist", "2026-03-12"));
    }

    [Fact]
    public void ValidateDrop_Passes_Correct_ProposedStart_Preserving_TimeOfDay()
    {
        L.SchedulerScheduleDropContext? seen = null;
        var events = new[] { new L.SchedulerEvent("e1", "Standup", D(2026, 3, 10, 9, 15), D(2026, 3, 10, 9, 45)) };
        var cut = _ctx.Render<L.SchedulerMonthView>(p => p
            .Add(c => c.Events, events)
            .Add(c => c.CanDrop, (L.SchedulerEvent _, L.SchedulerScheduleDropContext ctx) => { seen = ctx; return true; }));

        cut.Instance.ValidateDrop("e1", "2026-03-20");

        Assert.NotNull(seen);
        Assert.Equal(new DateTime(2026, 3, 20, 9, 15, 0), seen!.ProposedStart);
        Assert.Equal(new DateTime(2026, 3, 20, 9, 45, 0), seen.ProposedEnd);
        Assert.Equal(L.SchedulerEventUpdateSource.Move, seen.Source);
    }

    [Fact]
    public async Task CommitDrag_Fires_OnEventChange_With_New_Day_Same_Duration()
    {
        L.SchedulerEvent? changed = null;
        var events = new[] { new L.SchedulerEvent("e1", "Standup", D(2026, 3, 10, 9, 0), D(2026, 3, 10, 9, 30)) };
        var cut = _ctx.Render<L.SchedulerMonthView>(p => p
            .Add(c => c.Events, events)
            .Add(c => c.OnEventChange, (L.SchedulerEvent e) => changed = e));

        await cut.InvokeAsync(() => cut.Instance.CommitDrag("e1", "2026-03-17"));

        Assert.NotNull(changed);
        Assert.Equal(new DateTime(2026, 3, 17, 9, 0, 0), changed!.Start);
        Assert.Equal(new DateTime(2026, 3, 17, 9, 30, 0), changed.End);
    }

    [Fact]
    public async Task CommitDrag_Same_Day_Is_A_NoOp()
    {
        var fired = false;
        var events = new[] { new L.SchedulerEvent("e1", "Standup", D(2026, 3, 10, 9, 0), D(2026, 3, 10, 9, 30)) };
        var cut = _ctx.Render<L.SchedulerMonthView>(p => p
            .Add(c => c.Events, events)
            .Add(c => c.OnEventChange, (L.SchedulerEvent _) => fired = true));

        await cut.InvokeAsync(() => cut.Instance.CommitDrag("e1", "2026-03-10"));

        Assert.False(fired);
    }

    [Fact]
    public async Task CommitDrag_Unknown_EventId_Fires_Nothing()
    {
        var fired = false;
        var cut = _ctx.Render<L.SchedulerMonthView>(p => p
            .Add(c => c.OnEventChange, (L.SchedulerEvent _) => fired = true));

        await cut.InvokeAsync(() => cut.Instance.CommitDrag("nope", "2026-03-10"));

        Assert.False(fired);
    }

    // ── Drag registration options (hasCanDrop gate) ──────────────────────────

    [Fact]
    public void Registers_Drag_With_HasCanDrop_False_When_CanDrop_Unset()
    {
        _ctx.Render<L.SchedulerMonthView>(p => p.Add(c => c.AnchorDate, D(2026, 3, 15)));

        var options = Assert.IsType<Dictionary<string, object?>>(_interop.LastSchedulerViewsMonthDragOptions);
        Assert.Equal(false, options["hasCanDrop"]);
    }

    [Fact]
    public void Registers_Drag_With_HasCanDrop_True_When_CanDrop_Set()
    {
        _ctx.Render<L.SchedulerMonthView>(p => p
            .Add(c => c.AnchorDate, D(2026, 3, 15))
            .Add(c => c.CanDrop, (L.SchedulerEvent _, L.SchedulerScheduleDropContext _) => true));

        var options = Assert.IsType<Dictionary<string, object?>>(_interop.LastSchedulerViewsMonthDragOptions);
        Assert.Equal(true, options["hasCanDrop"]);
    }

    // ── Keyboard navigation (spec §5.2) ──────────────────────────────────────

    [Fact]
    public async Task ArrowRight_Moves_Roving_Tabindex_To_The_Next_Cell()
    {
        var cut = _ctx.Render<L.SchedulerMonthView>(p => p.Add(c => c.AnchorDate, D(2026, 3, 15)));

        var firstCell = cut.FindAll("[role='gridcell']")[0];
        Assert.Equal("0", firstCell.GetAttribute("tabindex"));

        await cut.InvokeAsync(() => firstCell.KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "ArrowRight" }));

        var cells = cut.FindAll("[role='gridcell']");
        Assert.Equal("-1", cells[0].GetAttribute("tabindex"));
        Assert.Equal("0", cells[1].GetAttribute("tabindex"));
    }
}
