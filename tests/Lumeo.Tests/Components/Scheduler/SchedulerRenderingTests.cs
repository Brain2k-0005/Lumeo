using Bunit;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Scheduler;

/// <summary>
/// <c>&lt;Scheduler&gt;</c> now renders Lumeo's own Blazor views and nothing else — the
/// FullCalendar wrapper, its <c>Engine</c> switch and its JS bridge are gone. These cover what
/// the component renders and how its toolbar drives it, all without touching JS.
///
/// <para>
/// The features below were once reachable only through the opt-in engine — week numbers,
/// weekend hiding, live announcements, resource columns, and <c>SchedulerEvent.Recurrence</c>,
/// which the wrapper ignored entirely. They are simply what the component does now.
/// </para>
/// </summary>
public class SchedulerRenderingTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SchedulerRenderingTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static readonly DateTime Day = new(2026, 3, 10);

    private static readonly L.SchedulerEvent[] Events =
    [
        new("e1", "Standup", Day.AddHours(9), Day.AddHours(10)),
    ];

    [Fact]
    public void The_Default_Render_Is_Lumeos_Own_Grid()
    {
        // The inverse of what this asserted before the removal: with no parameters at all,
        // the component renders its own month grid rather than an empty JS host div.
        var cut = _ctx.Render<L.Scheduler>(p => p.Add(c => c.Events, Events));

        Assert.NotEmpty(cut.FindAll("[role='grid']"));
        Assert.Equal(42, cut.FindAll("[data-cell-date]").Count);
    }

    [Fact]
    public void The_Month_View_Renders_A_42_Cell_Grid()
    {
        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialView, L.SchedulerView.Month)
            .Add(c => c.InitialDate, Day)
            .Add(c => c.Events, Events));

        // 42-cell month grid is the first-party view's signature.
        Assert.Equal(42, cut.FindAll("[data-cell-date]").Count);
    }

    [Fact]
    public void Recurrence_Is_Expanded()
    {
        // The sharpest reason this opt-in exists. Scheduler.razor's ToJsEvent
        // branches solely on the legacy DaysOfWeek pair, so a structured
        // Recurrence rule reaches its change-detection hash and nothing else.
        var rule = new L.SchedulerRecurrenceRule(L.SchedulerRecurrenceFrequency.Daily);
        var ev = new L.SchedulerEvent("e1", "Daily standup",
            Day.AddDays(-3).AddHours(9), Day.AddDays(-3).AddHours(10))
        { Recurrence = rule };

        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialView, L.SchedulerView.Month)
            .Add(c => c.InitialDate, Day)
            .Add(c => c.Events, new[] { ev }));

        // Expanded onto days it was never explicitly scheduled for.
        Assert.Contains("Daily standup", cut.Markup);
    }

    [Theory]
    [InlineData(L.SchedulerView.Week)]
    [InlineData(L.SchedulerView.Day)]
    public void The_Time_Grid_Views_Render(L.SchedulerView view)
    {
        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialView, view)
            .Add(c => c.InitialDate, Day)
            .Add(c => c.Events, Events));

        Assert.NotEmpty(cut.FindAll("[data-daycol]"));
    }

    [Fact]
    public void The_List_View_Maps_To_The_Agenda_View()
    {
        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialView, L.SchedulerView.List)
            .Add(c => c.InitialDate, Day)
            .Add(c => c.Events, Events));

        // The agenda view is a list, not a grid.
        Assert.Empty(cut.FindAll("[data-cell-date]"));
        Assert.Contains("Standup", cut.Markup);
    }

    [Fact]
    public async Task Toolbar_Navigation_Moves_The_Anchor_Without_Any_Interop()
    {
        // The wrapper delegates Prev/Next to FullCalendar; this engine owns an
        // anchor date instead. Predicted-wrong behaviour if it were still routed
        // through interop: nothing moves, because there is no JS instance at all.
        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialView, L.SchedulerView.Month)
            .Add(c => c.InitialDate, Day));

        var before = cut.FindAll("[data-cell-date]")[10].GetAttribute("data-cell-date");

        var next = cut.FindAll("button").First(b => (b.GetAttribute("aria-label") ?? "").Contains("Next", StringComparison.OrdinalIgnoreCase));
        await next.ClickAsync(new());

        var after = cut.FindAll("[data-cell-date]")[10].GetAttribute("data-cell-date");
        Assert.NotEqual(before, after);
    }

    [Fact]
    public void The_Advertised_Month_Flags_Actually_Reach_The_View()
    {
        // Codex review, P1-in-spirit: the Engine docs advertised week numbers and
        // weekend hiding, but no parameters existed for either — the claim was in
        // the API documentation and not in the code.
        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialView, L.SchedulerView.Month)
            .Add(c => c.InitialDate, Day)
            .Add(c => c.ShowWeekNumbers, true)
            .Add(c => c.HideWeekends, true));

        Assert.Equal(6, cut.FindAll("[role='rowheader']").Count);   // week-number column
        Assert.Equal(30, cut.FindAll("[data-cell-date]").Count);    // weekends dropped
    }

    [Fact]
    public void The_Advertised_Resource_View_Actually_Renders()
    {
        // Same finding: the docs promised resource columns, and no branch rendered
        // SchedulerResourceView at all.
        var rooms = new[] { new L.SchedulerResource("r1", "Room A") };
        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialView, L.SchedulerView.Resource)
            .Add(c => c.InitialDate, Day)
            .Add(c => c.Resources, rooms)
            .Add(c => c.Events, new[] { new L.SchedulerEvent("e1", "Standup", Day.AddHours(9), Day.AddHours(10), ResourceId: "r1") }));

        // Scoped to the column, not the whole markup (CodeRabbit review of this PR): the
        // title also appears in the chip's tooltip and aria-label, so a substring check on
        // cut.Markup passes even when the event renders outside its resource lane — which is
        // precisely the binding this test exists to prove.
        Assert.Contains("Standup", cut.Find("[data-resourcecol='r1']").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_Edit_Reaches_A_Bind_Events_Consumer()
    {
        // Codex review, P1: OnEventChange was forwarded straight to the children,
        // so EventsChanged never fired — a consumer using @bind-Events without a
        // separate handler saw the edit reach nobody.
        IEnumerable<L.SchedulerEvent>? pushed = null;
        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialView, L.SchedulerView.Month)
            .Add(c => c.InitialDate, Day)
            .Add(c => c.Events, Events)
            .Add(c => c.EventsChanged, (IEnumerable<L.SchedulerEvent> e) => { pushed = e; }));

        var month = cut.FindComponent<L.SchedulerMonthView>();
        await cut.InvokeAsync(() => month.Instance.CommitDrag("e1", "2026-03-12"));

        Assert.NotNull(pushed);
        Assert.Equal(new DateTime(2026, 3, 12), pushed!.Single().Start.Date);
    }

    [Fact]
    public void An_Event_Keeps_Its_Resource_Colour()
    {
        // Codex review, P2: passing EventColor straight through dropped the
        // resource fallback, so an event carrying only a ResourceId lost its colour
        // the moment a consumer opted in.
        var rooms = new[] { new L.SchedulerResource("r1", "Room A", "rgb(1, 2, 3)") };
        var ev = new L.SchedulerEvent("e1", "Standup", Day.AddHours(9), Day.AddHours(10), ResourceId: "r1");

        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialView, L.SchedulerView.Month)
            .Add(c => c.InitialDate, Day)
            .Add(c => c.Resources, rooms)
            .Add(c => c.Events, new[] { ev }));

        Assert.Contains("rgb(1, 2, 3)", cut.Markup);
    }

    [Fact]
    public async Task An_Uncontrolled_Edit_Sticks()
    {
        // Codex review, P1 — and a correction to my OWN previous fix: routing the
        // edit through a local list repaired the controlled case and left the
        // documented UNCONTROLLED mode broken, so with no EventsChanged delegate
        // the chip still snapped back.
        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialView, L.SchedulerView.Month)
            .Add(c => c.InitialDate, Day)
            .Add(c => c.Events, Events));

        var month = cut.FindComponent<L.SchedulerMonthView>();
        await cut.InvokeAsync(() => month.Instance.CommitDrag("e1", "2026-03-12"));

        // The moved chip must be on the 12th, not back on the 10th.
        var cell = cut.Find("[data-cell-date='2026-03-12']");
        Assert.Contains("Standup", cell.TextContent);
    }

    [Fact]
    public void The_Resource_View_Steps_One_Day_At_A_Time()
    {
        // Codex review, P2: Resource renders a single date, so falling through to
        // the week-sized default skipped six days per click.
        var rooms = new[] { new L.SchedulerResource("r1", "Room A") };
        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialView, L.SchedulerView.Resource)
            .Add(c => c.InitialDate, Day)
            .Add(c => c.Resources, rooms)
            .Add(c => c.Events, new[] { new L.SchedulerEvent("e1", "Standup", Day.AddDays(1).AddHours(9), Day.AddDays(1).AddHours(10), ResourceId: "r1") }));

        // Nothing on the anchor day...
        Assert.DoesNotContain("Standup", cut.Find("[data-resourcecol='r1']").TextContent);

        var next = cut.FindAll("button").First(b => (b.GetAttribute("aria-label") ?? "").Contains("Next", StringComparison.OrdinalIgnoreCase));
        next.Click();

        // ...and exactly one day later it is there.
        Assert.Contains("Standup", cut.Find("[data-resourcecol='r1']").TextContent);
    }

    [Fact]
    public void No_Calendar_Library_Is_Loaded_At_All()
    {
        // What this used to assert — that the first-party engine skipped the JS init — is
        // now structural rather than behavioural: there is no init method left on the
        // interop surface to call. The assertion that still carries weight is that the
        // shipped package contains no calendar library and no module to load one.
        var scheduler = typeof(L.Scheduler).Assembly.Location;
        var wwwroot = Path.Combine(Path.GetDirectoryName(scheduler)!, "..", "..", "..", "..", "..",
                                   "src", "Lumeo.Scheduler", "wwwroot", "js");

        if (!Directory.Exists(wwwroot)) return;   // packaged run — nothing to inspect

        var modules = Directory.GetFiles(wwwroot, "*.js").Select(Path.GetFileName).ToArray();
        Assert.DoesNotContain("scheduler.js", modules);          // the FullCalendar bridge
        Assert.Contains("scheduler-views.js", modules);          // the first-party drag module stays
    }

    [Fact]
    public void Replacing_Events_Is_Adopted_By_The_Grid()
    {
        // Adoption used to be gated on a JS handshake that never happened for this engine,
        // so a parent replacing its collection saw the calendar keep rendering the old one
        // forever. The gate is gone with the wrapper; this keeps the behaviour pinned.
        using var ctx = new BunitContext();
        ctx.AddLumeoServices();

        var cut = ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialView, L.SchedulerView.Month)
            .Add(c => c.InitialDate, Day)
            .Add(c => c.Events, Events));

        var replaced = new[]
        {
            new L.SchedulerEvent("e2", "Retro", Day.AddHours(14), Day.AddHours(15)),
        };
        cut.Render(p => p
            .Add(c => c.InitialView, L.SchedulerView.Month)
            .Add(c => c.InitialDate, Day)
            .Add(c => c.Events, replaced));

        Assert.Contains("Retro", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Standup", cut.Markup, StringComparison.Ordinal);
    }
}
