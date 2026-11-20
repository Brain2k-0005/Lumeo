using System.Globalization;
using Bunit;
using Lumeo.GanttV3;
using Lumeo.Tests.Helpers;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.GanttV3;

/// <summary>
/// CalendarSystem.Jalali support of the Gantt time scale (#547). The scale math is tested
/// directly on <see cref="GanttScale"/> / <see cref="GanttCalendar"/>; a couple of render tests
/// check the header text of the real component. Gregorian output must stay unchanged.
/// </summary>
public class GanttJalaliTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public GanttJalaliTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private const CalendarSystem J = CalendarSystem.Jalali;
    private static readonly CultureInfo Fa = CultureInfo.GetCultureInfo("fa-IR");
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");
    private static DateTime D(int y, int m, int d) => new(y, m, d);

    // 1 Farvardin 1403 = 2024-03-20 (1403 is a leap year), 1 Farvardin 1404 = 2025-03-21
    private static readonly DateTime Nowruz1403 = D(2024, 3, 20);

    // ── Unit boundaries ─────────────────────────────────────────────────────

    [Fact]
    public void Month_Units_Start_On_The_First_Of_Each_Jalali_Month()
    {
        var units = GanttScale.BuildDateUnits(GanttViewMode.Month, Nowruz1403, D(2024, 8, 1), J);
        Assert.Equal(
            [D(2024, 3, 20), D(2024, 4, 20), D(2024, 5, 21), D(2024, 6, 21), D(2024, 7, 22)],
            units.Take(5));
        Assert.All(units, u => Assert.Equal(1, GanttCalendar.Day(J, u)));
    }

    [Fact]
    public void Quarter_Units_Start_On_Jalali_Months_1_4_7_10()
    {
        var units = GanttScale.BuildDateUnits(GanttViewMode.Quarter, Nowruz1403, D(2025, 3, 20), J);
        Assert.Equal([D(2024, 3, 20), D(2024, 6, 21), D(2024, 9, 22), D(2024, 12, 21)], units);
        Assert.Equal(
            D(2024, 9, 22),
            GanttScale.AlignToUnitStart(GanttViewMode.Quarter, D(2024, 11, 5), J)); // 15 Aban 1403 -> 1 Mehr
    }

    [Fact]
    public void Year_Units_Start_On_One_Farvardin()
    {
        var units = GanttScale.BuildDateUnits(GanttViewMode.Year, Nowruz1403, D(2026, 6, 1), J);
        Assert.Equal([D(2024, 3, 20), D(2025, 3, 21), D(2026, 3, 21)], units);
        Assert.Equal(D(2025, 3, 21), GanttScale.AlignToUnitStart(GanttViewMode.Year, D(2025, 12, 31), J));
    }

    [Fact]
    public void AlignToUnitStart_Month_Snaps_To_Jalali_Month_Start()
    {
        // 2024-10-15 = 24 Mehr 1403 -> 1 Mehr 1403 = 2024-09-22
        Assert.Equal(D(2024, 9, 22), GanttScale.AlignToUnitStart(GanttViewMode.Month, D(2024, 10, 15), J));
        // Gregorian is untouched
        Assert.Equal(D(2024, 10, 1), GanttScale.AlignToUnitStart(GanttViewMode.Month, D(2024, 10, 15)));
    }

    // ── Column day counts ───────────────────────────────────────────────────

    [Theory]
    [InlineData(1403, 1, 31)]
    [InlineData(1403, 6, 31)]
    [InlineData(1403, 7, 30)]
    [InlineData(1403, 12, 30)] // leap year
    [InlineData(1402, 12, 29)]
    public void DaysInMonth_Follows_The_Real_Jalali_Month_Length(int year, int month, int expected)
    {
        var start = DateOnly.FromDateTime(PersianToGregorian(year, month, 1));
        Assert.Equal(expected, GanttCalendar.DaysInMonth(J, start.ToDateTime(TimeOnly.MinValue)));
    }

    [Fact]
    public void DateToPixel_Month_Scales_Within_The_Real_Month_Length()
    {
        // Esfand 1402 has 29 days: its last day ends exactly at the right edge of the column,
        // where the Gregorian "/30" approximation would stop short of it.
        var esfand = D(2024, 2, 20); // 1 Esfand 1402
        var lastDay = D(2024, 3, 19); // 29 Esfand 1402
        Assert.Equal(120d * 28 / 29, GanttScale.DateToPixel(GanttViewMode.Month, esfand, lastDay, 120, J), 6);
        Assert.Equal(120d, GanttScale.DateToPixel(GanttViewMode.Month, esfand, lastDay.AddDays(1), 120, J), 6);
        // Half way through the 31-day Farvardin (day 16 start = 15 days in) is 15/31 of the column.
        Assert.Equal(120d * 15 / 31, GanttScale.DateToPixel(GanttViewMode.Month, Nowruz1403, Nowruz1403.AddDays(15), 120, J), 6);
        // Month unit start of the following month is exactly one column.
        Assert.Equal(120d, GanttScale.DateToPixel(GanttViewMode.Month, Nowruz1403, D(2024, 4, 20), 120, J), 6);
    }

    [Fact]
    public void DateToPixel_Year_Uses_The_Real_Year_Length()
    {
        // 1403 is a leap year (366 days): 2025-03-21 is exactly one year column from 2024-03-20.
        Assert.Equal(100d, GanttScale.DateToPixel(GanttViewMode.Year, Nowruz1403, D(2025, 3, 21), 100, J), 6);
        Assert.Equal(50d, GanttScale.DateToPixel(GanttViewMode.Year, Nowruz1403, Nowruz1403.AddDays(183), 100, J), 6);
    }

    [Fact]
    public void PixelToDate_And_Continuous_Are_Inverse_On_Jalali_Month_Boundaries()
    {
        Assert.Equal(D(2024, 5, 21), GanttScale.PixelToDate(GanttViewMode.Month, Nowruz1403, 240, 120, J));
        Assert.Equal(D(2024, 4, 20), GanttScale.PixelToDateContinuous(GanttViewMode.Month, Nowruz1403, 120, 120, J));
        // 15/31 into Farvardin
        var mid = GanttScale.PixelToDateContinuous(GanttViewMode.Month, Nowruz1403, 120d * 15 / 31, 120, J);
        Assert.True(Math.Abs((mid - Nowruz1403.AddDays(15)).TotalSeconds) < 1, mid.ToString("O"));
        Assert.Equal(D(2025, 3, 21), GanttScale.PixelToDate(GanttViewMode.Year, Nowruz1403, 100, 100, J));
    }

    [Fact]
    public void Gregorian_Scale_Math_Is_Unchanged()
    {
        var o = D(2026, 1, 1);
        Assert.Equal(GanttScale.DateToPixel(GanttViewMode.Month, o, D(2026, 3, 16), 120),
            GanttScale.DateToPixel(GanttViewMode.Month, o, D(2026, 3, 16), 120, CalendarSystem.Gregorian));
        Assert.Equal(2 * 120 + 15 / 30.0 * 120, GanttScale.DateToPixel(GanttViewMode.Month, o, D(2026, 3, 16), 120), 6);
        Assert.Equal(
            [D(2026, 1, 1), D(2026, 2, 1), D(2026, 3, 1)],
            GanttScale.BuildDateUnits(GanttViewMode.Month, o, D(2026, 3, 5)));
        Assert.Equal(D(2026, 4, 1), GanttScale.AlignToUnitStart(GanttViewMode.Quarter, D(2026, 5, 9)));
    }

    // ── Weeks ───────────────────────────────────────────────────────────────

    [Fact]
    public void Week_Mode_Aligns_To_Saturday_By_Default_And_To_An_Explicit_FirstDayOfWeek()
    {
        var wed = D(2024, 10, 16); // a Wednesday
        Assert.Equal(DayOfWeek.Wednesday, wed.DayOfWeek);
        Assert.Equal(D(2024, 10, 12), GanttScale.AlignToUnitStart(GanttViewMode.Week, wed, J, DayOfWeek.Saturday));
        Assert.Equal(DayOfWeek.Saturday, GanttScale.AlignToUnitStart(GanttViewMode.Week, wed, J, DayOfWeek.Saturday).DayOfWeek);
        Assert.Equal(D(2024, 10, 14), GanttScale.AlignToUnitStart(GanttViewMode.Week, wed, J, DayOfWeek.Monday));
        // Gregorian Week mode keeps its (unaligned) behaviour.
        Assert.Equal(wed, GanttScale.AlignToUnitStart(GanttViewMode.Week, wed));
    }

    [Fact]
    public void JalaliWeekOfYear_Week_One_Contains_One_Farvardin()
    {
        // 1 Farvardin 1403 = Wednesday 2024-03-20; the Saturday-start week containing it begins 2024-03-16.
        Assert.Equal(DayOfWeek.Wednesday, Nowruz1403.DayOfWeek);
        Assert.Equal(1, GanttCalendar.JalaliWeekOfYear(D(2024, 3, 16), DayOfWeek.Saturday));
        Assert.Equal(2, GanttCalendar.JalaliWeekOfYear(D(2024, 3, 23), DayOfWeek.Saturday));
        Assert.Equal(52, GanttCalendar.JalaliWeekOfYear(D(2025, 3, 8), DayOfWeek.Saturday));
        // The band that straddles 29 Esfand 1403 / 1 Farvardin 1404 (2025-03-21, a Friday) is week 1 of 1404.
        Assert.Equal(DayOfWeek.Friday, D(2025, 3, 21).DayOfWeek);
        Assert.Equal(1, GanttCalendar.JalaliWeekOfYear(D(2025, 3, 15), DayOfWeek.Saturday));
        // With a Monday week start the same date is in band 2025-03-17..23.
        Assert.Equal(1, GanttCalendar.JalaliWeekOfYear(D(2025, 3, 17), DayOfWeek.Monday));
    }

    // ── Labels ──────────────────────────────────────────────────────────────

    [Fact]
    public void Month_Mode_Labels_Fa_Use_Persian_Names_And_Digits()
    {
        var units = GanttScale.BuildDateUnits(GanttViewMode.Month, Nowruz1403, D(2024, 6, 25), J);
        var lower = GanttScale.LowerLabels(GanttViewMode.Month, units, J, Fa);
        Assert.Equal(["فروردین", "اردیبهشت", "خرداد", "تیر"], lower);
        var upper = GanttScale.UpperRuns(GanttViewMode.Month, units, null, J, Fa);
        Assert.Single(upper);
        Assert.Equal("۱۴۰۳", upper[0].Label);
        Assert.Equal(4, upper[0].Span);
    }

    [Fact]
    public void Month_Mode_Labels_En_Use_Latin_Transliteration()
    {
        var units = GanttScale.BuildDateUnits(GanttViewMode.Month, Nowruz1403, D(2024, 6, 25), J);
        Assert.Equal(["Far", "Ord", "Kho", "Tir"], GanttScale.LowerLabels(GanttViewMode.Month, units, J, En));
        Assert.Equal("1403", GanttScale.UpperRuns(GanttViewMode.Month, units, null, J, En)[0].Label);
    }

    [Fact]
    public void Day_Mode_Labels_Use_Jalali_Day_Numbers_And_Saturday_Week_Bands()
    {
        // Saturday 2024-03-16 = 26 Esfand 1402 (Esfand 1402 has 29 days); the band runs through
        // Friday 22 Mar = 3 Farvardin 1403.
        var units = GanttScale.BuildDateUnits(GanttViewMode.Day, D(2024, 3, 16), D(2024, 3, 22), J);
        Assert.Equal(["۲۶", "۲۷", "۲۸", "۲۹", "۰۱", "۰۲", "۰۳"], GanttScale.LowerLabels(GanttViewMode.Day, units, J, Fa));
        var upper = GanttScale.UpperRuns(GanttViewMode.Day, units, null, J, En);
        Assert.Single(upper); // one Saturday-start band
        Assert.Equal("W1 Esf 26 – Far 3", upper[0].Label);
        Assert.Equal("هفته ۱ اسفند ۲۶ – فروردین ۳", GanttScale.UpperRuns(GanttViewMode.Day, units, null, J, Fa)[0].Label);
    }

    [Fact]
    public void Week_Mode_Lower_Labels_Are_Jalali_Day_Slash_Month()
    {
        var units = GanttScale.BuildDateUnits(GanttViewMode.Week, D(2024, 3, 16), D(2024, 3, 29), J);
        Assert.Equal(["26/12", "4/1"], GanttScale.LowerLabels(GanttViewMode.Week, units, J, En));
        Assert.Equal(["۲۶/۱۲", "۴/۱"], GanttScale.LowerLabels(GanttViewMode.Week, units, J, Fa));
    }

    [Fact]
    public void Quarter_And_Year_Labels_Jalali()
    {
        var q = GanttScale.BuildDateUnits(GanttViewMode.Quarter, Nowruz1403, D(2025, 3, 20), J);
        Assert.Equal(["Q1", "Q2", "Q3", "Q4"], GanttScale.LowerLabels(GanttViewMode.Quarter, q, J, En));
        Assert.Equal(["بهار", "تابستان", "پاییز", "زمستان"], GanttScale.LowerLabels(GanttViewMode.Quarter, q, J, Fa));
        var y = GanttScale.BuildDateUnits(GanttViewMode.Year, Nowruz1403, D(2025, 3, 21), J);
        Assert.Equal(["۱۴۰۳", "۱۴۰۴"], GanttScale.LowerLabels(GanttViewMode.Year, y, J, Fa));
        Assert.Equal(["1403", "1404"], GanttScale.LowerLabels(GanttViewMode.Year, y, J, En));
    }

    [Fact]
    public void Gregorian_Labels_Are_Unchanged()
    {
        var units = GanttScale.BuildDateUnits(GanttViewMode.Month, D(2026, 1, 1), D(2026, 3, 1));
        Assert.Equal(["Jan", "Feb", "Mar"], GanttScale.LowerLabels(GanttViewMode.Month, units, CalendarSystem.Gregorian, En));
        Assert.Equal("2026", GanttScale.UpperRuns(GanttViewMode.Month, units)[0].Label);
        var days = GanttScale.BuildDateUnits(GanttViewMode.Day, D(2026, 1, 1), D(2026, 1, 3));
        Assert.Equal(["01", "02", "03"], GanttScale.LowerLabels(GanttViewMode.Day, days));
    }

    // ── Component ───────────────────────────────────────────────────────────

    [Fact]
    public void GanttChart_Jalali_Month_View_Renders_Persian_Headers_Under_Fa()
    {
        var tasks = new[] { new L.GanttTask("t1", "طرح", D(2024, 4, 5), D(2024, 5, 30)) };
        var cut = _ctx.Render<L.GanttChart>(p => p
            .Add(c => c.Tasks, tasks)
            .Add(c => c.ViewMode, GanttViewMode.Month)
            .Add(c => c.CalendarSystem, J)
            .Add(c => c.Culture, Fa));
        var text = cut.Markup;
        Assert.Contains("فروردین", text);
        Assert.Contains("اردیبهشت", text);
        Assert.Contains("۱۴۰۳", text);
        Assert.DoesNotContain(">Apr<", text);
    }

    [Fact]
    public void GanttChart_Default_Gregorian_Month_View_Is_Unchanged()
    {
        var tasks = new[] { new L.GanttTask("t1", "Plan", D(2024, 4, 5), D(2024, 5, 30)) };
        var cut = _ctx.Render<L.GanttChart>(p => p
            .Add(c => c.Tasks, tasks)
            .Add(c => c.ViewMode, GanttViewMode.Month)
            .Add(c => c.Culture, En));
        Assert.Contains("Apr", cut.Markup);
        Assert.DoesNotContain("Farvardin", cut.Markup);
        Assert.DoesNotContain("Far<", cut.Markup);
    }

    [Fact]
    public void GanttBar_Tooltip_Dates_Are_Jalali_Formatted()
    {
        var task = new L.GanttTask("t1", "Design", D(2024, 3, 20), D(2024, 3, 25));
        var cut = _ctx.Render<L.GanttBar>(p => p
            .Add(c => c.Task, task).Add(c => c.X, 0d).Add(c => c.Width, 100d)
            .Add(c => c.RowIndex, 0).Add(c => c.BarHeight, 22)
            .Add(c => c.CalendarSystem, J).Add(c => c.Culture, En));
        Assert.Contains("Far 1, 1403", cut.Markup);
    }

    private static DateTime PersianToGregorian(int y, int m, int d) =>
        new PersianCalendar().ToDateTime(y, m, d, 0, 0, 0, 0);
}
