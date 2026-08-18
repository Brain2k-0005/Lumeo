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
}
