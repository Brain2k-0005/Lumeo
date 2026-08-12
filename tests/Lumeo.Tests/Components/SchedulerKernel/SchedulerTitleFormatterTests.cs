using System.Globalization;
using Lumeo.SchedulerKernel;
using Lumeo.Services.Localization;
using Xunit;

namespace Lumeo.Tests.Components.SchedulerKernel;

/// <summary>
/// Tests for <see cref="SchedulerTitleFormatter"/> — spec §7.1: "regression-testing the exact
/// WeekOf bug (assert the formatted title contains the actual date substring, not just that it
/// doesn't throw — a test that only checks 'does not throw' would have passed on the buggy
/// code)". <see cref="FakeLocalizer"/> below is a locally-constructed <see cref="ILumeoLocalizer"/>
/// — this class never touches the real, shipped locale tables (a sibling branch is fixing the
/// underlying `Scheduler.WeekOf` data bug there; this test suite doesn't depend on that fix
/// landing first, and correctly fails independent of it if THIS class's own arg-plumbing regresses).
/// </summary>
public class SchedulerTitleFormatterTests
{
    /// <summary>
    /// Minimal <see cref="ILumeoLocalizer"/> fake: a fixed key->format-string table, with the
    /// args-aware indexer actually applying <see cref="string.Format(string, object?[])"/> —
    /// i.e. behaving like a CORRECTLY authored locale entry (one that HAS a <c>{0}</c>
    /// placeholder), which is what wave 0's fix will make the real en/de tables look like.
    /// </summary>
    private sealed class FakeLocalizer : ILumeoLocalizer
    {
        private readonly Dictionary<string, string> _strings;
        public FakeLocalizer(Dictionary<string, string> strings) => _strings = strings;

        public string this[string key] => _strings.TryGetValue(key, out var v) ? v : key;

        public string this[string key, params object?[] args] =>
            _strings.TryGetValue(key, out var format) ? string.Format(CultureInfo.InvariantCulture, format, args) : key;

        public bool TryGet(string key, out string value)
        {
            if (_strings.TryGetValue(key, out var found))
            {
                value = found;
                return true;
            }
            value = key;
            return false;
        }
    }

    private static readonly ILumeoLocalizer CorrectLocalizer = new FakeLocalizer(new Dictionary<string, string>
    {
        ["Scheduler.WeekOf"] = "Week of {0}",
    });

    private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");

    // 2026-08-12 is a Wednesday.
    private static readonly DateTime Anchor = new(2026, 8, 12);

    [Fact]
    public void Week_Title_Contains_The_Actual_Start_Of_Week_Date_Substring()
    {
        // This is the exact regression shape the WeekOf bug needed and a "does not throw" test
        // would have missed: en/de's *data* had no {0} placeholder, so string.Format silently
        // dropped the date and returned the literal "Week of" with nothing after it. Asserting
        // Contains(dateSubstring) fails loudly on that bug; Assert.NotNull/DoesNotThrow would not.
        var expectedDate = SchedulerDateMath.StartOfWeek(Anchor, DayOfWeek.Monday).ToString("MMM d, yyyy", EnUs);

        var title = SchedulerTitleFormatter.Format(SchedulerView.Week, Anchor, DayOfWeek.Monday, CorrectLocalizer, EnUs);

        Assert.Contains(expectedDate, title);
        Assert.StartsWith("Week of ", title);
    }

    [Fact]
    public void Week_Title_Uses_The_Requested_FirstDayOfWeek_To_Resolve_The_Week_Start()
    {
        var sundayStartTitle = SchedulerTitleFormatter.Format(SchedulerView.Week, Anchor, DayOfWeek.Sunday, CorrectLocalizer, EnUs);
        var mondayStartTitle = SchedulerTitleFormatter.Format(SchedulerView.Week, Anchor, DayOfWeek.Monday, CorrectLocalizer, EnUs);

        // Anchor (Wed 08-12) falls in a week that starts Sun 08-09 under a Sunday-first
        // convention, but Mon 08-10 under a Monday-first convention — different substrings.
        Assert.Contains("Aug 9, 2026", sundayStartTitle);
        Assert.Contains("Aug 10, 2026", mondayStartTitle);
        Assert.NotEqual(sundayStartTitle, mondayStartTitle);
    }

    [Fact]
    public void A_Localizer_Whose_String_Is_Missing_The_Placeholder_Still_Does_Not_Throw()
    {
        // Documents (does not "fix") the actual data-bug shape: if the underlying locale STRING
        // itself has no {0}, the date is dropped — that's a locale-data problem this class
        // cannot solve on its own (it's routed through ILumeoLocalizer precisely so the DATA can
        // be fixed independently, see the sibling branch). This class's own job is to not
        // additionally corrupt or throw on that input.
        var buggyLocalizer = new FakeLocalizer(new Dictionary<string, string> { ["Scheduler.WeekOf"] = "Week of" });

        var title = SchedulerTitleFormatter.Format(SchedulerView.Week, Anchor, DayOfWeek.Monday, buggyLocalizer, EnUs);

        Assert.Equal("Week of", title);
    }

    [Theory]
    [InlineData(SchedulerView.Month)]
    [InlineData(SchedulerView.List)]
    public void Month_And_List_Titles_Contain_The_Month_Name_And_Year(SchedulerView view)
    {
        var title = SchedulerTitleFormatter.Format(view, Anchor, DayOfWeek.Monday, CorrectLocalizer, EnUs);

        Assert.Contains("August", title);
        Assert.Contains("2026", title);
    }

    [Fact]
    public void Day_Title_Contains_The_Full_Date_And_Weekday_Name()
    {
        var title = SchedulerTitleFormatter.Format(SchedulerView.Day, Anchor, DayOfWeek.Monday, CorrectLocalizer, EnUs);

        Assert.Contains("Wednesday", title);
        Assert.Contains("Aug 12, 2026", title);
    }

    [Fact]
    public void Month_Title_Is_Culture_Aware()
    {
        var german = CultureInfo.GetCultureInfo("de-DE");
        var title = SchedulerTitleFormatter.Format(SchedulerView.Month, Anchor, DayOfWeek.Monday, CorrectLocalizer, german);

        Assert.Contains("August", title); // German month name for August is also "August".
        Assert.Contains("2026", title);
    }

    [Fact]
    public void Week_Title_Never_Calls_The_NoArgs_Indexer_For_A_Key_That_Needs_An_Argument()
    {
        // A localizer whose plain (no-args) indexer would silently drop the date (returns a
        // fixed literal) but whose ARGS-aware indexer is correctly wired — proves Format() goes
        // through the args-aware overload, not `string.Format(L["key"], ...)` bolted on top of
        // the plain one (the exact call shape spec §4.6 identifies as the root of the bug).
        var localizer = new PlainIndexerOnlyDropsArgsLocalizer();

        var title = SchedulerTitleFormatter.Format(SchedulerView.Week, Anchor, DayOfWeek.Monday, localizer, EnUs);

        Assert.Contains("Aug 10, 2026", title);
    }

    /// <summary>A localizer whose plain indexer returns a placeholder-free string, but whose args-aware indexer is correct — isolates which overload the formatter actually calls.</summary>
    private sealed class PlainIndexerOnlyDropsArgsLocalizer : ILumeoLocalizer
    {
        public string this[string key] => "Week of"; // no {0} — would drop the date if this were used.
        public string this[string key, params object?[] args] => string.Format(CultureInfo.InvariantCulture, "Week of {0}", args);
        public bool TryGet(string key, out string value)
        {
            value = this[key];
            return true;
        }
    }
}
