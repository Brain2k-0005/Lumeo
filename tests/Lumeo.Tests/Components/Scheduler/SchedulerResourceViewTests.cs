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
    public void Slot_Labels_Name_The_Resource_As_Well_As_The_Time()
    {
        // Several columns share one time axis, so a bare hour identifies nothing.
        var cut = _ctx.Render<L.SchedulerResourceView>(p => p
            .Add(c => c.Date, Day)
            .Add(c => c.Resources, Rooms));

        var labels = cut.FindAll("[role='gridcell']").Select(c => c.GetAttribute("aria-label") ?? "").ToList();
        Assert.NotEmpty(labels);
        Assert.Contains(labels, l => l.Contains("Room A", StringComparison.Ordinal));
        Assert.Contains(labels, l => l.Contains("Room B", StringComparison.Ordinal));
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
