using Bunit;
using Lumeo.Tests.Helpers;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Scheduler;

/// <summary>
/// ReUI parity — its event calendar lists an "N-day" view alongside month/week/day/agenda
/// (https://reui.io/components/event-calendar). Lumeo had no equivalent: <c>DaysToShow</c>
/// existed only on the agenda, which is a list, not a time grid.
///
/// The distinguishing property is that it does NOT align to a week. "The next three days"
/// has to stay the next three days as you page, and consecutive pages have to tile the
/// calendar rather than overlap — that is what most of these pin.
/// </summary>
public class SchedulerMultiDayViewTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SchedulerMultiDayViewTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // A Wednesday — deliberately mid-week, so week-alignment shows up as a wrong first column.
    private static readonly DateTime Day = new(2026, 3, 11);

    private IRenderedComponent<L.Scheduler> Render(int? visibleDays, L.SchedulerView view = L.SchedulerView.MultiDay) =>
        _ctx.Render<L.Scheduler>(p =>
        {
            p.Add(c => c.InitialView, view);
            p.Add(c => c.InitialDate, Day);
            if (visibleDays is not null) p.Add(c => c.VisibleDays, visibleDays);
        });

    private static string[] DayColumns(IRenderedComponent<L.Scheduler> cut) =>
        cut.FindAll("[data-daycol]").Select(c => c.GetAttribute("data-daycol")!).ToArray();

    [Fact]
    public void It_Renders_Exactly_The_Requested_Number_Of_Days()
    {
        var cut = Render(3);

        Assert.Equal(new[] { "2026-03-11", "2026-03-12", "2026-03-13" }, DayColumns(cut));
    }

    [Fact]
    public void It_Starts_At_The_Anchor_Not_At_The_Week_Start()
    {
        // The regression this exists for: the time grid aligns to a week boundary when it is
        // asked for 7 days. An N-day window must not inherit that — a 3-day view anchored on
        // Wednesday starts on Wednesday.
        var cut = Render(3);

        Assert.Equal("2026-03-11", DayColumns(cut)[0]);
    }

    [Fact]
    public void Seven_Visible_Days_Still_Starts_At_The_Anchor()
    {
        // The awkward case: Days == 7 is exactly the value that triggers week alignment in the
        // grid. Asking for a rolling seven days is not asking for "this week".
        var cut = Render(7);

        var cols = DayColumns(cut);
        Assert.Equal(7, cols.Length);
        Assert.Equal("2026-03-11", cols[0]);
        Assert.Equal("2026-03-17", cols[^1]);
    }

    [Fact]
    public async Task Next_Pages_By_The_Window_Width_So_Pages_Tile()
    {
        var cut = Render(3);

        await cut.InvokeAsync(() => cut.FindAll("button").First(b => b.GetAttribute("aria-label")?.Contains("Next", StringComparison.OrdinalIgnoreCase) == true
                                                                    || b.TextContent.Trim() == "›").Click());

        var cols = DayColumns(cut);
        Assert.Equal("2026-03-14", cols[0]);   // 11+3, no gap and no overlap
        Assert.Equal("2026-03-16", cols[^1]);
    }

    [Fact]
    public void The_Toolbar_Button_Appears_Only_Once_A_Window_Width_Is_Chosen()
    {
        // An N-day view with no N is not a view, so the button is hidden rather than
        // defaulting silently.
        var without = Render(null, L.SchedulerView.Week);
        Assert.DoesNotContain(without.FindAll("button"), b => b.TextContent.Contains("days", StringComparison.OrdinalIgnoreCase));

        var with = Render(3, L.SchedulerView.Week);
        Assert.Contains(with.FindAll("button"), b => b.TextContent.Contains("3 days", StringComparison.Ordinal));
    }

    [Fact]
    public void The_Title_Names_Both_Ends_So_Adjacent_Pages_Differ()
    {
        var cut = Render(3);

        var title = cut.Find(".text-center").TextContent.Trim();

        // Not the month name: List, Resource and MultiDay all opened on it, because the
        // INITIAL title was computed by the wrapper-shaped formatter which has no branch for
        // any of them. It only corrected itself once the user pressed prev or next.
        Assert.Contains("Mar 11", title, StringComparison.Ordinal);
        Assert.Contains("Mar 13", title, StringComparison.Ordinal);
    }

    [Fact]
    public void A_One_Day_Window_Reads_As_A_Day_Not_As_A_Range()
    {
        var cut = Render(1);

        Assert.Single(DayColumns(cut));
        Assert.DoesNotContain("Mar 11 – Mar 11", cut.Markup, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-4, 1)]
    [InlineData(99, 14)]
    public void The_Window_Width_Is_Clamped(int requested, int expected)
    {
        var cut = Render(requested);

        Assert.Equal(expected, DayColumns(cut).Length);
    }

    [Fact]
    public void The_Other_First_Party_Views_Also_Open_On_Their_Own_Title()
    {
        // Same pre-existing bug, wider than this view: the agenda opened on the month name
        // too. Pinned here because MultiDay is what exposed it.
        var agenda = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialView, L.SchedulerView.List)
            .Add(c => c.InitialDate, Day));

        Assert.DoesNotContain("March 2026", agenda.Find(".text-center").TextContent, StringComparison.Ordinal);
    }
}
