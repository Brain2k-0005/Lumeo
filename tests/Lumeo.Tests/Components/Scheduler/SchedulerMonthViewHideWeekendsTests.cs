using System.Globalization;
using AngleSharp.Dom;
using Bunit;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Scheduler;

/// <summary>
/// ReUI parity — its event calendar exposes a weekend-hiding toggle
/// (https://reui.io/components/event-calendar, "Display Controls"). Lumeo's month
/// grid had no way to hide them at all.
///
/// The risk in this feature is not the rendering, it is the KEYBOARD: arrow
/// navigation moved by raw 0-41 cell index (+/-1 for a day, +/-7 for a week), so
/// simply not rendering two columns would leave arrow keys walking onto cells
/// that are not in the DOM. Navigation therefore walks positions in the visible
/// list instead, and most of the tests below pin that rather than the markup.
/// </summary>
public class SchedulerMonthViewHideWeekendsTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SchedulerMonthViewHideWeekendsTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static DateTime D(int y, int m, int d) => new(y, m, d);

    private IRenderedComponent<L.SchedulerMonthView> Render(bool hideWeekends, bool weekNumbers = false) =>
        _ctx.Render<L.SchedulerMonthView>(p => p
            .Add(c => c.AnchorDate, D(2026, 3, 15))
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Monday)
            .Add(c => c.HideWeekends, hideWeekends)
            .Add(c => c.ShowWeekNumbers, weekNumbers));

    private static DateTime DateOf(IElement cell) =>
        DateTime.ParseExact(cell.GetAttribute("data-cell-date")!, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    [Fact]
    public void Off_By_Default_The_Grid_Is_Byte_For_Byte_The_Seven_Column_One()
    {
        var cut = Render(hideWeekends: false);

        Assert.Equal(42, cut.FindAll("[data-cell-date]").Count);
        Assert.Contains("grid-cols-7", cut.Find("[role='grid']").GetAttribute("class"));
    }

    [Fact]
    public void On_Only_Weekdays_Render()
    {
        var cut = Render(hideWeekends: true);

        var cells = cut.FindAll("[data-cell-date]");
        Assert.Equal(30, cells.Count); // 6 rows x 5 weekdays
        Assert.All(cells, c =>
        {
            var dow = DateOf(c).DayOfWeek;
            Assert.NotEqual(DayOfWeek.Saturday, dow);
            Assert.NotEqual(DayOfWeek.Sunday, dow);
        });
        Assert.Contains("grid-cols-5", cut.Find("[role='grid']").GetAttribute("class"));
    }

    [Fact]
    public void The_Weekday_Header_Strip_Drops_The_Same_Two_Columns()
    {
        // A 5-column body under a 7-column header would shear the whole grid.
        var cut = Render(hideWeekends: true);

        // The header strip is the element immediately before the grid; counting
        // its OWN direct children avoids also matching the day-number spans
        // inside the grid, which share the font-medium class.
        var strip = cut.Find("[role='grid']").PreviousElementSibling!;
        var headers = strip.Children.Where(c => c.LocalName == "span").ToList();

        Assert.Equal(5, headers.Count);
    }

    [Fact]
    public async Task ArrowRight_From_Friday_Skips_The_Weekend_And_Lands_On_Monday()
    {
        // The regression this exists for: +1 on the raw index would target
        // Saturday, which is not rendered, and focus would go nowhere.
        var cut = Render(hideWeekends: true);
        var cells = cut.FindAll("[data-cell-date]");
        var friday = cells.First(c => DateOf(c).DayOfWeek == DayOfWeek.Friday);
        var fridayDate = DateOf(friday);

        await cut.InvokeAsync(() => friday.KeyDown(new KeyboardEventArgs { Key = "ArrowRight" }));

        var focused = cut.FindAll("[data-cell-date]").First(c => c.GetAttribute("tabindex") == "0");
        Assert.Equal(DayOfWeek.Monday, DateOf(focused).DayOfWeek);
        Assert.Equal(fridayDate.AddDays(3), DateOf(focused));
    }

    [Fact]
    public async Task ArrowDown_Moves_One_Week_Within_The_Same_Weekday_Column()
    {
        var cut = Render(hideWeekends: true);
        var cells = cut.FindAll("[data-cell-date]");
        var wednesday = cells.First(c => DateOf(c).DayOfWeek == DayOfWeek.Wednesday);
        var start = DateOf(wednesday);

        await cut.InvokeAsync(() => wednesday.KeyDown(new KeyboardEventArgs { Key = "ArrowDown" }));

        var focused = cut.FindAll("[data-cell-date]").First(c => c.GetAttribute("tabindex") == "0");
        // Predicted-wrong value if the step were still the raw +7: the same
        // weekday, but reached through hidden cells — which happens to agree
        // here, so the assertion pins the DATE, not just the weekday.
        Assert.Equal(start.AddDays(7), DateOf(focused));
    }

    [Fact]
    public async Task End_Snaps_To_Friday_Not_Sunday()
    {
        var cut = Render(hideWeekends: true);
        var cells = cut.FindAll("[data-cell-date]");
        var monday = cells.First(c => DateOf(c).DayOfWeek == DayOfWeek.Monday);

        await cut.InvokeAsync(() => monday.KeyDown(new KeyboardEventArgs { Key = "End" }));

        var focused = cut.FindAll("[data-cell-date]").First(c => c.GetAttribute("tabindex") == "0");
        Assert.Equal(DayOfWeek.Friday, DateOf(focused).DayOfWeek);
    }

    [Fact]
    public void A_Sunday_First_Grid_Still_Has_A_Focusable_Cell()
    {
        // Codex review of this PR, P1: the roving-tabindex anchor defaults to
        // index 0, which in a Sunday-first grid is a hidden Sunday. A bounds
        // check alone left every cell at tabindex="-1", dropping the grid out of
        // the tab order and killing arrow navigation outright.
        var cut = _ctx.Render<L.SchedulerMonthView>(p => p
            .Add(c => c.AnchorDate, D(2026, 3, 15))
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.HideWeekends, true));

        var anchor = Assert.Single(cut.FindAll("[data-cell-date]"), c => c.GetAttribute("tabindex") == "0");
        Assert.NotEqual(DayOfWeek.Sunday, DateOf(anchor).DayOfWeek);
        Assert.NotEqual(DayOfWeek.Saturday, DateOf(anchor).DayOfWeek);
    }

    [Fact]
    public void Toggling_Weekends_Off_While_A_Weekend_Cell_Is_Focused_Moves_The_Anchor()
    {
        var cut = _ctx.Render<L.SchedulerMonthView>(p => p
            .Add(c => c.AnchorDate, D(2026, 3, 15))
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.HideWeekends, false));

        cut.Render(p => p
            .Add(c => c.AnchorDate, D(2026, 3, 15))
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.HideWeekends, true));

        Assert.Single(cut.FindAll("[data-cell-date]"), c => c.GetAttribute("tabindex") == "0");
    }

    [Fact]
    public async Task ArrowUp_In_The_Top_Row_Does_Not_Slide_Sideways()
    {
        // Codex review of this PR, P2: clamping the FLATTENED position changes
        // its column, so ArrowUp from the first row's Friday used to land on
        // that row's Monday instead of doing nothing.
        var cut = Render(hideWeekends: true);
        var cells = cut.FindAll("[data-cell-date]");
        var topFriday = cells.Take(5).First(c => DateOf(c).DayOfWeek == DayOfWeek.Friday);
        var before = DateOf(topFriday);

        await cut.InvokeAsync(() => topFriday.KeyDown(new KeyboardEventArgs { Key = "ArrowUp" }));

        var focused = cut.FindAll("[data-cell-date]").First(c => c.GetAttribute("tabindex") == "0");
        Assert.Equal(before, DateOf(focused));
    }

    [Fact]
    public void An_Event_Starting_On_A_Hidden_Saturday_Still_Shows_Its_Title()
    {
        // Codex review of this PR, P2: ShowTitle is set only on a run's FIRST
        // day, so hiding that Saturday removed the one titled segment and every
        // weekday chip rendered a non-breaking space — an unlabelled bar.
        // 2026-03-07 is a Saturday; the event runs into the following Tuesday.
        var ev = new L.SchedulerEvent("e1", "Offsite",
            new DateTime(2026, 3, 7, 9, 0, 0), new DateTime(2026, 3, 10, 17, 0, 0));

        var cut = _ctx.Render<L.SchedulerMonthView>(p => p
            .Add(c => c.AnchorDate, D(2026, 3, 15))
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Monday)
            .Add(c => c.HideWeekends, true)
            .Add(c => c.Events, new[] { ev }));

        // Assert the CHIP's own text, not the markup as a whole: the title also
        // appears in the tooltip and aria-label, so a substring check on the
        // markup passes even when every visible chip renders a blank.
        var chips = cut.FindAll("[data-event-id='e1']");
        Assert.NotEmpty(chips);
        Assert.Contains(chips, c => c.TextContent.Contains("Offsite", StringComparison.Ordinal));
    }

    [Fact]
    public void Week_Numbers_And_Hidden_Weekends_Compose()
    {
        // The grid-template must be a literal Tailwind can see for every
        // combination, not an interpolated string.
        var cut = Render(hideWeekends: true, weekNumbers: true);

        var cls = cut.Find("[role='grid']").GetAttribute("class")!;
        Assert.Contains("repeat(5,minmax(0,1fr))", cls);
        Assert.Equal(6, cut.FindAll("[role='rowheader']").Count);
        Assert.Equal(30, cut.FindAll("[data-cell-date]").Count);
    }

    [Theory]
    [InlineData(false, DayOfWeek.Friday)]
    [InlineData(true, DayOfWeek.Friday)]
    public void The_last_VISIBLE_column_opens_its_overflow_inward(bool hideWeekends, DayOfWeek lastVisible)
    {
        // With weekends hidden on a Monday-first grid, Friday is the last column on SCREEN while
        // its raw index is 4 — so a raw index test left it opening outward, past the edge of a
        // five-column grid whose root clips (Codex review, PR #427).
        var anchor = D(2026, 3, 15);
        var friday = new DateTime(2026, 3, 20);   // a Friday inside the rendered month
        var events = Enumerable.Range(0, 6)
            .Select(i => new L.SchedulerEvent($"f{i}", $"Event {i}", friday.AddHours(9), friday.AddHours(10)))
            .ToArray();

        var cut = _ctx.Render<L.SchedulerMonthView>(p => p
            .Add(c => c.AnchorDate, anchor)
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Monday)
            .Add(c => c.HideWeekends, hideWeekends)
            .Add(c => c.Events, events));

        Assert.Equal(DayOfWeek.Friday, lastVisible);

        var trigger = cut.FindAll("[data-testid='month-more-events']")
                         .Single(t => DateOf(t.Closest("[data-cell-date]")!) == friday.Date);
        trigger.Click();

        var cls = cut.Find("[data-testid='month-more-popover']").GetAttribute("class") ?? string.Empty;
        if (hideWeekends)
        {
            // Last visible column: it has to open back into the grid.
            Assert.Contains("end-0", cls);
        }
        else
        {
            // Friday is column five of seven — two more follow it, so it opens outward as usual.
            Assert.Contains("start-0", cls);
        }
    }

}
