using Bunit;
using Lumeo.Tests.Helpers;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Scheduler;

/// <summary>
/// Calendars decide whether an event is shown at all, which is what separates them from
/// resources — those decide where it sits. These pin the distinction and the two places it is
/// easy to get wrong: what "no calendar" means, and what happens when the user's own toggling
/// meets a parent re-render.
/// </summary>
public class SchedulerCalendarTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SchedulerCalendarTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static readonly DateTime Anchor = new(2026, 3, 9);   // a Monday

    private static readonly L.SchedulerCalendar[] TwoCalendars =
    [
        new("team", "Team", "rebeccapurple"),
        new("personal", "Personal", "seagreen"),
    ];

    private static L.SchedulerEvent Event(string id, string? calendarId, int dayOffset = 0) =>
        new(id, id, Anchor.AddDays(dayOffset).AddHours(9), Anchor.AddDays(dayOffset).AddHours(10))
        {
            CalendarId = calendarId,
        };

    private IRenderedComponent<L.Scheduler> Render(
        IEnumerable<L.SchedulerEvent> events,
        IReadOnlyList<L.SchedulerCalendar>? calendars = null,
        L.SchedulerPaneMode mode = L.SchedulerPaneMode.Overlay) =>
        _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialView, L.SchedulerView.Week)
            .Add(c => c.InitialDate, Anchor)
            .Add(c => c.Events, events)
            .Add(c => c.Calendars, calendars ?? TwoCalendars)
            .Add(c => c.PaneMode, mode));

    private static int Chips(IRenderedComponent<L.Scheduler> cut) =>
        cut.FindAll("[data-scheduler-calendar]").Count;

    // ── visibility ───────────────────────────────────────────────────────────

    [Fact]
    public void Every_calendar_gets_a_chip()
    {
        Assert.Equal(2, Chips(Render(new[] { Event("a", "team") })));
    }

    [Fact]
    public void No_chips_without_calendars()
    {
        // The row is a cost, not a decoration: a caller who never mentions calendars must not
        // pay a strip of chrome for the feature.
        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialView, L.SchedulerView.Week)
            .Add(c => c.InitialDate, Anchor)
            .Add(c => c.Events, new[] { Event("a", null) }));

        Assert.Empty(cut.FindAll("[data-scheduler-calendar]"));
    }

    [Fact]
    public void Hiding_a_calendar_removes_its_events()
    {
        var cut = Render(new[] { Event("a", "team"), Event("b", "personal") });
        Assert.Equal(2, cut.FindAll("[data-event-instance]").Count);

        cut.Find("[data-scheduler-calendar='team']").Click();

        var remaining = cut.FindAll("[data-event-instance]");
        Assert.Single(remaining);
    }

    [Fact]
    public void An_event_belonging_to_no_calendar_is_never_hidden()
    {
        // Null CalendarId means "no calendar", which no toggle owns. Hiding Team must not
        // silently swallow everything unfiled — the user would have no way to get it back.
        var cut = Render(new[] { Event("filed", "team"), Event("unfiled", null) });

        cut.Find("[data-scheduler-calendar='team']").Click();

        Assert.Single(cut.FindAll("[data-event-instance]"));
        Assert.Contains(cut.FindAll("[data-event-instance]"),
            e => (e.GetAttribute("data-event-instance") ?? "").StartsWith("unfiled", StringComparison.Ordinal));
    }

    [Fact]
    public void A_blank_calendar_id_is_a_real_calendar()
    {
        // The empty string is a legitimate id, distinct from null — the same rule ResourceId
        // follows — so an event filed under it hides with its calendar.
        var blank = new[] { new L.SchedulerCalendar("", "Unfiled") };
        var cut = Render(new[] { Event("a", "") }, blank);

        Assert.Single(cut.FindAll("[data-event-instance]"));
        cut.Find("[data-scheduler-calendar='']").Click();
        Assert.Empty(cut.FindAll("[data-event-instance]"));
    }

    [Fact]
    public void A_calendar_declared_hidden_starts_hidden()
    {
        var calendars = new[]
        {
            new L.SchedulerCalendar("team", "Team"),
            new L.SchedulerCalendar("personal", "Personal", Visible: false),
        };

        var cut = Render(new[] { Event("a", "team"), Event("b", "personal") }, calendars);

        Assert.Single(cut.FindAll("[data-event-instance]"));
    }

    [Fact]
    public void A_parent_re_render_does_not_undo_the_users_toggling()
    {
        // Visibility is component state precisely so this cannot happen: a parent that
        // re-renders with the same list would otherwise switch a calendar back on under the
        // user's hands.
        var cut = Render(new[] { Event("a", "team"), Event("b", "personal") });
        cut.Find("[data-scheduler-calendar='team']").Click();
        Assert.Single(cut.FindAll("[data-event-instance]"));

        cut.Render(p => p
            .Add(c => c.InitialView, L.SchedulerView.Week)
            .Add(c => c.InitialDate, Anchor)
            .Add(c => c.Events, new[] { Event("a", "team"), Event("b", "personal") })
            .Add(c => c.Calendars, TwoCalendars));

        Assert.Single(cut.FindAll("[data-event-instance]"));
    }

    // ── panes ────────────────────────────────────────────────────────────────

    [Fact]
    public void Overlay_draws_one_view_for_everything()
    {
        var cut = Render(new[] { Event("a", "team"), Event("b", "personal") });

        Assert.Empty(cut.FindAll("[data-scheduler-pane]"));
        Assert.Equal(2, cut.FindAll("[data-event-instance]").Count);
    }

    [Fact]
    public void Side_by_side_draws_one_pane_per_visible_calendar()
    {
        var cut = Render(new[] { Event("a", "team"), Event("b", "personal") },
                         mode: L.SchedulerPaneMode.SideBySide);

        var panes = cut.FindAll("[data-scheduler-pane]");
        Assert.Equal(2, panes.Count);

        // Each pane shows ONLY its own calendar's events — that is the question overlaying
        // cannot answer.
        Assert.All(panes, pane => Assert.Single(pane.QuerySelectorAll("[data-event-instance]")));
    }

    [Fact]
    public void Hiding_a_calendar_removes_its_pane()
    {
        var cut = Render(new[] { Event("a", "team"), Event("b", "personal") },
                         mode: L.SchedulerPaneMode.SideBySide);
        Assert.Equal(2, cut.FindAll("[data-scheduler-pane]").Count);

        cut.Find("[data-scheduler-calendar='team']").Click();

        // One visible calendar is the overlay wearing a column header, so the panes collapse.
        Assert.Empty(cut.FindAll("[data-scheduler-pane]"));
    }

    [Fact]
    public void Hiding_the_last_calendar_falls_back_to_overlay_rather_than_nothing()
    {
        var cut = Render(new[] { Event("a", "team"), Event("b", "personal") },
                         mode: L.SchedulerPaneMode.SideBySide);

        cut.Find("[data-scheduler-calendar='team']").Click();
        cut.Find("[data-scheduler-calendar='personal']").Click();

        Assert.Empty(cut.FindAll("[data-scheduler-pane]"));
        Assert.NotNull(cut.Find("[data-daycol]"));   // a view is still on screen
    }

    [Fact]
    public void The_toolbar_control_switches_modes()
    {
        var cut = Render(new[] { Event("a", "team"), Event("b", "personal") });
        Assert.Empty(cut.FindAll("[data-scheduler-pane]"));

        cut.Find("[data-scheduler-pane-toggle]").Click();

        Assert.Equal(2, cut.FindAll("[data-scheduler-pane]").Count);
    }

    [Fact]
    public void Switching_modes_reports_it_to_the_caller()
    {
        L.SchedulerPaneMode? reported = null;
        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialView, L.SchedulerView.Week)
            .Add(c => c.InitialDate, Anchor)
            .Add(c => c.Events, new[] { Event("a", "team") })
            .Add(c => c.Calendars, TwoCalendars)
            .Add(c => c.PaneModeChanged, (L.SchedulerPaneMode m) => reported = m));

        cut.Find("[data-scheduler-pane-toggle]").Click();

        Assert.Equal(L.SchedulerPaneMode.SideBySide, reported);
    }

    [Fact]
    public void One_calendar_shows_no_pane_control()
    {
        // Side by side needs a second calendar to put beside the first; offering the control
        // with one would promise a layout that cannot exist.
        var single = new[] { new L.SchedulerCalendar("team", "Team") };
        var cut = Render(new[] { Event("a", "team") }, single);

        Assert.Empty(cut.FindAll("[data-scheduler-pane-toggle]"));
    }

    // ── colour ───────────────────────────────────────────────────────────────

    [Fact]
    public void An_event_inherits_its_calendars_colour()
    {
        var cut = Render(new[] { Event("a", "team") });

        Assert.Contains("rebeccapurple",
            cut.Find("[data-event-instance]").GetAttribute("style") ?? "", StringComparison.Ordinal);
    }

    [Fact]
    public void A_resource_colour_still_beats_the_calendars()
    {
        // The resource an event consumes is more specific than the feed it arrived on, so a
        // room's colour survives being filed under Team.
        var ev = new L.SchedulerEvent("a", "a", Anchor.AddHours(9), Anchor.AddHours(10), ResourceId: "r1")
        {
            CalendarId = "team",
        };

        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialView, L.SchedulerView.Week)
            .Add(c => c.InitialDate, Anchor)
            .Add(c => c.Events, new[] { ev })
            .Add(c => c.Calendars, TwoCalendars)
            .Add(c => c.Resources, new[] { new L.SchedulerResource("r1", "Room 1", "darkorange") }));

        var style = cut.Find("[data-event-instance]").GetAttribute("style") ?? "";
        Assert.Contains("darkorange", style, StringComparison.Ordinal);
        Assert.DoesNotContain("rebeccapurple", style, StringComparison.Ordinal);
    }

    // -- one scrollbar for every pane -----------------------------------------

    [Theory]
    [InlineData(L.SchedulerView.Week)]
    [InlineData(L.SchedulerView.Day)]
    public void Side_by_side_panes_share_a_single_scroller(L.SchedulerView view)
    {
        // Each pane owning a scroller gave one scrollbar per calendar, and scrolling one left the
        // others behind — so the same hour was never on screen twice, which is the whole point of
        // putting the calendars beside each other.
        var calendars = new[]
        {
            new L.SchedulerCalendar("team", "Team"),
            new L.SchedulerCalendar("personal", "Personal"),
        };

        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialView, view)
            .Add(c => c.InitialDate, Anchor)
            .Add(c => c.Events, Array.Empty<L.SchedulerEvent>())
            .Add(c => c.Calendars, calendars)
            .Add(c => c.PaneMode, L.SchedulerPaneMode.SideBySide));

        Assert.Equal(2, cut.FindAll("[data-scheduler-pane]").Count);
        Assert.Single(ScrollersIn(cut));
        Assert.Empty(cut.FindAll("[data-scheduler-pane] > .overflow-auto"));
    }

    [Fact]
    public void Merging_the_panes_gives_the_view_its_own_scroller_back()
    {
        // The counterpart: overlaid, the view is alone in the box and scrolling itself is what
        // every other consumer already expects of it.
        var calendars = new[]
        {
            new L.SchedulerCalendar("team", "Team"),
            new L.SchedulerCalendar("personal", "Personal"),
        };

        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialView, L.SchedulerView.Week)
            .Add(c => c.InitialDate, Anchor)
            .Add(c => c.Events, Array.Empty<L.SchedulerEvent>())
            .Add(c => c.Calendars, calendars)
            .Add(c => c.PaneMode, L.SchedulerPaneMode.Overlay));

        Assert.Empty(cut.FindAll("[data-scheduler-pane]"));
        // The scheduler's own box, and the view's — the arrangement panes deliberately collapse.
        Assert.Equal(2, ScrollersIn(cut).Count);
    }

    private static IReadOnlyList<AngleSharp.Dom.IElement> ScrollersIn(IRenderedComponent<L.Scheduler> cut) =>
        cut.FindAll("*")
           .Where(e =>
           {
               var style = e.GetAttribute("style") ?? string.Empty;
               var cls = e.GetAttribute("class") ?? string.Empty;
               return style.Contains("overflow: auto", StringComparison.Ordinal)
                   || style.Contains("overflow-y: auto", StringComparison.Ordinal)
                   || cls.Split(' ').Contains("overflow-y-auto")
                   // The class form matters too: the wrapper's own per-pane box used it, and
                   // a predicate blind to it passed while the real page still had one
                   // scrollbar per calendar.
                   || cls.Split(' ').Contains("overflow-auto");
           })
           .ToList();

    [Fact]
    public void Panes_reserve_the_same_all_day_height_so_their_hours_line_up()
    {
        // One box scrolls them both by the same pixels, so an offset that differs per pane never
        // closes — the same hour would sit at two heights, which defeats the comparison the mode
        // exists for (Codex review, PR #427).
        var day = Anchor;
        var calendars = new[]
        {
            new L.SchedulerCalendar("team", "Team"),
            new L.SchedulerCalendar("personal", "Personal"),
        };

        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialView, L.SchedulerView.Week)
            .Add(c => c.InitialDate, day)
            .Add(c => c.Calendars, calendars)
            .Add(c => c.PaneMode, L.SchedulerPaneMode.SideBySide)
            .Add(c => c.Events, new[]
            {
                // Only the team calendar has anything all-day.
                new L.SchedulerEvent("a1", "Offsite", day, day.AddDays(1)) { AllDay = true, CalendarId = "team" },
                new L.SchedulerEvent("t1", "Gym", day.AddHours(7), day.AddHours(8)) { CalendarId = "personal" },
            }));

        var strips = cut.FindAll("[data-testid='timegrid-allday-strip']");
        Assert.Equal(2, strips.Count);

        // Both panes hold the strip open to the same number of lanes.
        var reserved = strips.Select(s => s.GetAttribute("data-reserved-lanes")).Distinct().ToList();
        Assert.Single(reserved);

        var lanesPerPane = cut.FindAll("[data-scheduler-pane]")
            .Select(p => p.QuerySelectorAll("[data-testid='timegrid-allday-strip'] [data-lane]").Length)
            .Distinct()
            .ToList();
        Assert.Single(lanesPerPane);
    }

    [Theory]
    [InlineData(L.SchedulerView.List)]
    [InlineData(L.SchedulerView.Resource)]
    public void Views_whose_panes_cannot_line_up_keep_their_own_scrollers(L.SchedulerView view)
    {
        // A shared scroll only means anything when the vertical axis is the SAME in every pane.
        // The clock is; a pane's own resources, its own overlap-driven row heights and an agenda's
        // own row count are not — sharing there moves both panes without aligning anything, and
        // costs each of them the headings its own scroller kept in place (Codex review, PR #427).
        var calendars = new[]
        {
            new L.SchedulerCalendar("team", "Team"),
            new L.SchedulerCalendar("personal", "Personal"),
        };

        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialView, view)
            .Add(c => c.InitialDate, Anchor)
            .Add(c => c.Events, Array.Empty<L.SchedulerEvent>())
            .Add(c => c.Resources, new[] { new L.SchedulerResource("r1", "Room 1") })
            .Add(c => c.Calendars, calendars)
            .Add(c => c.PaneMode, L.SchedulerPaneMode.SideBySide));

        Assert.Equal(2, cut.FindAll("[data-scheduler-pane]").Count);

        // Each pane still owns the box around its view. Counting scrollers anywhere would not
        // discriminate: these views scroll themselves regardless, so the pane box is the thing
        // that actually changes.
        Assert.Equal(2, cut.FindAll("[data-scheduler-pane] > .overflow-auto").Count);
    }

    [Fact]
    public void The_pane_names_stay_outside_the_shared_scroller()
    {
        // Inside it they scrolled away with the calendars, and the sticky day rows that remain are
        // identical in every pane — so nothing on screen said which calendar a column belonged to
        // (Codex review, PR #427).
        var calendars = new[]
        {
            new L.SchedulerCalendar("team", "Team"),
            new L.SchedulerCalendar("personal", "Personal"),
        };

        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialView, L.SchedulerView.Week)
            .Add(c => c.InitialDate, Anchor)
            .Add(c => c.Events, Array.Empty<L.SchedulerEvent>())
            .Add(c => c.Calendars, calendars)
            .Add(c => c.PaneMode, L.SchedulerPaneMode.SideBySide));

        var names = cut.FindAll("[data-scheduler-pane-name]");
        Assert.Equal(2, names.Count);
        Assert.Equal(new[] { "team", "personal" },
                     names.Select(n => n.GetAttribute("data-scheduler-pane-name")).ToArray());

        // None of them sits inside the box that moves.
        var scroller = ScrollersIn(cut).Single();
        Assert.Empty(scroller.QuerySelectorAll("[data-scheduler-pane-name]"));
    }

}
