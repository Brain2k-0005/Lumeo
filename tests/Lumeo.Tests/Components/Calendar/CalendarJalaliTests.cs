using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Calendar;

/// <summary>
/// CalendarSystem="Jalali" (issue #545): the grid, headers and navigation follow the
/// Persian calendar while the bound values stay Gregorian DateOnly.
/// 2024-10-03 is 12 Mehr 1403; Mehr 1403 runs 2024-09-22 (a Sunday) .. 2024-10-21.
/// </summary>
public class CalendarJalaliTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly CultureInfo _prevCulture = CultureInfo.CurrentCulture;
    private readonly CultureInfo _prevUiCulture = CultureInfo.CurrentUICulture;

    public CalendarJalaliTests()
    {
        _ctx.AddLumeoServices();
        CultureInfo.CurrentCulture = new CultureInfo("en-US");
        CultureInfo.CurrentUICulture = new CultureInfo("en-US");
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        CultureInfo.CurrentCulture = _prevCulture;
        CultureInfo.CurrentUICulture = _prevUiCulture;
        await _ctx.DisposeAsync();
    }

    private IRenderedComponent<L.Calendar> RenderJalali(DateOnly? value = null, Action<DateOnly?>? onChanged = null)
        => _ctx.Render<L.Calendar>(p =>
        {
            p.Add(c => c.CalendarSystem, L.CalendarSystem.Jalali);
            if (value.HasValue) p.Add(c => c.Value, value.Value);
            if (onChanged is not null)
                p.Add(c => c.ValueChanged, EventCallback.Factory.Create<DateOnly?>(this, onChanged));
        });

    [Fact]
    public void Header_Shows_Jalali_Month_And_Year()
    {
        var cut = RenderJalali(new DateOnly(2024, 10, 3));
        Assert.Contains("Mehr 1403", cut.Markup);
        Assert.Equal("Mehr 1403", cut.Find("[role=grid]").GetAttribute("aria-label"));
    }

    [Fact]
    public void Week_Starts_On_Saturday_By_Default()
    {
        var cut = RenderJalali(new DateOnly(2024, 10, 3));
        var headers = cut.FindAll("[role=columnheader]");
        Assert.Equal("Sa", headers[0].TextContent.Trim());
        Assert.Equal("Fr", headers[6].TextContent.Trim());
    }

    [Fact]
    public void Explicit_FirstDayOfWeek_Still_Wins()
    {
        var cut = _ctx.Render<L.Calendar>(p => p
            .Add(c => c.CalendarSystem, L.CalendarSystem.Jalali)
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Monday)
            .Add(c => c.Value, new DateOnly(2024, 10, 3)));
        Assert.Equal("Mo", cut.FindAll("[role=columnheader]")[0].TextContent.Trim());
    }

    [Fact]
    public void Grid_Lays_Out_The_Jalali_Month()
    {
        var cut = RenderJalali(new DateOnly(2024, 10, 3));
        var days = cut.FindAll("[role=gridcell] button");

        // Saturday 2024-09-21 (31 Shahrivar) leads, Sunday 2024-09-22 is 1 Mehr.
        Assert.Equal("31", days[0].TextContent.Trim());
        Assert.Equal("1", days[1].TextContent.Trim());
        Assert.Equal("Sunday, 1 Mehr 1403", days[1].GetAttribute("aria-label"));

        // 12 Mehr is the selected value.
        var selected = cut.Find("[role=gridcell][aria-selected=true] button");
        Assert.Equal("12", selected.TextContent.Trim());
        Assert.Equal("Thursday, 12 Mehr 1403", selected.GetAttribute("aria-label"));
    }

    [Fact]
    public void Clicking_A_Day_Emits_The_Gregorian_Date()
    {
        DateOnly? picked = null;
        var cut = RenderJalali(new DateOnly(2024, 10, 3), v => picked = v);

        cut.Find("button[aria-label='Sunday, 1 Mehr 1403']").Click();

        Assert.Equal(new DateOnly(2024, 9, 22), picked);
    }

    [Fact]
    public void Next_And_Previous_Navigate_Jalali_Months()
    {
        var cut = RenderJalali(new DateOnly(2024, 10, 3));

        cut.Find("button[aria-label='Next month']").Click();
        Assert.Equal("Aban 1403", cut.Find("[role=grid]").GetAttribute("aria-label"));

        cut.Find("button[aria-label='Previous month']").Click();
        cut.Find("button[aria-label='Previous month']").Click();
        Assert.Equal("Shahrivar 1403", cut.Find("[role=grid]").GetAttribute("aria-label"));
    }

    [Fact]
    public void Next_Month_Steps_Past_A_31_Day_Jalali_Month()
    {
        // 2024-04-20 is 1 Ordibehesht 1403 (31 days). Gregorian month addition would stall here.
        var cut = RenderJalali(new DateOnly(2024, 4, 20));
        Assert.Equal("Ordibehesht 1403", cut.Find("[role=grid]").GetAttribute("aria-label"));

        cut.Find("button[aria-label='Next month']").Click();
        Assert.Equal("Khordad 1403", cut.Find("[role=grid]").GetAttribute("aria-label"));
    }

    [Fact]
    public void MinDate_Disables_Previous_At_The_Jalali_Month_Boundary()
    {
        var cut = _ctx.Render<L.Calendar>(p => p
            .Add(c => c.CalendarSystem, L.CalendarSystem.Jalali)
            .Add(c => c.Value, new DateOnly(2024, 10, 3))
            // 1 Mehr 1403 — still inside the displayed Jalali month although it is September.
            .Add(c => c.MinDate, new DateOnly(2024, 9, 22)));

        var prev = cut.Find("button[aria-label='Previous month']");
        Assert.True(prev.HasAttribute("disabled"));
    }

    [Fact]
    public void Persian_Culture_Uses_Persian_Names_And_Digits()
    {
        CultureInfo.CurrentCulture = new CultureInfo("fa-IR");
        var cut = RenderJalali(new DateOnly(2024, 10, 3));

        Assert.Equal("مهر ۱۴۰۳", cut.Find("[role=grid]").GetAttribute("aria-label"));
        Assert.Equal("۱۲", cut.Find("[role=gridcell][aria-selected=true] button").TextContent.Trim());
    }

    [Fact]
    public void Switching_CalendarSystem_Re_Anchors_The_Displayed_Month()
    {
        var cut = _ctx.Render<L.Calendar>(p => p.Add(c => c.Value, new DateOnly(2024, 10, 3)));
        Assert.Equal("October 2024", cut.Find("[role=grid]").GetAttribute("aria-label"));

        cut.Render(p => p.Add(c => c.CalendarSystem, L.CalendarSystem.Jalali));

        // 1 Oct 2024 is 10 Mehr 1403 → the grid now shows Mehr, starting on 1 Mehr.
        Assert.Equal("Mehr 1403", cut.Find("[role=grid]").GetAttribute("aria-label"));
        Assert.Equal("Sunday, 1 Mehr 1403", cut.FindAll("[role=gridcell] button")[1].GetAttribute("aria-label"));
    }

    [Fact]
    public void Week_Numbers_Use_Persian_Digits_Under_Fa()
    {
        CultureInfo.CurrentCulture = new CultureInfo("fa-IR");
        var cut = _ctx.Render<L.Calendar>(p => p
            .Add(c => c.CalendarSystem, L.CalendarSystem.Jalali)
            .Add(c => c.ShowWeekNumbers, true)
            .Add(c => c.Value, new DateOnly(2024, 10, 3)));
        Assert.All(cut.FindAll("[role=rowheader]"), h => Assert.DoesNotMatch("[0-9]", h.TextContent));
    }

    [Fact]
    public void Gregorian_Default_Is_Unchanged()
    {
        var cut = _ctx.Render<L.Calendar>(p => p.Add(c => c.Value, new DateOnly(2024, 10, 3)));
        Assert.Equal("October 2024", cut.Find("[role=grid]").GetAttribute("aria-label"));
        Assert.Equal("Su", cut.FindAll("[role=columnheader]")[0].TextContent.Trim());
    }
}

public class CalendarSystemMathTests
{
    private static readonly CultureInfo En = new("en-US");
    private static readonly CultureInfo Fa = new("fa-IR");

    [Fact]
    public void Converts_Gregorian_To_Jalali_Fields()
    {
        var d = new DateOnly(2024, 10, 3);
        Assert.Equal(1403, CalendarSystemMath.GetYear(L.CalendarSystem.Jalali, d));
        Assert.Equal(7, CalendarSystemMath.GetMonth(L.CalendarSystem.Jalali, d));
        Assert.Equal(12, CalendarSystemMath.GetDay(L.CalendarSystem.Jalali, d));
        Assert.Equal(new DateOnly(2024, 9, 22), CalendarSystemMath.MonthStart(L.CalendarSystem.Jalali, d));
        Assert.Equal(new DateOnly(2024, 10, 21), CalendarSystemMath.MonthEnd(L.CalendarSystem.Jalali, d));
    }

    [Fact]
    public void AddMonths_Clamps_The_Day_Of_Month()
    {
        // 31 Shahrivar 1403 + 1 month → 30 Mehr 1403 (Mehr has 30 days).
        var lastShahrivar = new DateOnly(2024, 9, 21);
        Assert.Equal(new DateOnly(2024, 10, 21), CalendarSystemMath.AddMonths(L.CalendarSystem.Jalali, lastShahrivar, 1));
    }

    [Theory]
    [InlineData("yyyy/MM/dd", "1403/07/12")]
    [InlineData("d MMMM yyyy", "12 Mehr 1403")]
    [InlineData("dd-MMM-yy", "12-Meh-03")]
    [InlineData("'Day' d", "Day 12")]
    public void Formats_With_Pattern(string pattern, string expected)
        => Assert.Equal(expected, CalendarSystemMath.Format(L.CalendarSystem.Jalali, new DateOnly(2024, 10, 3), pattern, En));

    [Fact]
    public void Formats_Persian_Digits_Under_Fa_Culture()
        => Assert.Equal("۱۴۰۳/۰۷/۱۲", CalendarSystemMath.Format(L.CalendarSystem.Jalali, new DateOnly(2024, 10, 3), "yyyy/MM/dd", Fa));

    [Theory]
    [InlineData("1403/07/12")]
    [InlineData("1403-7-12")]
    [InlineData("۱۴۰۳/۰۷/۱۲")]
    [InlineData("١٤٠٣/٠٧/١٢")]
    public void Parses_Ascii_Persian_And_Arabic_Digits(string text)
    {
        Assert.True(CalendarSystemMath.TryParseJalali(text, "yyyy/MM/dd", out var result));
        Assert.Equal(new DateOnly(2024, 10, 3), result);
    }

    [Fact]
    public void Parse_Follows_Pattern_Order_And_Month_Names()
    {
        Assert.True(CalendarSystemMath.TryParseJalali("12/07/1403", "dd/MM/yyyy", out var a));
        Assert.Equal(new DateOnly(2024, 10, 3), a);

        Assert.True(CalendarSystemMath.TryParseJalali("12 Mehr 1403", "d MMMM yyyy", out var b));
        Assert.Equal(new DateOnly(2024, 10, 3), b);

        // "دی" is a substring of "اردیبهشت" — the longer name must win.
        Assert.True(CalendarSystemMath.TryParseJalali("۱ اردیبهشت ۱۴۰۳", "d MMMM yyyy", out var c));
        Assert.Equal(2, CalendarSystemMath.GetMonth(L.CalendarSystem.Jalali, c));
    }

    [Theory]
    [InlineData("1404/12/30")] // 1404 is not a leap year: Esfand has 29 days
    [InlineData("1403/13/01")]
    [InlineData("1403/07")]
    [InlineData("hello")]
    public void Rejects_Invalid_Dates(string text)
        => Assert.False(CalendarSystemMath.TryParseJalali(text, "yyyy/MM/dd", out _));

    [Theory]
    [InlineData("yy/MM/dd", 2020, 6, 1)]   // 1399/03/12 — before 1400
    [InlineData("yy/MM/dd", 2024, 10, 3)]  // 1403/07/12
    [InlineData("yyyyMMdd", 2024, 10, 3)]  // no separators
    [InlineData("ddMMyyyy", 2020, 6, 1)]
    [InlineData("yyMMdd", 2024, 10, 3)]
    public void Format_Parse_Round_Trips(string pattern, int y, int m, int d)
    {
        var date = new DateOnly(y, m, d);
        var text = CalendarSystemMath.Format(L.CalendarSystem.Jalali, date, pattern, En);
        Assert.True(CalendarSystemMath.TryParseJalali(text, pattern, out var parsed), text);
        Assert.Equal(date, parsed);
    }

    [Theory]
    [InlineData("D", "Thursday, 12 Mehr 1403")]
    [InlineData("d", "1403/07/12")]
    [InlineData("Y", "Mehr 1403")]
    public void Expands_Standard_Formats(string pattern, string expected)
        => Assert.Equal(expected, CalendarSystemMath.Format(L.CalendarSystem.Jalali, new DateOnly(2024, 10, 3), pattern, En));

    [Fact]
    public void Quoted_Literals_With_Digits_Round_Trip()
    {
        var date = new DateOnly(2024, 10, 3);
        var text = CalendarSystemMath.Format(L.CalendarSystem.Jalali, date, "'v2' yyyy/MM/dd", En);
        Assert.Equal("v2 1403/07/12", text);
        Assert.True(CalendarSystemMath.TryParseJalali(text, "'v2' yyyy/MM/dd", out var parsed));
        Assert.Equal(date, parsed);
    }

    [Fact]
    public void Four_Digit_Years_Are_Not_Expanded()
    {
        Assert.True(CalendarSystemMath.TryParseJalali("0001/01/01", "yyyy/MM/dd", out var y1));
        Assert.Equal(1, CalendarSystemMath.GetYear(L.CalendarSystem.Jalali, y1));
        Assert.False(CalendarSystemMath.TryParseJalali("0000/01/01", "yyyy/MM/dd", out _));

        Assert.True(CalendarSystemMath.TryParseJalaliMonth("0001/07", out var m));
        Assert.Equal(1, CalendarSystemMath.GetYear(L.CalendarSystem.Jalali, m));
        Assert.Equal(7, CalendarSystemMath.GetMonth(L.CalendarSystem.Jalali, m));
    }

    [Fact]
    public void Accepts_Leap_Day()
        => Assert.True(CalendarSystemMath.TryParseJalali("1403/12/30", "yyyy/MM/dd", out _));

    [Fact]
    public void Parses_Months_And_Years()
    {
        Assert.True(CalendarSystemMath.TryParseJalaliMonth("Mehr 1403", out var m));
        Assert.Equal(new DateOnly(2024, 9, 22), m);
        Assert.True(CalendarSystemMath.TryParseJalaliMonth("1403/07", out var m2));
        Assert.Equal(m, m2);
        Assert.True(CalendarSystemMath.TryParseJalaliYear("۱۴۰۳", out var y));
        Assert.Equal(new DateOnly(2024, 3, 20), y);
    }
}
