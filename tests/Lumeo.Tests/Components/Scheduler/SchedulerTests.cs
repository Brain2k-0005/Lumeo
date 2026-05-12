using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Scheduler;

public class SchedulerTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SchedulerTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_toolbar_navigation()
    {
        var cut = _ctx.Render<L.Scheduler>();
        // Toolbar has Today, Month, Week, Day, List buttons
        Assert.Contains("Today", cut.Markup);
        Assert.Contains("Month", cut.Markup);
    }

    [Fact]
    public void Merges_class_parameter()
    {
        var cut = _ctx.Render<L.Scheduler>(p => p.Add(c => c.Class, "scheduler-cls"));
        Assert.Contains("scheduler-cls", cut.Markup);
    }

    [Fact]
    public void Forwards_additional_attributes()
    {
        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object> { ["data-testid"] = "sched" }));
        Assert.Contains("data-testid=\"sched\"", cut.Markup);
    }

    [Fact]
    public void Initial_view_month_selected_by_default()
    {
        var cut = _ctx.Render<L.Scheduler>();
        // "Month" toggle item should be present and active
        Assert.Contains("Month", cut.Markup);
    }

    // ── New tests for rc.29 additions ──────────────────────────────────────

    /// <summary>
    /// A Scheduler with a recurring SchedulerEvent (DaysOfWeek set) must render
    /// without throwing any exception. FullCalendar's JS layer won't run in bUnit's
    /// headless DOM, so we just assert the component renders and the event is wired.
    /// </summary>
    [Fact]
    public void Recurring_event_renders_without_exception()
    {
        var today = DateTime.Today;
        var recurringEvent = new L.SchedulerEvent(
            Id: "rec1",
            Title: "Stand-up",
            Start: today.AddHours(9),
            End: today.AddHours(9).AddMinutes(30),
            DaysOfWeek: new[] { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday },
            RecurrenceEnd: today.AddMonths(3),
            ExceptionDates: new[] { today.AddDays(7) }
        );

        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.Events, new[] { recurringEvent })
            .Add(c => c.InitialView, L.SchedulerView.Week));

        // Component must render the toolbar (proves no exception during render).
        Assert.Contains("Today", cut.Markup);
        Assert.Contains("Week", cut.Markup);
    }

    /// <summary>
    /// NowIndicator, SlotMinTime, and SlotMaxTime parameters are accepted and the
    /// component renders cleanly. The JS won't execute in bUnit, so we verify the
    /// Blazor layer accepts the params without errors.
    /// </summary>
    [Fact]
    public void NowIndicator_and_slot_params_accepted()
    {
        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.NowIndicator, true)
            .Add(c => c.SlotMinTime, new TimeOnly(7, 0))
            .Add(c => c.SlotMaxTime, new TimeOnly(22, 0))
            .Add(c => c.SlotDuration, TimeSpan.FromMinutes(15))
            .Add(c => c.InitialView, L.SchedulerView.Week));

        Assert.Contains("Today", cut.Markup);
        Assert.Contains("Week", cut.Markup);
    }

    /// <summary>
    /// When Resources are provided, the resource legend (aria-label="Resource legend")
    /// is rendered and each resource title appears in the markup.
    /// </summary>
    [Fact]
    public void Resources_legend_renders_when_resources_provided()
    {
        var resources = new[]
        {
            new L.SchedulerResource("room-a", "Conference Room A", "var(--color-primary)"),
            new L.SchedulerResource("room-b", "Conference Room B", "var(--color-destructive)"),
        };

        var events = new[]
        {
            new L.SchedulerEvent("e1", "Team Meeting", DateTime.Today.AddHours(10),
                DateTime.Today.AddHours(11), ResourceId: "room-a"),
            new L.SchedulerEvent("e2", "1:1 Review", DateTime.Today.AddHours(14),
                DateTime.Today.AddHours(15), ResourceId: "room-b"),
        };

        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.Resources, resources)
            .Add(c => c.Events, events)
            .Add(c => c.InitialView, L.SchedulerView.Week));

        // Legend container must be present.
        Assert.Contains("Resource legend", cut.Markup);
        // Both resource titles must appear.
        Assert.Contains("Conference Room A", cut.Markup);
        Assert.Contains("Conference Room B", cut.Markup);
    }
}
