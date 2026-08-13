using Bunit;
using Lumeo.Tests.Helpers;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Scheduler;

/// <summary>
/// ReUI parity — its event calendar renders "one column per bookable resource",
/// activated by supplying resources. Lumeo already had <c>SchedulerResource</c> and
/// <c>SchedulerEvent.ResourceId</c>, but they only drove colour-coding and a legend: no view had
/// ever grouped BY resource, so a room-booking or per-person schedule could not be expressed at
/// all.
///
/// This first version is READ-ONLY by design — no drag, resize or create. Those live in the
/// time grid's pointer engine, which keys every gesture off the day under the cursor; wiring it
/// to resource columns is a separate piece of work, and shipping a half-wired version would be
/// worse than shipping none.
/// </summary>
public class SchedulerResourceViewTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SchedulerResourceViewTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static readonly DateTime Day = new(2026, 3, 10);

    private static readonly IReadOnlyList<L.SchedulerResource> Rooms = new[]
    {
        new L.SchedulerResource("r1", "Room A", "var(--color-primary)"),
        new L.SchedulerResource("r2", "Room B", "var(--color-destructive)"),
    };

    private static L.SchedulerEvent Booking(string id, string title, string? resourceId, int startHour, int endHour) =>
        new(id, title, Day.AddHours(startHour), Day.AddHours(endHour), ResourceId: resourceId);

    [Fact]
    public void One_Column_Per_Resource_In_The_Given_Order()
    {
        var cut = _ctx.Render<L.SchedulerResourceView>(p => p
            .Add(c => c.Date, Day)
            .Add(c => c.Resources, Rooms));

        var columns = cut.FindAll("[data-resourcecol]");
        Assert.Equal(2, columns.Count);
        Assert.Equal("r1", columns[0].GetAttribute("data-resourcecol"));
        Assert.Equal("r2", columns[1].GetAttribute("data-resourcecol"));
    }

    [Fact]
    public void An_Event_Lands_In_Its_Own_Resource_Column()
    {
        var cut = _ctx.Render<L.SchedulerResourceView>(p => p
            .Add(c => c.Date, Day)
            .Add(c => c.Resources, Rooms)
            .Add(c => c.Events, new[]
            {
                Booking("e1", "Standup", "r1", 9, 10),
                Booking("e2", "Review", "r2", 9, 11),
            }));

        var first = cut.Find("[data-resourcecol='r1']");
        var second = cut.Find("[data-resourcecol='r2']");

        Assert.Contains("Standup", first.TextContent);
        Assert.DoesNotContain("Review", first.TextContent);
        Assert.Contains("Review", second.TextContent);
        Assert.DoesNotContain("Standup", second.TextContent);
    }

    [Fact]
    public void Overlaps_Are_Packed_Within_A_Column_Not_Across_Resources()
    {
        // Two rooms booked at the same time is not a conflict — it is the whole
        // point of the view. Only overlaps inside ONE column may be narrowed.
        var cut = _ctx.Render<L.SchedulerResourceView>(p => p
            .Add(c => c.Date, Day)
            .Add(c => c.Resources, Rooms)
            .Add(c => c.Events, new[]
            {
                Booking("e1", "A one", "r1", 9, 11),
                Booking("e2", "B one", "r2", 9, 11),
            }));

        // Each is alone in its column, so each keeps the full width.
        foreach (var id in new[] { "e1", "e2" })
        {
            var style = cut.Find($"[data-event-id='{id}']").GetAttribute("style")!;
            Assert.Contains("width:calc(100% - 4px)", style);
        }
    }

    [Fact]
    public void Two_Bookings_On_One_Resource_Share_Its_Column()
    {
        var cut = _ctx.Render<L.SchedulerResourceView>(p => p
            .Add(c => c.Date, Day)
            .Add(c => c.Resources, Rooms)
            .Add(c => c.Events, new[]
            {
                Booking("e1", "First", "r1", 9, 11),
                Booking("e2", "Second", "r1", 10, 12),
            }));

        // Predicted-wrong value if packing were skipped: both at 100% width,
        // stacked on top of each other and unreadable.
        var style = cut.Find("[data-event-id='e1']").GetAttribute("style")!;
        Assert.Contains("width:calc(50% - 4px)", style);
    }

    [Fact]
    public void An_Event_With_No_Matching_Resource_Is_Not_Rendered()
    {
        // Deliberate: folding it into the first column would misattribute a
        // booking, and inventing an "unassigned" column is a layout decision this
        // view has no basis to make. Documented on the Resources parameter.
        var cut = _ctx.Render<L.SchedulerResourceView>(p => p
            .Add(c => c.Date, Day)
            .Add(c => c.Resources, Rooms)
            .Add(c => c.Events, new[] { Booking("e9", "Orphan", "nope", 9, 10) }));

        Assert.Empty(cut.FindAll("[data-event-id='e9']"));
    }

    [Fact]
    public void An_Event_On_Another_Day_Is_Not_Rendered()
    {
        var cut = _ctx.Render<L.SchedulerResourceView>(p => p
            .Add(c => c.Date, Day)
            .Add(c => c.Resources, Rooms)
            .Add(c => c.Events, new[]
            {
                new L.SchedulerEvent("e1", "Yesterday", Day.AddDays(-1).AddHours(9), Day.AddDays(-1).AddHours(10), ResourceId: "r1"),
            }));

        Assert.Empty(cut.FindAll("[data-event-id='e1']"));
    }

    [Fact]
    public void A_Recurring_Booking_Is_Expanded_Onto_This_Day()
    {
        // Uses the same expander the month and time grids do, so a recurring
        // booking appears here exactly as it does there.
        var rule = new L.SchedulerRecurrenceRule(L.SchedulerRecurrenceFrequency.Daily);
        var ev = new L.SchedulerEvent("e1", "Daily standup",
            Day.AddDays(-3).AddHours(9), Day.AddDays(-3).AddHours(10), ResourceId: "r1")
        { Recurrence = rule };

        var cut = _ctx.Render<L.SchedulerResourceView>(p => p
            .Add(c => c.Date, Day)
            .Add(c => c.Resources, Rooms)
            .Add(c => c.Events, new[] { ev }));

        Assert.Contains("Daily standup", cut.Find("[data-resourcecol='r1']").TextContent);
    }

    [Fact]
    public async Task Clicking_A_Chip_Reports_The_Event()
    {
        L.SchedulerEvent? clicked = null;
        var cut = _ctx.Render<L.SchedulerResourceView>(p => p
            .Add(c => c.Date, Day)
            .Add(c => c.Resources, Rooms)
            .Add(c => c.Events, new[] { Booking("e1", "Standup", "r1", 9, 10) })
            .Add(c => c.OnEventClick, (L.SchedulerEvent e) => { clicked = e; }));

        await cut.Find("[data-event-id='e1']").ClickAsync(new());

        Assert.NotNull(clicked);
        Assert.Equal("e1", clicked!.Id);
    }

    [Fact]
    public void Each_Resource_Column_Is_A_Labelled_List_Not_A_Grid()
    {
        // Codex review, P2: the DOM is column-oriented (one element per resource,
        // hours stacked inside) while the VISUAL grid reads hours-by-resource, so
        // role="grid"/"row"/"gridcell" reported the two axes swapped. A grid role
        // also promises 2-D arrow navigation this read-only view does not implement.
        var cut = _ctx.Render<L.SchedulerResourceView>(p => p
            .Add(c => c.Date, Day)
            .Add(c => c.Resources, Rooms));

        Assert.Empty(cut.FindAll("[role='grid']"));
        Assert.Empty(cut.FindAll("[role='gridcell']"));

        var labels = cut.FindAll("[role='list']").Select(c => c.GetAttribute("aria-label") ?? "").ToList();
        Assert.Contains(labels, l => l.Contains("Room A", StringComparison.Ordinal));
        Assert.Contains(labels, l => l.Contains("Room B", StringComparison.Ordinal));
    }

    [Fact]
    public void An_All_Day_Booking_Appears_In_Its_Own_Strip()
    {
        // Codex review, P2: all-day events were filtered out entirely, so a room
        // booked for a whole day simply vanished.
        var ev = new L.SchedulerEvent("e1", "Offsite", Day, Day.AddDays(1), AllDay: true, ResourceId: "r1");

        var cut = _ctx.Render<L.SchedulerResourceView>(p => p
            .Add(c => c.Date, Day).Add(c => c.Resources, Rooms).Add(c => c.Events, new[] { ev }));

        var chip = cut.Find("[data-event-id='e1']");
        Assert.Equal("true", chip.GetAttribute("data-allday"));
        Assert.Contains("Offsite", chip.TextContent);
    }

    [Fact]
    public void An_Overnight_Booking_Is_Clipped_To_The_Day_Being_Viewed()
    {
        // Codex review, P2: the expander returns a 22:00-02:00 booking whole for
        // either day it touches, so the SECOND day drew it from 22:00 again.
        var ev = new L.SchedulerEvent("e1", "Night shift",
            Day.AddDays(-1).AddHours(22), Day.AddHours(2), ResourceId: "r1");

        var cut = _ctx.Render<L.SchedulerResourceView>(p => p
            .Add(c => c.Date, Day).Add(c => c.Resources, Rooms).Add(c => c.Events, new[] { ev }));

        // Clipped to midnight, so it starts at the very top of the axis.
        var style = cut.Find("[data-event-id='e1']").GetAttribute("style")!;
        Assert.Contains("top:0px", style);
    }

    [Fact]
    public void A_Booking_Outside_The_Visible_Hours_Is_Not_Rendered()
    {
        // Codex review, P2: clamping drew a sliver at the top of the axis,
        // implying a reservation that is not in view at all.
        var cut = _ctx.Render<L.SchedulerResourceView>(p => p
            .Add(c => c.Date, Day).Add(c => c.Resources, Rooms)
            .Add(c => c.SlotMinTime, new TimeOnly(8, 0))
            .Add(c => c.Events, new[] { Booking("e1", "Early", "r1", 6, 7) }));

        Assert.Empty(cut.FindAll("[data-event-id='e1']"));
    }

    [Fact]
    public void Two_Events_Sharing_An_Id_And_Start_Do_Not_Crash_The_Packer()
    {
        // Codex review, P2: identical instance keys threw out of ToDictionary.
        var a = Booking("dup", "One", "r1", 9, 10);
        var b = Booking("dup", "Two", "r1", 9, 10);

        var ex = Record.Exception(() => _ctx.Render<L.SchedulerResourceView>(p => p
            .Add(c => c.Date, Day).Add(c => c.Resources, Rooms).Add(c => c.Events, new[] { a, b })));

        Assert.Null(ex);
    }

    [Fact]
    public void A_Late_SlotMinTime_Does_Not_Throw_On_The_Hour_Axis()
    {
        // Codex review, P2: 23:30 pushed the minimum-span rule past midnight and
        // the resulting hour 24 threw out of DateTime's constructor.
        var ex = Record.Exception(() => _ctx.Render<L.SchedulerResourceView>(p => p
            .Add(c => c.Date, Day).Add(c => c.Resources, Rooms)
            .Add(c => c.SlotMinTime, new TimeOnly(23, 30))));

        Assert.Null(ex);
    }

    [Fact]
    public void A_Fractional_SlotMinTime_Keeps_Chips_Aligned_With_Their_Hour_Rows()
    {
        // Codex review, P2: hour rows started at 08:00 while chips were positioned
        // from 08:30, so a 09:00 booking sat half an hour off its own label.
        var cut = _ctx.Render<L.SchedulerResourceView>(p => p
            .Add(c => c.Date, Day).Add(c => c.Resources, Rooms)
            .Add(c => c.SlotMinTime, new TimeOnly(8, 30))
            .Add(c => c.Events, new[] { Booking("e1", "Standup", "r1", 9, 10) }));

        // One hour past the 08:00 row anchor = 48px, not 24px.
        var style = cut.Find("[data-event-id='e1']").GetAttribute("style")!;
        Assert.Contains("top:48px", style);
    }

    [Fact]
    public void A_Booking_Announces_Its_Full_Span()
    {
        // Chip height is the only visual duration cue, which a screen reader
        // cannot see (Codex review, P2).
        var cut = _ctx.Render<L.SchedulerResourceView>(p => p
            .Add(c => c.Date, Day).Add(c => c.Resources, Rooms)
            .Add(c => c.Events, new[] { Booking("e1", "Standup", "r1", 9, 11) }));

        var label = cut.Find("[data-event-id='e1']").GetAttribute("aria-label")!;
        Assert.Contains(Day.AddHours(9).ToString("t"), label);
        Assert.Contains(Day.AddHours(11).ToString("t"), label);
    }

    [Fact]
    public void Columns_Keep_A_Usable_Minimum_Width()
    {
        // Codex review, P2: 1fr tracks compressed dozens of resources into the
        // viewport until headings were a few pixels wide.
        var many = Enumerable.Range(0, 20)
            .Select(i => new L.SchedulerResource($"r{i}", $"Room {i}"))
            .ToList();

        var cut = _ctx.Render<L.SchedulerResourceView>(p => p
            .Add(c => c.Date, Day).Add(c => c.Resources, many));

        var style = cut.Find("[data-resourcecol='r0']").ParentElement!.GetAttribute("style")!;
        Assert.Contains("minmax(8rem, 1fr)", style);
    }

    [Fact]
    public void No_Resources_Renders_An_Empty_Grid_Rather_Than_Throwing()
    {
        var ex = Record.Exception(() => _ctx.Render<L.SchedulerResourceView>(p => p
            .Add(c => c.Date, Day)
            .Add(c => c.Events, new[] { Booking("e1", "Standup", "r1", 9, 10) })));

        Assert.Null(ex);
    }
}
