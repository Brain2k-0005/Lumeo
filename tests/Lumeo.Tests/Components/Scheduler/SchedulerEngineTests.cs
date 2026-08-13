using Bunit;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Scheduler;

/// <summary>
/// Closes the last ReUI-comparison gap that was reachable without a product decision: the
/// first-party views existed, were tested, and were shown on the docs page — but the shipped
/// <c>&lt;Scheduler&gt;</c> could not render them, so a consumer using the public component had no
/// way to get week numbers, weekend hiding, live announcements, resource columns, or
/// <c>SchedulerEvent.Recurrence</c> (which the FullCalendar wrapper ignores entirely).
///
/// <para>
/// Deliberately an OPT-IN, not a switch of the default. Flipping the default would change what
/// every existing consumer renders on an upgrade — that is the owner's call, not a cleanup. The
/// first test below is the one that matters most: with <c>Engine</c> unset, nothing changes.
/// </para>
/// </summary>
public class SchedulerEngineTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SchedulerEngineTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static readonly DateTime Day = new(2026, 3, 10);

    private static readonly L.SchedulerEvent[] Events =
    [
        new("e1", "Standup", Day.AddHours(9), Day.AddHours(10)),
    ];

    [Fact]
    public void The_Default_Engine_Is_Still_The_FullCalendar_Wrapper()
    {
        // The whole point of the opt-in: an existing consumer that never sets
        // Engine must render exactly what it rendered before — a JS host element,
        // and none of the first-party view markup.
        var cut = _ctx.Render<L.Scheduler>(p => p.Add(c => c.Events, Events));

        Assert.Empty(cut.FindAll("[role='grid']"));            // month/time-grid views expose one
        Assert.Empty(cut.FindAll("[data-testid='scheduler-live-region']"));
    }

    [Fact]
    public void The_First_Party_Engine_Renders_Lumeos_Own_Month_View()
    {
        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.Engine, L.SchedulerEngine.FirstParty)
            .Add(c => c.InitialView, L.SchedulerView.Month)
            .Add(c => c.InitialDate, Day)
            .Add(c => c.Events, Events));

        // 42-cell month grid is the first-party view's signature.
        Assert.Equal(42, cut.FindAll("[data-cell-date]").Count);
    }

    [Fact]
    public void The_First_Party_Engine_Honours_Recurrence_Which_The_Wrapper_Ignores()
    {
        // The sharpest reason this opt-in exists. Scheduler.razor's ToJsEvent
        // branches solely on the legacy DaysOfWeek pair, so a structured
        // Recurrence rule reaches its change-detection hash and nothing else.
        var rule = new L.SchedulerRecurrenceRule(L.SchedulerRecurrenceFrequency.Daily);
        var ev = new L.SchedulerEvent("e1", "Daily standup",
            Day.AddDays(-3).AddHours(9), Day.AddDays(-3).AddHours(10))
        { Recurrence = rule };

        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.Engine, L.SchedulerEngine.FirstParty)
            .Add(c => c.InitialView, L.SchedulerView.Month)
            .Add(c => c.InitialDate, Day)
            .Add(c => c.Events, new[] { ev }));

        // Expanded onto days it was never explicitly scheduled for.
        Assert.Contains("Daily standup", cut.Markup);
    }

    [Theory]
    [InlineData(L.SchedulerView.Week)]
    [InlineData(L.SchedulerView.Day)]
    public void The_Time_Grid_Views_Map_Onto_The_First_Party_Engine(L.SchedulerView view)
    {
        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.Engine, L.SchedulerEngine.FirstParty)
            .Add(c => c.InitialView, view)
            .Add(c => c.InitialDate, Day)
            .Add(c => c.Events, Events));

        Assert.NotEmpty(cut.FindAll("[data-daycol]"));
    }

    [Fact]
    public void The_List_View_Maps_To_The_Agenda_View()
    {
        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.Engine, L.SchedulerEngine.FirstParty)
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
            .Add(c => c.Engine, L.SchedulerEngine.FirstParty)
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
            .Add(c => c.Engine, L.SchedulerEngine.FirstParty)
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
            .Add(c => c.Engine, L.SchedulerEngine.FirstParty)
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
    public async Task A_First_Party_Edit_Reaches_A_Bind_Events_Consumer()
    {
        // Codex review, P1: OnEventChange was forwarded straight to the children,
        // so EventsChanged never fired — a consumer using @bind-Events without a
        // separate handler saw the edit reach nobody.
        IEnumerable<L.SchedulerEvent>? pushed = null;
        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.Engine, L.SchedulerEngine.FirstParty)
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
    public void An_Event_Keeps_Its_Resource_Colour_After_Switching_Engines()
    {
        // Codex review, P2: passing EventColor straight through dropped the
        // resource fallback, so an event carrying only a ResourceId lost its colour
        // the moment a consumer opted in.
        var rooms = new[] { new L.SchedulerResource("r1", "Room A", "rgb(1, 2, 3)") };
        var ev = new L.SchedulerEvent("e1", "Standup", Day.AddHours(9), Day.AddHours(10), ResourceId: "r1");

        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.Engine, L.SchedulerEngine.FirstParty)
            .Add(c => c.InitialView, L.SchedulerView.Month)
            .Add(c => c.InitialDate, Day)
            .Add(c => c.Resources, rooms)
            .Add(c => c.Events, new[] { ev }));

        Assert.Contains("rgb(1, 2, 3)", cut.Markup);
    }

    [Fact]
    public async Task An_Uncontrolled_First_Party_Edit_Sticks()
    {
        // Codex review, P1 — and a correction to my OWN previous fix: routing the
        // edit through a local list repaired the controlled case and left the
        // documented UNCONTROLLED mode broken, so with no EventsChanged delegate
        // the chip still snapped back.
        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.Engine, L.SchedulerEngine.FirstParty)
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
            .Add(c => c.Engine, L.SchedulerEngine.FirstParty)
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
    public void The_First_Party_Engine_Creates_No_JS_Instance()
    {
        // What makes this a genuinely dependency-free path rather than the same
        // wrapper with different markup: no init call, so no calendar library.
        var interop = new TrackingInteropService();
        using var ctx = new BunitContext();
        ctx.AddLumeoServices();
        ctx.Services.AddSingleton<Lumeo.Services.IComponentInteropService>(interop);

        ctx.Render<L.Scheduler>(p => p
            .Add(c => c.Engine, L.SchedulerEngine.FirstParty)
            .Add(c => c.InitialDate, Day)
            .Add(c => c.Events, Events));

        Assert.Equal(0, interop.SchedulerInitCallCount);
    }

    [Fact]
    public void Replacing_Events_In_First_Party_Mode_Touches_No_JS_At_All()
    {
        // Codex review of this PR, P2. Adoption had to be opened to the first-party engine
        // (otherwise a parent replacing its collection was never picked up), and that also
        // let it reach an interop call guarded only by a null-forgiving `!` — untrue here,
        // because this engine never creates a FullCalendar instance. The JS side no-ops on
        // an unknown id, but getting there imports scheduler.js, which can throw wherever
        // JS is unavailable at all and take the pure-Blazor update down with it.
        var interop = new TrackingInteropService();
        using var ctx = new BunitContext();
        ctx.AddLumeoServices();
        ctx.Services.AddSingleton<Lumeo.Services.IComponentInteropService>(interop);

        var cut = ctx.Render<L.Scheduler>(p => p
            .Add(c => c.Engine, L.SchedulerEngine.FirstParty)
            .Add(c => c.InitialView, L.SchedulerView.Month)
            .Add(c => c.InitialDate, Day)
            .Add(c => c.Events, Events));

        var replaced = new[]
        {
            new L.SchedulerEvent("e2", "Retro", Day.AddHours(14), Day.AddHours(15)),
        };
        cut.Render(p => p
            .Add(c => c.Engine, L.SchedulerEngine.FirstParty)
            .Add(c => c.InitialView, L.SchedulerView.Month)
            .Add(c => c.InitialDate, Day)
            .Add(c => c.Events, replaced));

        // The replacement is adopted...
        Assert.Contains("Retro", cut.Markup, StringComparison.Ordinal);
        // ...without ever reaching the JS bridge.
        Assert.Empty(interop.SchedulerSetEventsIds);
    }
}
