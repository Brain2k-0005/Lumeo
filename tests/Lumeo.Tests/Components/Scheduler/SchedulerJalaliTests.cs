using System.Globalization;
using Bunit;
using Lumeo.SchedulerKernel;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Scheduler;

/// <summary>
/// <see cref="L.CalendarSystem.Jalali"/> on the Scheduler (#547): the month grid covers the
/// Jalali month, week starts Saturday unless FirstDayOfWeek is set, the toolbar title and
/// column headers are Jalali (Persian digits under fa), time labels use the culture's own
/// AM/PM, and the Gregorian default is untouched. Event data stays Gregorian throughout.
/// </summary>
public class SchedulerJalaliTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly CultureInfo _originalUiCulture = CultureInfo.CurrentUICulture;
    private readonly CultureInfo _originalCulture = CultureInfo.CurrentCulture;

    public SchedulerJalaliTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(new TrackingInteropService());
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        CultureInfo.CurrentUICulture = _originalUiCulture;
        CultureInfo.CurrentCulture = _originalCulture;
        await _ctx.DisposeAsync();
    }

    private static void UseCulture(string name)
    {
        var culture = CultureInfo.GetCultureInfo(name);
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
    }

    private static string Iso(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    // ── Grid boundaries (kernel) ─────────────────────────────────────────────

    private static int DaysInGrid(DateTime anchor, DayOfWeek first, out DateTime firstOfMonth, out DateTime lastOfMonth)
    {
        var grid = SchedulerDateMath.BuildMonthGrid(anchor, first, L.CalendarSystem.Jalali);
        var cells = grid.Where(d => SchedulerCalendarMath.SameMonth(L.CalendarSystem.Jalali, d, anchor)).ToList();
        firstOfMonth = cells.First();
        lastOfMonth = cells.Last();
        Assert.Equal(42, grid.Count);
        return cells.Count;
    }

    [Fact]
    public void Farvardin_1403_Starts_On_2024_03_20_And_Has_31_Days()
    {
        var days = DaysInGrid(new DateTime(2024, 3, 25), DayOfWeek.Saturday, out var first, out var last);

        Assert.Equal(31, days);
        Assert.Equal(new DateTime(2024, 3, 20), first);
        Assert.Equal(new DateTime(2024, 4, 19), last);
    }

    [Fact]
    public void Esfand_1402_Has_29_Days()
    {
        // 1402 is not a leap year.
        var days = DaysInGrid(new DateTime(2024, 3, 1), DayOfWeek.Saturday, out var first, out var last);

        Assert.Equal(29, days);
        Assert.Equal(new DateTime(2024, 2, 20), first);
        Assert.Equal(new DateTime(2024, 3, 19), last);
    }

    [Fact]
    public void Esfand_1403_Has_30_Days_Because_1403_Is_A_Leap_Year()
    {
        var days = DaysInGrid(new DateTime(2025, 3, 1), DayOfWeek.Saturday, out var first, out var last);

        Assert.Equal(30, days);
        Assert.Equal(new DateTime(2025, 2, 19), first);
        Assert.Equal(new DateTime(2025, 3, 20), last);
    }

    [Fact]
    public void Grid_Is_Anchored_On_The_Week_Containing_The_First_Of_The_Jalali_Month()
    {
        // 1 Farvardin 1403 is a Wednesday, so a Saturday-first grid opens on Saturday 16 March.
        var grid = SchedulerDateMath.BuildMonthGrid(new DateTime(2024, 3, 25), DayOfWeek.Saturday, L.CalendarSystem.Jalali);

        Assert.Equal(new DateTime(2024, 3, 16), grid[0]);
        Assert.Equal(DayOfWeek.Saturday, grid[0].DayOfWeek);
    }

    [Fact]
    public void Gregorian_Grid_Is_Unchanged()
    {
        var anchor = new DateTime(2024, 3, 25);

        var legacy = SchedulerDateMath.BuildMonthGrid(anchor, DayOfWeek.Monday);
        var explicitGregorian = SchedulerDateMath.BuildMonthGrid(anchor, DayOfWeek.Monday, L.CalendarSystem.Gregorian);

        Assert.Equal(legacy, explicitGregorian);
        Assert.Equal(new DateTime(2024, 2, 26), legacy[0]);
    }

    // ── Month view ───────────────────────────────────────────────────────────

    [Fact]
    public void MonthView_Jalali_Marks_Only_The_Jalali_Month_As_Current_And_Numbers_Cells_In_Jalali()
    {
        UseCulture("en-US");
        var cut = _ctx.Render<L.SchedulerMonthView>(p => p
            .Add(c => c.AnchorDate, new DateTime(2024, 3, 25))
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Saturday)
            .Add(c => c.CalendarSystem, L.CalendarSystem.Jalali));

        string Num(string date) => cut.Find($"[data-cell-date='{date}'] span").TextContent.Trim();

        Assert.Equal("1", Num("2024-03-20"));   // 1 Farvardin 1403
        Assert.Equal("31", Num("2024-04-19"));  // 31 Farvardin
        Assert.Equal("1", Num("2024-04-20"));   // 1 Ordibehesht (trailing cell)
        Assert.Equal("29", Num("2024-03-19"));  // 29 Esfand 1402 (leading cell)

        var current = cut.FindAll("[role='gridcell']")
            .Where(c => !c.ClassName!.Contains("text-muted-foreground")).ToList();
        Assert.Equal(31, current.Count);
    }

    [Fact]
    public void MonthView_Jalali_Under_Fa_Uses_Persian_Digits_And_Culture_Weekday_Names()
    {
        UseCulture("fa-IR");
        var cut = _ctx.Render<L.SchedulerMonthView>(p => p
            .Add(c => c.AnchorDate, new DateTime(2024, 3, 25))
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Saturday)
            .Add(c => c.CalendarSystem, L.CalendarSystem.Jalali));

        Assert.Equal("۱", cut.Find("[data-cell-date='2024-03-20'] span").TextContent.Trim());
        Assert.Equal("۳۱", cut.Find("[data-cell-date='2024-04-19'] span").TextContent.Trim());

        var fa = CultureInfo.GetCultureInfo("fa-IR");
        var headers = cut.FindAll("div.border-b > span").Select(s => s.TextContent.Trim()).Where(t => t.Length > 0).Take(7).ToList();
        Assert.Equal(fa.DateTimeFormat.AbbreviatedDayNames[(int)DayOfWeek.Saturday], headers[0]);
        Assert.Equal(fa.DateTimeFormat.AbbreviatedDayNames[(int)DayOfWeek.Friday], headers[6]);
    }

    [Fact]
    public void MonthView_Gregorian_Default_Keeps_Gregorian_Numbers_And_Current_Month()
    {
        UseCulture("en-US");
        var cut = _ctx.Render<L.SchedulerMonthView>(p => p
            .Add(c => c.AnchorDate, new DateTime(2024, 3, 25)));

        Assert.Equal("20", cut.Find("[data-cell-date='2024-03-20'] span").TextContent.Trim());
        var current = cut.FindAll("[role='gridcell']")
            .Where(c => !c.ClassName!.Contains("text-muted-foreground")).ToList();
        Assert.Equal(31, current.Count); // March 2024
    }

    // ── Scheduler: title, week start, navigation ─────────────────────────────

    [Fact]
    public void Scheduler_Jalali_Month_Title_Is_Persian_Under_FaIR()
    {
        UseCulture("fa-IR");
        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialView, L.SchedulerView.Month)
            .Add(c => c.InitialDate, new DateTime(2024, 3, 25))
            .Add(c => c.CalendarSystem, L.CalendarSystem.Jalali));

        Assert.Equal("فروردین ۱۴۰۳", cut.Find(".text-center").TextContent.Trim());
    }

    [Fact]
    public void Scheduler_Jalali_Month_Title_Is_Transliterated_Under_EnUS()
    {
        UseCulture("en-US");
        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialView, L.SchedulerView.Month)
            .Add(c => c.InitialDate, new DateTime(2024, 3, 25))
            .Add(c => c.CalendarSystem, L.CalendarSystem.Jalali));

        Assert.Equal("Farvardin 1403", cut.Find(".text-center").TextContent.Trim());
    }

    [Fact]
    public void Scheduler_Gregorian_Title_Is_Unchanged()
    {
        UseCulture("en-US");
        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialView, L.SchedulerView.Month)
            .Add(c => c.InitialDate, new DateTime(2024, 3, 25)));

        Assert.Equal("March 2024", cut.Find(".text-center").TextContent.Trim());
    }

    [Fact]
    public void Scheduler_Jalali_Week_Starts_On_Saturday_By_Default()
    {
        UseCulture("en-US");
        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialView, L.SchedulerView.Month)
            .Add(c => c.InitialDate, new DateTime(2024, 3, 25))
            .Add(c => c.CalendarSystem, L.CalendarSystem.Jalali));

        Assert.Equal("2024-03-16", cut.Find("[data-cell-index='0']").GetAttribute("data-cell-date"));
    }

    [Fact]
    public void Scheduler_Jalali_Explicit_FirstDayOfWeek_Wins_Over_Saturday()
    {
        UseCulture("en-US");
        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialView, L.SchedulerView.Month)
            .Add(c => c.InitialDate, new DateTime(2024, 3, 25))
            .Add(c => c.CalendarSystem, L.CalendarSystem.Jalali)
            .Add(c => c.FirstDayOfWeek, 0)); // Sunday

        // 1 Farvardin 1403 is a Wednesday; the Sunday-first grid opens on Sunday 17 March.
        Assert.Equal("2024-03-17", cut.Find("[data-cell-index='0']").GetAttribute("data-cell-date"));
    }

    [Fact]
    public void Scheduler_Gregorian_Default_Week_Still_Starts_On_Monday()
    {
        UseCulture("en-US");
        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialView, L.SchedulerView.Month)
            .Add(c => c.InitialDate, new DateTime(2024, 3, 25)));

        Assert.Equal("2024-02-26", cut.Find("[data-cell-index='0']").GetAttribute("data-cell-date"));
    }

    [Fact]
    public void Scheduler_Jalali_Next_And_Previous_Step_By_Jalali_Month()
    {
        UseCulture("en-US");
        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialView, L.SchedulerView.Month)
            .Add(c => c.InitialDate, new DateTime(2024, 3, 25))
            .Add(c => c.CalendarSystem, L.CalendarSystem.Jalali));

        cut.Find("button[aria-label='Next']").Click();
        Assert.Equal("Ordibehesht 1403", cut.Find(".text-center").TextContent.Trim());

        cut.Find("button[aria-label='Previous']").Click();
        cut.Find("button[aria-label='Previous']").Click();
        Assert.Equal("Esfand 1402", cut.Find(".text-center").TextContent.Trim());
    }

    // ── Week / day time grid ─────────────────────────────────────────────────

    [Fact]
    public void TimeGrid_Jalali_Headers_Carry_Jalali_Day_And_Month_Under_FaIR()
    {
        UseCulture("fa-IR");
        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, new DateTime(2024, 3, 20))
            .Add(c => c.Days, 7)
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Saturday)
            .Add(c => c.CalendarSystem, L.CalendarSystem.Jalali));

        var wednesday = cut.Find("[data-dayheader='2024-03-20']").TextContent;
        Assert.Contains("۱ فروردین", wednesday);
        Assert.Contains(CultureInfo.GetCultureInfo("fa-IR").DateTimeFormat.AbbreviatedDayNames[(int)DayOfWeek.Wednesday], wednesday);

        // The hour gutter localizes its digits too.
        Assert.Contains("۰۹:۰۰", cut.Find("[data-testid='timegrid-hour-gutter']").TextContent);
    }

    [Fact]
    public void TimeGrid_Gregorian_Headers_And_Gutter_Are_Unchanged()
    {
        UseCulture("en-US");
        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, new DateTime(2024, 3, 20))
            .Add(c => c.Days, 7));

        var header = string.Concat(cut.Find("[data-dayheader='2024-03-20']").TextContent.Where(c => !char.IsWhiteSpace(c)));
        Assert.Equal("Wed20", header);
        Assert.Contains("09:00", cut.Find("[data-testid='timegrid-hour-gutter']").TextContent);
    }

    [Fact]
    public void Scheduler_Jalali_Day_Title_Names_The_Jalali_Date()
    {
        UseCulture("en-US");
        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialView, L.SchedulerView.Day)
            .Add(c => c.InitialDate, new DateTime(2024, 3, 20))
            .Add(c => c.CalendarSystem, L.CalendarSystem.Jalali));

        Assert.Equal("Wednesday, 1 Far 1403", cut.Find(".text-center").TextContent.Trim());
    }

    // ── AM/PM from the culture ───────────────────────────────────────────────

    private static CultureInfo CultureWithDesignators(string am, string pm)
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo("en-US").Clone();
        culture.DateTimeFormat.AMDesignator = am;
        culture.DateTimeFormat.PMDesignator = pm;
        culture.DateTimeFormat.ShortTimePattern = "h:mm tt";
        return culture;
    }

    private static readonly L.SchedulerEvent Afternoon =
        new("e1", "Review", new DateTime(2024, 3, 20, 14, 30, 0), new DateTime(2024, 3, 20, 15, 30, 0));

    [Theory]
    [InlineData(L.CalendarSystem.Gregorian)]
    [InlineData(L.CalendarSystem.Jalali)]
    public void Agenda_Time_Uses_The_Cultures_PM_Designator(L.CalendarSystem system)
    {
        var culture = CultureWithDesignators("ق.ظ", "ب.ظ");
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;

        var cut = _ctx.Render<L.SchedulerAgendaView>(p => p
            .Add(c => c.Events, new[] { Afternoon })
            .Add(c => c.AnchorDate, new DateTime(2024, 3, 20))
            .Add(c => c.DaysToShow, 1)
            .Add(c => c.CalendarSystem, system));

        var label = cut.Find("[role='listitem']").TextContent;
        Assert.Contains("ب.ظ", label);
        Assert.DoesNotContain("PM", label);
        Assert.DoesNotContain("AM", label);
    }

    [Fact]
    public void Agenda_Gregorian_Time_Is_Unchanged_Under_EnUS()
    {
        UseCulture("en-US");
        var cut = _ctx.Render<L.SchedulerAgendaView>(p => p
            .Add(c => c.Events, new[] { Afternoon })
            .Add(c => c.AnchorDate, new DateTime(2024, 3, 20))
            .Add(c => c.DaysToShow, 1));

        // ICU (Linux CI) separates the time and the designator with U+202F, Windows NLS with a space.
        var text = cut.Find("[role='listitem']").TextContent.Replace(' ', ' ').Replace(' ', ' ');
        Assert.Contains("2:30 PM", text);
    }

    [Fact]
    public void Agenda_Jalali_Under_FaIR_Has_Persian_Digits_In_Heading_And_Time()
    {
        UseCulture("fa-IR");
        var cut = _ctx.Render<L.SchedulerAgendaView>(p => p
            .Add(c => c.Events, new[] { Afternoon })
            .Add(c => c.AnchorDate, new DateTime(2024, 3, 20))
            .Add(c => c.DaysToShow, 1)
            .Add(c => c.CalendarSystem, L.CalendarSystem.Jalali));

        var markup = cut.Markup;
        Assert.Contains("۱۴۰۳", markup);
        Assert.Contains("فروردین", markup);
        var rowText = cut.Find("[role='listitem']").TextContent;
        Assert.True(!rowText.Contains("14:30"), string.Join(" ", rowText.Trim().Select(ch => ((int)ch).ToString("X"))));
    }

    // ── Timeline month columns ───────────────────────────────────────────────

    [Fact]
    public void Timeline_Month_Columns_Follow_Jalali_Month_Boundaries()
    {
        var origin = L.SchedulerTimelineScale.AlignOrigin(
            L.SchedulerTimelineUnit.Month, new DateTime(2024, 3, 25), DayOfWeek.Saturday, L.CalendarSystem.Jalali);
        var cols = L.SchedulerTimelineScale.BuildColumns(L.SchedulerTimelineUnit.Month, origin, 3, L.CalendarSystem.Jalali);

        Assert.Equal(new DateTime(2024, 3, 20), origin);
        Assert.Equal(new[] { new DateTime(2024, 3, 20), new DateTime(2024, 4, 20), new DateTime(2024, 5, 21) }, cols);
    }

    [Fact]
    public void Timeline_Month_Label_Is_Jalali_And_Gregorian_Overload_Is_Unchanged()
    {
        var en = CultureInfo.GetCultureInfo("en-US");
        var fa = CultureInfo.GetCultureInfo("fa-IR");
        var col = new DateTime(2024, 3, 20);

        Assert.Equal("Far 1403", L.SchedulerTimelineScale.ColumnLabel(L.SchedulerTimelineUnit.Month, col, en, L.CalendarSystem.Jalali));
        Assert.Equal("فروردین ۱۴۰۳", L.SchedulerTimelineScale.ColumnLabel(L.SchedulerTimelineUnit.Month, col, fa, L.CalendarSystem.Jalali));
        Assert.Equal("Mar 2024", L.SchedulerTimelineScale.ColumnLabel(L.SchedulerTimelineUnit.Month, col, en));
    }

    [Fact]
    public void Timeline_Jalali_Month_Pixel_Uses_The_Length_Of_The_Jalali_Month()
    {
        // 1 Ordibehesht 1403 (2024-04-20) is exactly one column in; 15 Farvardin (2024-04-03) is 14 of the month's 31 days along.
        var origin = new DateTime(2024, 3, 20);
        const int width = 100;

        Assert.Equal(100d, L.SchedulerTimelineScale.DateToPixel(L.SchedulerTimelineUnit.Month, origin, new DateTime(2024, 4, 20), width, L.CalendarSystem.Jalali), 6);
        Assert.Equal(14d / 31d * 100d, L.SchedulerTimelineScale.DateToPixel(L.SchedulerTimelineUnit.Month, origin, new DateTime(2024, 4, 3), width, L.CalendarSystem.Jalali), 6);
    }

    // ── No hardcoded AM/PM ───────────────────────────────────────────────────

    [Fact]
    public void Scheduler_Sources_Contain_No_Hardcoded_AM_PM_Strings()
    {
        var dir = FindSchedulerSourceDir();
        var offenders = Directory.EnumerateFiles(dir, "*.*")
            .Where(f => f.EndsWith(".razor") || f.EndsWith(".cs"))
            .SelectMany(f => File.ReadAllLines(f).Select((line, i) => (f, line, i)))
            .Where(t => !t.line.TrimStart().StartsWith("//") && !t.line.TrimStart().StartsWith("///") && !t.line.TrimStart().StartsWith("*"))
            .Where(t => System.Text.RegularExpressions.Regex.IsMatch(t.line, "\"(AM|PM|am|pm|a\\.m\\.|p\\.m\\.)\""))
            .Select(t => $"{Path.GetFileName(t.f)}:{t.i + 1}")
            .ToList();

        Assert.Empty(offenders);
    }

    private static string FindSchedulerSourceDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "Lumeo.Scheduler", "UI", "Scheduler");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Scheduler sources not found");
    }
}
