using System.Globalization;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Calendar;

/// <summary>
/// The Culture parameter (#547) drives month/weekday names, digits and the default first day of
/// week independent of CultureInfo.CurrentCulture, and pickers pass their Culture to the grid.
/// </summary>
public class CalendarCultureTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly CultureInfo _prev = CultureInfo.CurrentCulture;
    private static readonly CultureInfo Fa = new("fa-IR");

    public CalendarCultureTests()
    {
        _ctx.AddLumeoServices();
        CultureInfo.CurrentCulture = new CultureInfo("en-US");
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() { CultureInfo.CurrentCulture = _prev; await _ctx.DisposeAsync(); }

    private static readonly DateOnly Day = new(2024, 10, 3); // 12 Mehr 1403

    [Fact]
    public void Calendar_Culture_Fa_Renders_Persian_Month_Weekdays_And_Digits_While_Current_Is_EnUs()
    {
        var cut = _ctx.Render<L.Calendar>(p =>
        {
            p.Add(c => c.CalendarSystem, L.CalendarSystem.Jalali);
            p.Add(c => c.Culture, Fa);
            p.Add(c => c.Value, Day);
        });
        Assert.Equal("مهر ۱۴۰۳", cut.Find("[role=grid]").GetAttribute("aria-label"));
        Assert.Contains(Fa.DateTimeFormat.AbbreviatedDayNames[6], cut.Markup);
        Assert.DoesNotContain(">Sat<", cut.Markup);
        Assert.Contains("۱۲", cut.Markup);
    }

    [Fact]
    public void Calendar_Culture_Drives_Gregorian_Weekday_Names_And_First_Day()
    {
        var de = new CultureInfo("de-DE");
        var cut = _ctx.Render<L.Calendar>(p =>
        {
            p.Add(c => c.Culture, de);
            p.Add(c => c.Value, Day);
        });
        Assert.Contains(de.DateTimeFormat.AbbreviatedDayNames[(int)DayOfWeek.Monday], cut.Markup);
        Assert.DoesNotContain(">Sun<", cut.Markup);
    }

    [Fact]
    public void Calendar_Without_Culture_Uses_Current_Culture()
    {
        var cut = _ctx.Render<L.Calendar>(p => p.Add(c => c.Value, Day));
        Assert.Contains("October 2024", cut.Find("[role=grid]").GetAttribute("aria-label"));
    }

    [Fact]
    public void DatePicker_Passes_Culture_To_Calendar()
    {
        var cut = _ctx.Render<L.DatePicker>(p =>
        {
            p.Add(c => c.CalendarSystem, L.CalendarSystem.Jalali);
            p.Add(c => c.Culture, Fa);
            p.Add(c => c.Value, Day);
        });
        cut.Find("button").Click();
        Assert.Equal("مهر ۱۴۰۳", cut.Find("[role=grid]").GetAttribute("aria-label"));
    }

    [Fact]
    public void DateRangePicker_Passes_Culture_To_Calendar()
    {
        var cut = _ctx.Render<L.DateRangePicker>(p =>
        {
            p.Add(c => c.CalendarSystem, L.CalendarSystem.Jalali);
            p.Add(c => c.Culture, Fa);
            p.Add(c => c.StartDate, Day);
        });
        cut.Find("button").Click();
        Assert.Contains("مهر ۱۴۰۳", string.Join("|", cut.FindAll("[role=grid]").Select(g => g.GetAttribute("aria-label"))));
    }
}
