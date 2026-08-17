using System.Globalization;
using Bunit;
using Lumeo.Tests.Helpers;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Scheduler;

/// <summary>
/// The resource timeline: rows are resources, the time axis runs HORIZONTALLY across days,
/// weeks or months.
///
/// <para>
/// A third layout, not a variant of the two that existed. <c>SchedulerResourceView</c> lays
/// out one day with a column per resource and a vertical clock — "who is where today". This
/// one answers "how is this resource booked over the coming stretch", which is the shape
/// Outlook's scheduling assistant and FullCalendar's paid resource timeline have.
/// </para>
///
/// <para>
/// Most of what follows pins geometry, because that is where this view can be quietly wrong:
/// a bar whose left edge is off by a fraction of a column still looks plausible.
/// </para>
/// </summary>
public class SchedulerTimelineViewTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SchedulerTimelineViewTests() => _ctx.AddLumeoServices();
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
        L.SchedulerTimelineUnit unit = L.SchedulerTimelineUnit.Day,
        int columns = 7,
        DateTime? today = null) =>
        _ctx.Render<L.SchedulerTimelineView>(p =>
        {
            p.Add(c => c.Resources, Rooms);
            p.Add(c => c.RangeStart, Start);
            p.Add(c => c.Columns, columns);
            p.Add(c => c.Unit, unit);
            p.Add(c => c.ColumnWidth, ColW);
            p.Add(c => c.Today, today ?? Start.AddDays(2));
            if (events is not null) p.Add(c => c.Events, events);
        });

    private static double Px(string style, string prop)
    {
        var i = style.IndexOf(prop + ":", StringComparison.Ordinal);
        Assert.True(i >= 0, $"'{prop}' not found in style '{style}'");
        var rest = style[(i + prop.Length + 1)..].TrimStart();
        var end = rest.IndexOf("px", StringComparison.Ordinal);
        return double.Parse(rest[..end], CultureInfo.InvariantCulture);
    }

    // ── Structure ────────────────────────────────────────────────────────────

    [Fact]
    public void One_row_per_resource()
    {
        var cut = Render();

        Assert.Equal(2, cut.FindAll("[data-timeline-row]").Count);
        Assert.Equal(2, cut.FindAll("[data-timeline-resource]").Count);
    }

    [Fact]
    public void The_axis_has_one_column_per_unit()
    {
        var cut = Render(columns: 10);

        Assert.Equal(10, cut.FindAll("[data-timeline-column]").Count);
    }

    [Fact]
    public void The_column_count_is_clamped()
    {
        Assert.Single(Render(columns: 0).FindAll("[data-timeline-column]"));
        Assert.Equal(366, Render(columns: 5000).FindAll("[data-timeline-column]").Count);
    }

    // ── Geometry ─────────────────────────────────────────────────────────────

    [Fact]
    public void A_booking_starts_at_its_own_column_offset()
    {
        // Day 3 of the axis, so exactly two columns in.
        var ev = new L.SchedulerEvent("e1", "Booking", Start.AddDays(2), Start.AddDays(3), ResourceId: "alice");

        var bar = Render(new[] { ev }).Find("[data-timeline-bar='e1']");

        Assert.Equal(2 * ColW, Px(bar.GetAttribute("style")!, "left"));
        Assert.Equal(ColW, Px(bar.GetAttribute("style")!, "width"));
    }

    [Fact]
    public void A_part_day_booking_lands_inside_its_column()
    {
        // 09:00–12:00 on the second day: a quarter of the way in, an eighth wide.
        var ev = new L.SchedulerEvent("e1", "Booking",
            Start.AddDays(1).AddHours(6), Start.AddDays(1).AddHours(12), ResourceId: "alice");

        var bar = Render(new[] { ev }).Find("[data-timeline-bar='e1']");
        var style = bar.GetAttribute("style")!;

        Assert.Equal(ColW + ColW * 0.25, Px(style, "left"), 1);
        Assert.Equal(ColW * 0.25, Px(style, "width"), 1);
    }

    [Fact]
    public void A_booking_running_in_from_before_the_axis_is_clamped_not_dropped()
    {
        // Starts three days before the window and ends inside it. Dropping it would show the
        // resource as free while it is in fact busy — the one thing this view must not do.
        var ev = new L.SchedulerEvent("e1", "Long stay",
            Start.AddDays(-3), Start.AddDays(1), ResourceId: "alice");

        var bar = Render(new[] { ev }).Find("[data-timeline-bar='e1']");
        var style = bar.GetAttribute("style")!;

        Assert.Equal(0, Px(style, "left"));
        Assert.Equal(ColW, Px(style, "width"));
    }

    [Fact]
    public void A_booking_running_past_the_axis_is_clamped_to_its_end()
    {
        var ev = new L.SchedulerEvent("e1", "Long stay",
            Start.AddDays(6), Start.AddDays(20), ResourceId: "alice");

        var bar = Render(new[] { ev }, columns: 7).Find("[data-timeline-bar='e1']");

        Assert.Equal(ColW, Px(bar.GetAttribute("style")!, "width"));
    }

    [Fact]
    public void A_zero_length_booking_stays_clickable()
    {
        // Width 0 would render an invisible, unhittable target.
        var ev = new L.SchedulerEvent("e1", "Marker", Start.AddDays(1), Start.AddDays(1), ResourceId: "alice");

        var bar = Render(new[] { ev }).Find("[data-timeline-bar='e1']");

        Assert.True(Px(bar.GetAttribute("style")!, "width") >= 6);
    }

    // ── Conflicts ────────────────────────────────────────────────────────────

    [Fact]
    public void Overlapping_bookings_get_their_own_lane()
    {
        // Surfacing a double booking is the point of the view; stacking them would hide it.
        var a = new L.SchedulerEvent("a", "A", Start.AddHours(9), Start.AddHours(17), ResourceId: "alice");
        var b = new L.SchedulerEvent("b", "B", Start.AddHours(12), Start.AddHours(20), ResourceId: "alice");

        var cut = Render(new[] { a, b });
        var topA = Px(cut.Find("[data-timeline-bar='a']").GetAttribute("style")!, "top");
        var topB = Px(cut.Find("[data-timeline-bar='b']").GetAttribute("style")!, "top");

        Assert.NotEqual(topA, topB);
    }

    [Fact]
    public void Sequential_bookings_reuse_the_same_lane()
    {
        var a = new L.SchedulerEvent("a", "A", Start.AddHours(9), Start.AddHours(12), ResourceId: "alice");
        var b = new L.SchedulerEvent("b", "B", Start.AddHours(13), Start.AddHours(16), ResourceId: "alice");

        var cut = Render(new[] { a, b });

        Assert.Equal(Px(cut.Find("[data-timeline-bar='a']").GetAttribute("style")!, "top"),
                     Px(cut.Find("[data-timeline-bar='b']").GetAttribute("style")!, "top"));
    }

    [Fact]
    public void A_row_grows_to_fit_its_lanes()
    {
        var a = new L.SchedulerEvent("a", "A", Start.AddHours(9), Start.AddHours(17), ResourceId: "bob");
        var b = new L.SchedulerEvent("b", "B", Start.AddHours(10), Start.AddHours(18), ResourceId: "bob");

        var cut = Render(new[] { a, b });
        var rows = cut.FindAll("[data-timeline-row]");
        var single = Px(rows.First(r => r.GetAttribute("data-timeline-row") == "alice").GetAttribute("style")!, "height");
        var doubled = Px(rows.First(r => r.GetAttribute("data-timeline-row") == "bob").GetAttribute("style")!, "height");

        Assert.True(doubled > single, $"A two-lane row ({doubled}) must be taller than a one-lane row ({single}).");
    }

    [Fact]
    public void The_gutter_row_matches_the_timeline_row_height()
    {
        // The two halves are separate elements; if their heights drift the names stop lining
        // up with the bars, which is worse than useless.
        var a = new L.SchedulerEvent("a", "A", Start.AddHours(9), Start.AddHours(17), ResourceId: "bob");
        var b = new L.SchedulerEvent("b", "B", Start.AddHours(10), Start.AddHours(18), ResourceId: "bob");

        var cut = Render(new[] { a, b });

        foreach (var id in new[] { "alice", "bob" })
        {
            var gutter = cut.Find($"[data-timeline-resource='{id}']").GetAttribute("style")!;
            var row = cut.Find($"[data-timeline-row='{id}']").GetAttribute("style")!;
            Assert.Equal(Px(gutter, "height"), Px(row, "height"));
        }
    }

    // ── Filtering ────────────────────────────────────────────────────────────

    [Fact]
    public void An_event_without_a_matching_resource_is_not_rendered()
    {
        var orphan = new L.SchedulerEvent("x", "Orphan", Start, Start.AddDays(1), ResourceId: "nobody");
        var none = new L.SchedulerEvent("y", "No resource", Start, Start.AddDays(1));

        var cut = Render(new[] { orphan, none });

        Assert.Empty(cut.FindAll("[data-timeline-bar]"));
    }

    // ── Today marker ─────────────────────────────────────────────────────────

    [Fact]
    public void Today_is_marked_when_it_falls_on_the_axis()
    {
        var cut = Render(today: Start.AddDays(3));

        var marker = cut.Find("[data-timeline-today]");
        Assert.Equal(3 * ColW, Px(marker.GetAttribute("style")!, "left"));
    }

    [Fact]
    public void Today_is_absent_when_the_axis_is_elsewhere()
    {
        var cut = Render(today: Start.AddYears(1));

        Assert.Empty(cut.FindAll("[data-timeline-today]"));
    }

    // ── Units ────────────────────────────────────────────────────────────────

    [Fact]
    public void A_week_axis_snaps_its_origin_to_the_week_start()
    {
        // Starting mid-week without snapping would offset every bar by the remainder.
        var cut = _ctx.Render<L.SchedulerTimelineView>(p => p
            .Add(c => c.Resources, Rooms)
            .Add(c => c.RangeStart, new DateTime(2026, 3, 11))   // a Wednesday
            .Add(c => c.Unit, L.SchedulerTimelineUnit.Week)
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Monday)
            .Add(c => c.Columns, 3));

        Assert.Equal("2026-03-09", cut.FindAll("[data-timeline-column]")[0].GetAttribute("data-timeline-column"));
    }

    [Fact]
    public void A_month_axis_uses_the_real_length_of_each_month()
    {
        // A flat 30-day assumption drifts by a day inside February. Feb 15th 2026 is
        // 14/28 = exactly half way through its column.
        var left = L.SchedulerTimelineScale.DateToPixel(
            L.SchedulerTimelineUnit.Month, new DateTime(2026, 2, 1), new DateTime(2026, 2, 15), 100);

        Assert.Equal(50, left, 1);
    }

    // ── Interaction ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Clicking_a_bar_reports_its_event()
    {
        L.SchedulerEvent? clicked = null;
        var ev = new L.SchedulerEvent("e1", "Booking", Start, Start.AddDays(1), ResourceId: "alice");

        var cut = _ctx.Render<L.SchedulerTimelineView>(p => p
            .Add(c => c.Resources, Rooms)
            .Add(c => c.RangeStart, Start)
            .Add(c => c.Events, new[] { ev })
            .Add(c => c.OnEventClick, (L.SchedulerEvent e) => clicked = e));

        await cut.InvokeAsync(() => cut.Find("[data-timeline-bar='e1']").Click());

        Assert.Same(ev, clicked);
    }

    // ── Reachable from <Scheduler> ───────────────────────────────────────────

    private IRenderedComponent<L.Scheduler> Wrapper(
        L.SchedulerTimelineUnit unit = L.SchedulerTimelineUnit.Day,
        int? columns = null,
        bool withResources = true) =>
        _ctx.Render<L.Scheduler>(p =>
        {
            p.Add(c => c.InitialView, L.SchedulerView.Timeline);
            p.Add(c => c.InitialDate, Start);
            p.Add(c => c.TimelineUnit, unit);
            if (columns is not null) p.Add(c => c.TimelineColumns, columns);
            if (withResources) p.Add(c => c.Resources, Rooms);
        });

    [Fact]
    public void The_wrapper_renders_the_timeline()
    {
        var cut = Wrapper(columns: 5);

        Assert.Equal(2, cut.FindAll("[data-timeline-row]").Count);
        Assert.Equal(5, cut.FindAll("[data-timeline-column]").Count);
    }

    [Fact]
    public void Without_resources_it_falls_back_instead_of_rendering_an_empty_frame()
    {
        // A timeline with no rows is a header and nothing else — worse than showing the day.
        var cut = Wrapper(withResources: false);

        Assert.Empty(cut.FindAll("[data-timeline-row]"));
        Assert.NotEmpty(cut.FindAll("[data-daycol]"));
    }

    [Fact]
    public void The_toolbar_offers_it_only_where_it_can_render()
    {
        var with = Wrapper();
        Assert.Contains(with.FindAll("button"), b => b.TextContent.Trim() == "Timeline");

        var without = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialView, L.SchedulerView.Month)
            .Add(c => c.InitialDate, Start));
        Assert.DoesNotContain(without.FindAll("button"), b => b.TextContent.Trim() == "Timeline");
    }

    [Fact]
    public void Next_pages_by_the_whole_axis_in_its_own_unit()
    {
        // A month-per-column axis stepping by days would barely move.
        var cut = Wrapper(unit: L.SchedulerTimelineUnit.Month, columns: 3);
        var firstBefore = cut.FindAll("[data-timeline-column]")[0].GetAttribute("data-timeline-column");

        cut.FindAll("button").First(b => b.GetAttribute("aria-label")?.Contains("Next", StringComparison.OrdinalIgnoreCase) == true).Click();

        var firstAfter = cut.FindAll("[data-timeline-column]")[0].GetAttribute("data-timeline-column");
        Assert.Equal("2026-03-01", firstBefore);
        Assert.Equal("2026-06-01", firstAfter);   // three months on, tiling not overlapping
    }

    [Fact]
    public void The_title_names_both_ends_of_the_axis()
    {
        var cut = Wrapper(columns: 5);

        var title = cut.Find(".text-center").TextContent.Trim();
        Assert.Contains("Mar 9", title, StringComparison.Ordinal);
        Assert.Contains("Mar 13", title, StringComparison.Ordinal);
    }
}
