using System.Globalization;
using Bunit;
using Lumeo.Tests.Helpers;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Scheduler;

/// <summary>
/// One test per finding from the review round on the resource timeline, so each fix is pinned
/// rather than merely applied. Kept apart from
/// <see cref="SchedulerTimelineViewTests"/> — that file describes what the view does; this one
/// records what it got wrong first, and why the fix is shaped the way it is.
/// </summary>
public class SchedulerTimelineReviewTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SchedulerTimelineReviewTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static readonly DateTime Start = new(2026, 3, 9);   // a Monday
    private const int ColW = 100;

    private static readonly L.SchedulerResource[] Rooms =
    [
        new("alice", "Alice"),
        new("bob", "Bob"),
    ];

    private IRenderedComponent<L.SchedulerTimelineView> Render(
        IEnumerable<L.SchedulerEvent>? events = null,
        int columns = 7,
        IReadOnlyList<L.SchedulerResource>? resources = null,
        int? columnWidth = ColW,
        int? laneHeight = null) =>
        _ctx.Render<L.SchedulerTimelineView>(p =>
        {
            p.Add(c => c.Resources, resources ?? Rooms);
            p.Add(c => c.RangeStart, Start);
            p.Add(c => c.Columns, columns);
            p.Add(c => c.Today, Start.AddDays(2));
            if (columnWidth is not null) p.Add(c => c.ColumnWidth, (int?)columnWidth);
            if (laneHeight is not null) p.Add(c => c.LaneHeight, laneHeight.Value);
            if (events is not null) p.Add(c => c.Events, events);
        });

    private static double Px(string style, string prop)
    {
        var i = style.IndexOf(prop + ":", StringComparison.Ordinal);
        Assert.True(i >= 0, $"'{prop}' not found in style '{style}'");
        var rest = style[(i + prop.Length + 1)..].TrimStart();
        return double.Parse(rest[..rest.IndexOf("px", StringComparison.Ordinal)], CultureInfo.InvariantCulture);
    }

    // ── P1: recurrence ───────────────────────────────────────────────────────

    [Fact]
    public void A_recurring_booking_renders_every_occurrence_on_the_axis()
    {
        // Laying out the series' own Start and End instead of its expanded instances showed a
        // twice-weekly booking exactly once, so the resource read as free for the rest of the
        // window. Every other first-party view expands over its visible range.
        var weekly = new L.SchedulerEvent("r", "Standup",
            Start.AddHours(9), Start.AddHours(10), ResourceId: "alice",
            DaysOfWeek: new[] { DayOfWeek.Monday, DayOfWeek.Wednesday });

        var cut = Render(new[] { weekly }, columns: 7);

        Assert.True(cut.FindAll("[data-timeline-bar='r']").Count >= 2,
            "A twice-weekly booking must render one occurrence per matching day on the axis.");
    }

    [Fact]
    public void A_series_starting_before_the_axis_still_shows_its_later_occurrences()
    {
        // The other half of the same defect: the whole series was filtered out because its
        // source Start preceded the window, even though occurrences fall inside it.
        var weekly = new L.SchedulerEvent("r", "Standup",
            Start.AddDays(-28).AddHours(9), Start.AddDays(-28).AddHours(10), ResourceId: "alice",
            DaysOfWeek: new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
                                DayOfWeek.Thursday, DayOfWeek.Friday });

        var cut = Render(new[] { weekly }, columns: 7);

        Assert.NotEmpty(cut.FindAll("[data-timeline-bar='r']"));
    }

    // ── P1: RTL ──────────────────────────────────────────────────────────────

    [Fact]
    public void The_time_axis_is_pinned_left_to_right()
    {
        // The header row is flex, so under RTL it puts the first date on the right — while
        // bars, rules and the today marker keep positioning from the physical left. The
        // bookings would sit under the wrong dates in the Arabic UI.
        var cut = Render();

        var track = cut.Find("[data-timeline-column]").ParentElement!.ParentElement!;
        Assert.Equal("ltr", track.GetAttribute("dir"));
    }

    // ── Geometry limits ──────────────────────────────────────────────────────

    [Fact]
    public void Back_to_back_short_bookings_do_not_overlap_on_screen()
    {
        // Lanes are packed by rendered geometry, not by the clock. Every bar has a minimum
        // width, and at day scale an hour is four pixels, so two sequential hour bookings
        // shared a lane by time while covering each other on screen.
        var a = new L.SchedulerEvent("a", "A", Start.AddHours(9), Start.AddHours(10), ResourceId: "alice");
        var b = new L.SchedulerEvent("b", "B", Start.AddHours(10), Start.AddHours(11), ResourceId: "alice");

        var cut = Render(new[] { a, b });
        var sa = cut.Find("[data-timeline-bar='a']").GetAttribute("style")!;
        var sb = cut.Find("[data-timeline-bar='b']").GetAttribute("style")!;

        var sameLane = Px(sa, "top") == Px(sb, "top");
        var overlap = Px(sa, "left") + Px(sa, "width") > Px(sb, "left");
        Assert.False(sameLane && overlap,
            "Bars that still overlap once the minimum width is applied must not share a lane.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-40)]
    public void A_nonpositive_column_width_falls_back_to_the_default(int bad)
    {
        // A caller binding a not-yet-measured container width hands us exactly this, and using
        // it collapses the track while piling every bar at the same offset.
        var cut = Render(columns: 3, columnWidth: bad);

        var style = cut.FindAll("[data-timeline-column]")[0].GetAttribute("style")!;
        Assert.Equal(L.SchedulerTimelineScale.DefaultColumnWidth(L.SchedulerTimelineUnit.Day),
                     (int)Px(style, "width"));
    }

    [Fact]
    public void A_tiny_lane_height_still_renders_a_visible_bar()
    {
        // Bars are drawn LaneHeight minus 4 tall, so any value at or below 4 produced a zero
        // or negative box: invisible, or several lanes stacked at the same top.
        var ev = new L.SchedulerEvent("e1", "Booking", Start, Start.AddDays(1), ResourceId: "alice");

        var cut = Render(new[] { ev }, columns: 3, laneHeight: 2);

        Assert.True(Px(cut.Find("[data-timeline-bar='e1']").GetAttribute("style")!, "height") > 0);
    }

    [Fact]
    public void A_zero_length_marker_on_the_first_column_survives()
    {
        // A strict End-after-start filter dropped it exactly when its date was the first
        // visible column, contradicting this view's own support for zero-length bookings.
        var ev = new L.SchedulerEvent("e1", "Midnight", Start, Start, ResourceId: "alice");

        Assert.Single(Render(new[] { ev }).FindAll("[data-timeline-bar='e1']"));
    }

    // ── Matching and presentation ────────────────────────────────────────────

    [Fact]
    public void A_blank_resource_id_still_matches_its_resource()
    {
        // RebuildResourceLookup and the resource view both treat a blank id as legitimate, so
        // rejecting it here made that resource look free the moment you switched view.
        var res = new[] { new L.SchedulerResource("", "Unassigned") };
        var ev = new L.SchedulerEvent("e1", "Booking", Start, Start.AddDays(1), ResourceId: "");

        var cut = Render(new[] { ev }, columns: 5, resources: res);

        Assert.Single(cut.FindAll("[data-timeline-bar='e1']"));
    }

    [Fact]
    public void A_bar_falls_back_to_its_resource_colour()
    {
        // Otherwise a direct caller sees a gutter full of distinct colours and a track drawn
        // entirely in the primary one.
        var res = new[] { new L.SchedulerResource("alice", "Alice", "rebeccapurple") };
        var ev = new L.SchedulerEvent("e1", "Booking", Start, Start.AddDays(1), ResourceId: "alice");

        var cut = Render(new[] { ev }, columns: 3, resources: res);

        Assert.Contains("rebeccapurple",
            cut.Find("[data-timeline-bar='e1']").GetAttribute("style")!, StringComparison.Ordinal);
    }

    [Fact]
    public void A_bar_announces_its_end_as_well_as_its_start()
    {
        // Width conveys duration visually only. Without the end, two bookings that start
        // together are indistinguishable to a screen reader.
        var ev = new L.SchedulerEvent("e1", "Booking",
            Start.AddHours(9), Start.AddHours(17), ResourceId: "alice");

        var label = Render(new[] { ev }).Find("[data-timeline-bar='e1']").GetAttribute("aria-label")!;

        Assert.Contains("Alice", label, StringComparison.Ordinal);
        Assert.Contains("–", label, StringComparison.Ordinal);
    }

    [Fact]
    public void The_markup_claims_no_tabular_semantics_it_cannot_deliver()
    {
        // row and columnheader roles were orphaned: their nearest container is a group, the
        // gutter and track are separate subtrees, and the bars are absolutely positioned, so
        // assistive tech could not associate a date header with a resource row.
        var cut = Render();

        Assert.Empty(cut.FindAll("[role='row']"));
        Assert.Empty(cut.FindAll("[role='columnheader']"));
        Assert.NotEmpty(cut.FindAll("[role='listitem']"));   // the resources still are a list
    }

    // ── Wrapper semantics ────────────────────────────────────────────────────

    [Fact]
    public void The_empty_resource_fallback_navigates_like_a_day()
    {
        // Only the renderer fell back before: the toolbar still described a timeline range and
        // prev/next still jumped by TimelineColumns while a single day was on screen.
        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialView, L.SchedulerView.Timeline)
            .Add(c => c.InitialDate, Start)
            .Add(c => c.TimelineColumns, 30));

        var before = cut.Find("[data-daycol]").GetAttribute("data-daycol");
        cut.FindAll("button")
           .First(b => b.GetAttribute("aria-label")?.Contains("Next", StringComparison.OrdinalIgnoreCase) == true)
           .Click();
        var after = cut.Find("[data-daycol]").GetAttribute("data-daycol");

        Assert.Equal("2026-03-09", before);
        Assert.Equal("2026-03-10", after);   // one day, not thirty
    }

    [Fact]
    public void Changing_the_column_count_updates_the_title()
    {
        // The title is cached and was refreshed only by navigation, a view change or a culture
        // change, so it kept naming the old end date after the range moved underneath it.
        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialView, L.SchedulerView.Timeline)
            .Add(c => c.InitialDate, Start)
            .Add(c => c.Resources, Rooms)
            .Add(c => c.TimelineColumns, 3));
        Assert.Contains("Mar 11", cut.Find(".text-center").TextContent, StringComparison.Ordinal);

        cut.Render(p => p
            .Add(c => c.InitialView, L.SchedulerView.Timeline)
            .Add(c => c.InitialDate, Start)
            .Add(c => c.Resources, Rooms)
            .Add(c => c.TimelineColumns, 10));

        Assert.Contains("Mar 18", cut.Find(".text-center").TextContent, StringComparison.Ordinal);
    }

    // ── Review round 2 ───────────────────────────────────────────────────────

    [Fact]
    public void Two_events_sharing_an_id_stay_on_their_own_resources()
    {
        // Ids are caller-supplied and need not be unique; the resource view already treats
        // duplicates as legitimate input. Joining expanded instances back through a
        // dictionary keyed on Id kept only the last event, so one resource read as free while
        // the other collected the bars of both.
        var a = new L.SchedulerEvent("shared", "Alice's", Start, Start.AddDays(1), ResourceId: "alice");
        var b = new L.SchedulerEvent("shared", "Bob's", Start, Start.AddDays(1), ResourceId: "bob");

        var bars = Render(new[] { a, b }, columns: 3).FindAll("[data-timeline-bar='shared']");

        Assert.Equal(2, bars.Count);
        // One per resource track, rather than both piled into the last resource's row.
        Assert.NotSame(bars[0].ParentElement, bars[1].ParentElement);
    }

    [Fact]
    public void A_bar_reports_the_source_that_actually_owns_it()
    {
        // The other half of the same defect: both bars carried the last event's title.
        var a = new L.SchedulerEvent("shared", "Alice's", Start, Start.AddDays(1), ResourceId: "alice");
        var b = new L.SchedulerEvent("shared", "Bob's", Start, Start.AddDays(1), ResourceId: "bob");

        var titles = Render(new[] { a, b }, columns: 3)
            .FindAll("[data-timeline-bar='shared']")
            .Select(e => e.GetAttribute("aria-label") ?? "")
            .ToList();

        Assert.Contains(titles, t => t.Contains("Alice's", StringComparison.Ordinal));
        Assert.Contains(titles, t => t.Contains("Bob's", StringComparison.Ordinal));
    }

    [Fact]
    public void The_horizontal_scroller_owns_the_left_to_right_origin()
    {
        // Pinning only the track inside it left the overflow container RTL, so in an Arabic UI
        // a timeline wider than its viewport opened at the far end with RangeStart off screen.
        var cut = Render(columns: 30);

        var track = cut.Find("[data-timeline-column]").ParentElement!.ParentElement!;
        var scroller = track.ParentElement!;

        Assert.Contains("overflow-x-auto", scroller.GetAttribute("class") ?? "", StringComparison.Ordinal);
        Assert.Equal("ltr", scroller.GetAttribute("dir"));
    }

    [Fact]
    public void A_daily_series_fills_an_axis_longer_than_the_old_occurrence_cap()
    {
        // Candidates are counted from the series' own start, and the flat 500 stopped short of
        // any window longer than itself: a 24-month axis is 730 daily occurrences, so its last
        // eight months rendered as free on a resource booked every single day.
        // Recurrence is an init-only body property, not a positional parameter.
        var daily = new L.SchedulerEvent("d", "Daily", Start, Start.AddHours(1), ResourceId: "alice")
        {
            Recurrence = new L.SchedulerRecurrenceRule(L.SchedulerRecurrenceFrequency.Daily, Interval: 1),
        };

        var cut = _ctx.Render<L.SchedulerTimelineView>(p =>
        {
            p.Add(c => c.Resources, Rooms);
            p.Add(c => c.RangeStart, Start);
            p.Add(c => c.Unit, L.SchedulerTimelineUnit.Month);
            p.Add(c => c.Columns, 24);
            p.Add(c => c.Today, Start);
            p.Add(c => c.Events, new[] { daily });
        });

        Assert.True(cut.FindAll("[data-timeline-bar='d']").Count > 700,
            "Every day of the drawn axis is booked, so the tail must not render as free.");
    }

    [Fact]
    public void A_range_starting_at_the_minimum_date_renders_instead_of_throwing()
    {
        // DateTime.MinValue is a common "unset" sentinel for a public parameter, and the
        // one-tick lookback before the axis underflowed before a single row rendered.
        var cut = _ctx.Render<L.SchedulerTimelineView>(p =>
        {
            p.Add(c => c.Resources, Rooms);
            p.Add(c => c.RangeStart, DateTime.MinValue);
            p.Add(c => c.Columns, 3);
            p.Add(c => c.Today, DateTime.MinValue.AddDays(1));
        });

        Assert.NotEmpty(cut.FindAll("[data-timeline-column]"));
    }

    [Fact]
    public void An_axis_anchored_at_the_last_representable_day_renders()
    {
        // RangeStart is public, and the exclusive end of the final column does not exist, so
        // advancing past it threw while measuring the track (Codex review round 4).
        var cut = _ctx.Render<L.SchedulerTimelineView>(p =>
        {
            p.Add(c => c.Resources, Rooms);
            p.Add(c => c.RangeStart, DateTime.MaxValue.Date);
            p.Add(c => c.Columns, 1);
            p.Add(c => c.Today, DateTime.MaxValue.Date);
        });

        Assert.NotEmpty(cut.FindAll("[data-timeline-column]"));
    }

    [Fact]
    public void An_event_for_a_resource_that_is_not_shown_is_never_expanded()
    {
        // A caller may render a subset of one shared feed. Expanding an orphan in full before
        // the per-resource filter discards it is pure waste — on a long axis an unbounded daily
        // orphan alone allocates thousands of instances that can never reach the screen.
        var orphan = new L.SchedulerEvent("ghost", "Elsewhere", Start, Start.AddHours(1),
                                          ResourceId: "carol")
        {
            Recurrence = new L.SchedulerRecurrenceRule(L.SchedulerRecurrenceFrequency.Daily, Interval: 1),
        };
        var mine = new L.SchedulerEvent("e1", "Booking", Start, Start.AddDays(1), ResourceId: "alice");

        var cut = Render(new[] { orphan, mine }, columns: 5);

        Assert.Empty(cut.FindAll("[data-timeline-bar='ghost']"));
        Assert.Single(cut.FindAll("[data-timeline-bar='e1']"));
    }

    [Fact]
    public void A_multi_column_axis_at_the_upper_boundary_renders()
    {
        // Clamping only the endpoints left BuildColumns throwing first for any count above one.
        var cut = _ctx.Render<L.SchedulerTimelineView>(p =>
        {
            p.Add(c => c.Resources, Rooms);
            p.Add(c => c.RangeStart, DateTime.MaxValue.Date);
            p.Add(c => c.Columns, 2);
            p.Add(c => c.Today, DateTime.MaxValue.Date);
        });

        Assert.NotEmpty(cut.FindAll("[data-timeline-column]"));
    }

    [Fact]
    public void A_week_axis_at_the_minimum_date_aligns_without_underflowing()
    {
        // AlignOrigin steps BACK to the week start, and with a first weekday other than the one
        // DateTime.MinValue falls on, that step went below the representable range.
        var cut = _ctx.Render<L.SchedulerTimelineView>(p =>
        {
            p.Add(c => c.Resources, Rooms);
            p.Add(c => c.RangeStart, DateTime.MinValue);
            p.Add(c => c.Unit, L.SchedulerTimelineUnit.Week);
            p.Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday);
            p.Add(c => c.Columns, 2);
            p.Add(c => c.Today, DateTime.MinValue);
        });

        Assert.NotEmpty(cut.FindAll("[data-timeline-column]"));
    }

    [Fact]
    public void The_wrapper_opens_a_timeline_at_the_upper_boundary_without_throwing()
    {
        // The title describes the same axis and formatted it with unchecked advances, so it
        // threw during OnInitialized while the view itself was already safe.
        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialView, L.SchedulerView.Timeline)
            .Add(c => c.InitialDate, DateTime.MaxValue.Date)
            .Add(c => c.Resources, Rooms));

        Assert.NotNull(cut.Find(".text-center"));
    }

    // ── Review round 6 ───────────────────────────────────────────────────────

    [Fact]
    public void An_axis_that_reaches_the_end_of_the_calendar_is_shorter_not_repeated()
    {
        // Clamping each step to DateTime.MaxValue produced a run of identical final columns —
        // the same date drawn over and over, headers overflowing a track measured for one.
        var cut = _ctx.Render<L.SchedulerTimelineView>(p =>
        {
            p.Add(c => c.Resources, Rooms);
            p.Add(c => c.RangeStart, DateTime.MaxValue.Date);
            p.Add(c => c.Columns, 5);
            p.Add(c => c.Today, DateTime.MaxValue.Date);
        });

        var labels = cut.FindAll("[data-timeline-column]").Select(e => e.TextContent.Trim()).ToList();

        Assert.Single(labels);
        Assert.Equal(labels.Count, labels.Distinct().Count());
    }

    [Fact]
    public void Paging_a_boundary_timeline_does_not_crash_it()
    {
        // A view that renders must not be crashable by its own toolbar: Next at the upper
        // boundary, and Previous at the lower one, both stepped with a raw Add.
        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialView, L.SchedulerView.Timeline)
            .Add(c => c.InitialDate, DateTime.MaxValue.Date)
            .Add(c => c.Resources, Rooms));

        cut.FindAll("button")
           .First(b => b.GetAttribute("aria-label")?.Contains("Next", StringComparison.OrdinalIgnoreCase) == true)
           .Click();

        Assert.NotNull(cut.Find(".text-center"));
    }

    [Fact]
    public void The_title_of_a_boundary_timeline_reads_forwards()
    {
        // exclusiveEnd saturates at the boundary, and subtracting a day from it turned the
        // title around: "Dec 31 – Dec 30, 9999".
        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialView, L.SchedulerView.Timeline)
            .Add(c => c.InitialDate, DateTime.MaxValue.Date)
            .Add(c => c.Resources, Rooms));

        var title = cut.Find(".text-center").TextContent;

        Assert.DoesNotContain("Dec 31 – Dec 30", title, StringComparison.Ordinal);
    }

    [Fact]
    public void Clicking_the_fallback_day_commits_it_as_the_selected_view()
    {
        // A single-select ToggleGroup emits null when the pressed item is clicked again, so the
        // fallback only LOOKED selected: _currentView stayed on Timeline and that hidden view
        // came back as soon as Resources was repopulated.
        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialView, L.SchedulerView.Timeline)
            .Add(c => c.InitialDate, Start));      // no Resources -> falls back to Day

        cut.FindAll("button").First(b => b.TextContent.Trim() == "Day").Click();

        cut.Render(p => p
            .Add(c => c.InitialView, L.SchedulerView.Timeline)
            .Add(c => c.InitialDate, Start)
            .Add(c => c.Resources, Rooms));        // resources come back

        Assert.Empty(cut.FindAll("[data-timeline-column]"));
    }

    [Fact]
    public void An_unassigned_event_is_not_expanded_for_a_blank_resource()
    {
        // A blank id is a supported resource, but null is not the same value. Coercing one to
        // the other let every unassigned event through the prefilter, only for the ordinal
        // comparison to discard it — the exact waste the prefilter exists to prevent.
        var res = new[] { new L.SchedulerResource("", "Unassigned") };
        var unassigned = new L.SchedulerEvent("ghost", "No resource", Start, Start.AddHours(1));
        var blank = new L.SchedulerEvent("e1", "Blank id", Start, Start.AddDays(1), ResourceId: "");

        var cut = Render(new[] { unassigned, blank }, columns: 3, resources: res);

        Assert.Empty(cut.FindAll("[data-timeline-bar='ghost']"));
        Assert.Single(cut.FindAll("[data-timeline-bar='e1']"));
    }

    // ── Review round 7 ───────────────────────────────────────────────────────

    [Fact]
    public void A_saturated_month_title_names_the_last_visible_day()
    {
        // The final column's START is not its last visible day: a December axis drawing through
        // the 31st was titled "Dec 1 – Dec 1, 9999".
        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialView, L.SchedulerView.Timeline)
            .Add(c => c.TimelineUnit, L.SchedulerTimelineUnit.Month)
            .Add(c => c.InitialDate, DateTime.MaxValue.Date)
            .Add(c => c.Resources, Rooms));

        Assert.Contains("Dec 31", cut.Find(".text-center").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void The_track_is_as_wide_as_the_columns_it_draws()
    {
        // A week running off the end of the calendar is about 5/7 of a column wide BY DATE,
        // while its header and grid rule still paint a full one. Measuring the track from the
        // clamped end date made the two disagree and the surplus showed as phantom days.
        const int w = 120;
        var cols = L.SchedulerTimelineScale.BuildColumns(
            L.SchedulerTimelineUnit.Week, new DateTime(9999, 12, 27), 3);

        Assert.Equal(cols.Count * (double)w,
            L.SchedulerTimelineScale.TotalWidth(L.SchedulerTimelineUnit.Week, new DateTime(9999, 12, 27), 3, w));
    }

    [Fact]
    public void A_bar_with_a_url_renders_as_a_link()
    {
        // SchedulerEvent.Url documents that the URL is opened; the bar was a button either way,
        // so a URL-backed booking was inert when no callback was registered.
        var ev = new L.SchedulerEvent("e1", "Booking", Start, Start.AddDays(1),
                                      ResourceId: "alice", Url: "https://example.test/booking");

        var bar = Render(new[] { ev }, columns: 3).Find("[data-timeline-bar='e1']");

        Assert.Equal("A", bar.TagName, ignoreCase: true);
        Assert.Equal("https://example.test/booking", bar.GetAttribute("href"));
    }

    [Fact]
    public void A_bar_without_a_url_stays_a_button()
    {
        var ev = new L.SchedulerEvent("e1", "Booking", Start, Start.AddDays(1), ResourceId: "alice");

        Assert.Equal("BUTTON", Render(new[] { ev }, columns: 3)
            .Find("[data-timeline-bar='e1']").TagName, ignoreCase: true);
    }

    [Fact]
    public void A_short_booking_at_the_right_edge_stays_inside_the_track()
    {
        // The minimum width is a floor, and applying it to a booking that ends exactly at the
        // axis edge pushed the bar into a day that does not exist, stretching the scrollable
        // area past the advertised range.
        var last = Start.AddDays(2);                       // final column of a 3-column axis
        var ev = new L.SchedulerEvent("e1", "Late", last.AddHours(23.5), last.AddHours(24),
                                      ResourceId: "alice");

        var style = Render(new[] { ev }, columns: 3).Find("[data-timeline-bar='e1']").GetAttribute("style")!;

        Assert.True(Px(style, "left") + Px(style, "width") <= 3 * ColW + 0.01,
            $"bar ends at {Px(style, "left") + Px(style, "width")}px, track is {3 * ColW}px");
    }

    [Fact]
    public void The_wrapper_hands_the_timeline_a_resolved_today_rather_than_null()
    {
        // Passing null let the view fall back to the HOST's DateTime.Today while this wrapper's
        // own Today button resolves the BROWSER's date. On a Blazor Server circuit those can be
        // different days, so after clicking Today the axis moved and the marker stayed behind.
        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialView, L.SchedulerView.Timeline)
            .Add(c => c.InitialDate, Start)
            .Add(c => c.Resources, Rooms)
            .Add(c => c.TimelineColumns, 7));

        var view = cut.FindComponent<L.SchedulerTimelineView>();

        Assert.NotNull(view.Instance.Today);
    }

    [Fact]
    public void The_browser_date_is_resolved_through_the_schedulers_own_asset()
    {
        // It used to come from GanttV3GetLocalDateAsync, whose module lives in Lumeo.Gantt — a
        // package Lumeo.Scheduler does not reference. In the documented standalone install that
        // import 404s, the failure is swallowed, and the marker silently falls back to the
        // SERVER's date: the very case the resolution exists for (Codex review round 10).
        var source = System.IO.File.ReadAllText(SchedulerSourcePath());

        Assert.Contains("SchedulerViewsGetLocalDateAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GanttV3GetLocalDateAsync", source, StringComparison.Ordinal);
    }

    private static string SchedulerSourcePath()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Lumeo.slnx")))
            dir = Path.GetDirectoryName(dir);
        Assert.NotNull(dir);
        return Path.Combine(dir!, "src", "Lumeo.Scheduler", "UI", "Scheduler", "Scheduler.razor");
    }

    [Fact]
    public void The_scheduler_view_module_is_cache_busted()
    {
        // The browser-date resolution depends on an export this file only gained in this
        // release. Without the version query a browser holding the previous copy finds
        // getLocalDateIso missing, the call rejects, and the marker falls back to the SERVER's
        // date without saying so — the same silent defeat the Gantt-module dependency caused
        // (CodeRabbit, PR #424).
        var root = RepoRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "Lumeo", "Services", "ComponentInteropService.cs"));

        // The exact import expression, not a nearby line: an earlier COMMENT also names the
        // file, and anchoring on the first mention inspected that comment instead.
        const string url = "\"./_content/Lumeo.Scheduler/js/scheduler-views.js\"";

        Assert.Contains($"AppendVersion({url})", source, StringComparison.Ordinal);
        Assert.DoesNotContain($"\"import\", {url}", source, StringComparison.Ordinal);
    }

    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Lumeo.slnx")))
            dir = Path.GetDirectoryName(dir);
        Assert.NotNull(dir);
        return dir!;
    }
}
