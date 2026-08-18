using Bunit;
using Lumeo.Tests.Helpers;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Scheduler;

/// <summary>
/// The built-in dialog is OPT-IN and additive. Most of these exist to pin the second half of
/// that promise: a caller's own appointment workflow keeps running whether the dialog is on or
/// off, because displacing it is the one thing this feature must never do.
/// </summary>
public class SchedulerEventDialogTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SchedulerEventDialogTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static readonly DateTime Anchor = new(2026, 3, 9, 0, 0, 0);   // a Monday

    private static L.SchedulerEvent Existing() =>
        new("e1", "Standup", Anchor.AddHours(9), Anchor.AddHours(10));

    private IRenderedComponent<L.Scheduler> Render(
        bool dialog,
        IEnumerable<L.SchedulerEvent>? events = null,
        Action<L.SchedulerEvent>? onEventClick = null,
        Action<IEnumerable<L.SchedulerEvent>>? onEventsChanged = null) =>
        _ctx.Render<L.Scheduler>(p =>
        {
            p.Add(c => c.InitialView, L.SchedulerView.Week);
            p.Add(c => c.InitialDate, Anchor);
            p.Add(c => c.Events, events ?? new[] { Existing() });
            p.Add(c => c.BuiltInEventDialog, dialog);
            if (onEventClick is not null) p.Add(c => c.OnEventClick, onEventClick);
            if (onEventsChanged is not null) p.Add(c => c.EventsChanged, onEventsChanged);
        });

    // ── opt-in ───────────────────────────────────────────────────────────────

    [Fact]
    public void Nothing_is_rendered_when_the_dialog_is_off()
    {
        // Off by default, and off means ABSENT: a caller with its own form pays neither markup
        // nor behaviour for a feature it did not ask for.
        var cut = Render(dialog: false);

        Assert.Empty(cut.FindAll("[data-scheduler-dialog]"));
    }

    [Fact]
    public void It_is_off_unless_asked_for()
    {
        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialView, L.SchedulerView.Week)
            .Add(c => c.InitialDate, Anchor)
            .Add(c => c.Events, new[] { Existing() }));

        Assert.Empty(cut.FindAll("[data-scheduler-dialog]"));
    }

    [Fact]
    public void Clicking_an_event_opens_it_for_editing()
    {
        var cut = Render(dialog: true);

        cut.Find("[data-event-instance]").Click();

        Assert.Equal("edit", cut.Find("[data-scheduler-dialog]").GetAttribute("data-scheduler-dialog"));
        Assert.Equal("Standup", cut.Find("[data-scheduler-dialog-title] input, input[data-scheduler-dialog-title]")
                                   .GetAttribute("value"));
    }

    // ── the promise: it never displaces the caller's workflow ────────────────

    [Fact]
    public void The_callers_own_handler_still_fires_with_the_dialog_on()
    {
        // This is the whole point. A consumer keeping its own logging, navigation or analytics
        // on OnEventClick must not lose them by opting into the form.
        L.SchedulerEvent? seen = null;
        var cut = Render(dialog: true, onEventClick: ev => seen = ev);

        cut.Find("[data-event-instance]").Click();

        Assert.NotNull(seen);
        Assert.Equal("e1", seen!.Id);
    }

    [Fact]
    public void The_callers_own_handler_still_fires_with_the_dialog_off()
    {
        L.SchedulerEvent? seen = null;
        var cut = Render(dialog: false, onEventClick: ev => seen = ev);

        cut.Find("[data-event-instance]").Click();

        Assert.NotNull(seen);
    }

    // ── editing ──────────────────────────────────────────────────────────────

    [Fact]
    public void Saving_reports_through_the_same_channel_a_drag_uses()
    {
        // EventsChanged, not a new callback: a consumer that already persists drags gets the
        // dialog's edits without learning anything.
        IEnumerable<L.SchedulerEvent>? pushed = null;
        var cut = Render(dialog: true, onEventsChanged: evts => pushed = evts);

        cut.Find("[data-event-instance]").Click();
        cut.Find("input[data-scheduler-dialog-title]").Input("Renamed");
        cut.Find("[data-scheduler-dialog-save]").Click();

        Assert.NotNull(pushed);
        Assert.Contains(pushed!, e => e.Title == "Renamed");
    }

    [Fact]
    public void Deleting_removes_the_event()
    {
        IEnumerable<L.SchedulerEvent>? pushed = null;
        var cut = Render(dialog: true, onEventsChanged: evts => pushed = evts);

        cut.Find("[data-event-instance]").Click();
        cut.Find("[data-scheduler-dialog-delete]").Click();

        Assert.NotNull(pushed);
        Assert.Empty(pushed!);
    }

    [Fact]
    public void Cancelling_changes_nothing()
    {
        IEnumerable<L.SchedulerEvent>? pushed = null;
        var cut = Render(dialog: true, onEventsChanged: evts => pushed = evts);

        cut.Find("[data-event-instance]").Click();
        cut.Find("input[data-scheduler-dialog-title]").Input("Renamed");
        cut.Find("[data-scheduler-dialog-cancel]").Click();

        Assert.Null(pushed);
    }

    [Fact]
    public void An_empty_title_cannot_be_saved()
    {
        // The Save button is the only guard the form has, so it has to actually hold.
        var cut = Render(dialog: true);

        cut.Find("[data-event-instance]").Click();
        cut.Find("input[data-scheduler-dialog-title]").Input("   ");

        Assert.True(cut.Find("[data-scheduler-dialog-save]").HasAttribute("disabled"));
    }

    [Fact]
    public void A_new_event_lands_in_the_first_VISIBLE_calendar()
    {
        // Not the first DECLARED one: creating into a calendar the user has switched off would
        // make the event vanish the moment it is saved.
        var calendars = new[]
        {
            new L.SchedulerCalendar("hidden", "Hidden", Visible: false),
            new L.SchedulerCalendar("shown", "Shown"),
        };

        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialView, L.SchedulerView.Month)
            .Add(c => c.InitialDate, Anchor)
            .Add(c => c.Events, Array.Empty<L.SchedulerEvent>())
            .Add(c => c.Calendars, calendars)
            .Add(c => c.BuiltInEventDialog, true));

        // The month grid's own create gesture, rather than reaching for the private handler:
        // a test that calls what the UI cannot reach proves nothing about the UI.
        cut.Find("[data-cell-date]").DoubleClick();

        Assert.Equal("create", cut.Find("[data-scheduler-dialog]").GetAttribute("data-scheduler-dialog"));
        Assert.Equal("shown", cut.Find("[data-scheduler-dialog-calendar]").GetAttribute("value"));
    }

    // ── the gesture must not be armed when it can do nothing ────────────────

    [Fact]
    public void No_drag_create_gesture_when_nothing_can_handle_it()
    {
        // The views arm drag-create on OnDateSelect.HasDelegate so a gesture that cannot act is
        // never offered. Routing unconditionally through the internal handler armed it for
        // everyone, and on a demo with no OnDateSelect the create gesture then intercepted the
        // drag that MOVES an event — nothing committed. The E2E suite caught it; this pins it
        // where the local gate can see it.
        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialView, L.SchedulerView.Month)
            .Add(c => c.InitialDate, Anchor)
            .Add(c => c.Events, new[] { Existing() }));

        Assert.False(cut.FindComponent<L.SchedulerMonthView>().Instance.OnDateSelect.HasDelegate);
        Assert.False(cut.FindComponent<L.SchedulerMonthView>().Instance.OnEventClick.HasDelegate);
    }

    [Fact]
    public void The_gesture_is_armed_when_the_dialog_wants_it()
    {
        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialView, L.SchedulerView.Month)
            .Add(c => c.InitialDate, Anchor)
            .Add(c => c.Events, new[] { Existing() })
            .Add(c => c.BuiltInEventDialog, true));

        Assert.True(cut.FindComponent<L.SchedulerMonthView>().Instance.OnDateSelect.HasDelegate);
    }

    [Fact]
    public void The_gesture_is_armed_when_the_caller_wants_it()
    {
        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialView, L.SchedulerView.Month)
            .Add(c => c.InitialDate, Anchor)
            .Add(c => c.Events, new[] { Existing() })
            .Add(c => c.OnDateSelect, (L.SchedulerDateRange _) => { }));

        Assert.True(cut.FindComponent<L.SchedulerMonthView>().Instance.OnDateSelect.HasDelegate);
    }

    // ── review round 1 ───────────────────────────────────────────────────────

    [Fact]
    public void A_title_only_edit_does_not_shift_the_time()
    {
        // The dialog is populated from the DISPLAY projection, so its wall-clock values have to
        // be read back through the same projection a drag uses. Storing the display reading
        // rewrote the caller's data into a different frame on every save (Codex review, PR #426).
        var utcStart = new DateTime(2026, 3, 9, 12, 0, 0, DateTimeKind.Utc);
        var ev = new L.SchedulerEvent("e1", "Standup", utcStart, utcStart.AddHours(1));

        IEnumerable<L.SchedulerEvent>? pushed = null;
        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialView, L.SchedulerView.Week)
            .Add(c => c.InitialDate, utcStart.Date)
            .Add(c => c.Events, new[] { ev })
            .Add(c => c.TimeZone, "Europe/Berlin")
            .Add(c => c.BuiltInEventDialog, true)
            .Add(c => c.EventsChanged, (IEnumerable<L.SchedulerEvent> e) => pushed = e));

        cut.Find("[data-event-instance]").Click();
        cut.Find("input[data-scheduler-dialog-title]").Input("Renamed");
        cut.Find("[data-scheduler-dialog-save]").Click();

        Assert.NotNull(pushed);
        var saved = pushed!.Single();
        Assert.Equal("Renamed", saved.Title);
        Assert.Equal(utcStart, saved.Start);
    }

    [Fact]
    public void Switching_to_all_day_drops_the_times_it_hides()
    {
        // The controls become date-only, so leaving the instants behind them intact saved values
        // the user could no longer see — and toggling back revealed them again.
        var start = new DateTime(2026, 3, 9, 14, 30, 0);
        var ev = new L.SchedulerEvent("e1", "Standup", start, start.AddHours(1));

        IEnumerable<L.SchedulerEvent>? pushed = null;
        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialView, L.SchedulerView.Week)
            .Add(c => c.InitialDate, start.Date)
            .Add(c => c.Events, new[] { ev })
            .Add(c => c.BuiltInEventDialog, true)
            .Add(c => c.EventsChanged, (IEnumerable<L.SchedulerEvent> e) => pushed = e));

        cut.Find("[data-event-instance]").Click();
        // The Switch is a clickable control, not an <input> raising onchange.
        cut.Find("[data-scheduler-dialog-allday]").Click();
        cut.Find("[data-scheduler-dialog-save]").Click();

        var saved = pushed!.Single();
        Assert.True(saved.AllDay);
        Assert.Equal(saved.Start.Date, saved.Start);
        Assert.Equal(saved.End.Date, saved.End);
    }

    [Fact]
    public void Creating_in_a_pane_files_it_into_that_pane()
    {
        // Every pane shares one handler, so without the pane's own id a range drawn in the
        // second pane was filed into the first visible calendar and appeared in the wrong column.
        var calendars = new[]
        {
            new L.SchedulerCalendar("team", "Team"),
            new L.SchedulerCalendar("personal", "Personal"),
        };

        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialView, L.SchedulerView.Month)
            .Add(c => c.InitialDate, Anchor)
            .Add(c => c.Events, Array.Empty<L.SchedulerEvent>())
            .Add(c => c.Calendars, calendars)
            .Add(c => c.PaneMode, L.SchedulerPaneMode.SideBySide)
            .Add(c => c.BuiltInEventDialog, true));

        var secondPane = cut.FindAll("[data-scheduler-pane]")[1];
        secondPane.QuerySelector("[data-cell-date]")!.DoubleClick();

        Assert.Equal("personal", cut.Find("[data-scheduler-dialog-calendar]").GetAttribute("value"));
    }

    [Fact]
    public void Each_pane_gets_its_own_overflow_popover_ids()
    {
        // The id also keys the global click-outside registry, so two month views sharing one
        // produced a popover that closed the wrong pane — and left the other undismissable.
        var calendars = new[]
        {
            new L.SchedulerCalendar("team", "Team"),
            new L.SchedulerCalendar("personal", "Personal"),
        };

        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialView, L.SchedulerView.Month)
            .Add(c => c.InitialDate, Anchor)
            // Enough on ONE day to overflow the cell's lane budget — the overflow popover is
            // the only thing that renders these ids.
            .Add(c => c.Events, Enumerable.Range(0, 16)
                .Select(i => new L.SchedulerEvent($"e{i}", $"E{i}", Anchor.AddHours(9), Anchor.AddHours(10))
                {
                    CalendarId = i % 2 == 0 ? "team" : "personal",
                })
                .ToArray())
            .Add(c => c.Calendars, calendars)
            .Add(c => c.PaneMode, L.SchedulerPaneMode.SideBySide));

        // The popover only exists while it is open, so open one in EACH pane and compare.
        foreach (var pane in cut.FindAll("[data-scheduler-pane]"))
        {
            pane.QuerySelector("[data-testid='month-more-events']")!.Click();
        }

        var ids = cut.FindAll("[data-testid='month-more-popover']")
                     .Select(e => e.Id!)
                     .ToList();

        Assert.Equal(2, ids.Count);
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }
}
