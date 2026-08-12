using Lumeo.SchedulerKernel;
using Xunit;

namespace Lumeo.Tests.Components.SchedulerKernel;

/// <summary>
/// ReUI parity — its event calendar accepts raw RFC 5545 <c>RRULE</c> strings, which is the form
/// anyone holding iCalendar / Google / Outlook data actually has. Lumeo modelled recurrence only
/// as a structured record, so such a consumer had to hand-write a parser before their own data
/// could be passed in at all.
///
/// The parser is deliberately STRICT: parts outside the subset
/// <see cref="SchedulerRecurrenceExpander"/> implements are rejected, never silently dropped.
/// That choice is what most of the negative cases below pin — accepting a rule and then quietly
/// ignoring half of it would hand the caller a plausible-looking, wrong series built from their
/// real calendar data.
/// </summary>
public class SchedulerRRuleParserTests
{
    // 2026-08-10 is a Monday.
    private static readonly DateTime Monday = new(2026, 8, 10, 9, 0, 0);
    private static readonly DateTime MondayEnd = new(2026, 8, 10, 10, 0, 0);

    // ── Accepted forms ───────────────────────────────────────────────────────

    [Fact]
    public void Parses_The_Minimal_Rule()
    {
        var rule = SchedulerRRuleParser.Parse("FREQ=DAILY");

        Assert.Equal(SchedulerRecurrenceFrequency.Daily, rule.Freq);
        Assert.Equal(1, rule.Interval); // RFC 5545 default
        Assert.Null(rule.Count);
        Assert.Null(rule.Until);
        Assert.Null(rule.ByDay);
    }

    [Theory]
    [InlineData("FREQ=WEEKLY;BYDAY=MO,WE")]
    [InlineData("RRULE:FREQ=WEEKLY;BYDAY=MO,WE")]   // the full iCalendar content line
    [InlineData("freq=weekly;byday=mo,we")]          // RFC 5545 tokens are case-insensitive
    [InlineData(" FREQ=WEEKLY ; BYDAY = MO , WE ")]  // incidental whitespace
    public void Accepts_The_Surface_Variations_A_Real_Calendar_Emits(string rrule)
    {
        var rule = SchedulerRRuleParser.Parse(rrule);

        Assert.Equal(SchedulerRecurrenceFrequency.Weekly, rule.Freq);
        Assert.Collection(rule.ByDay!,
            d => Assert.Equal(DayOfWeek.Monday, d.Day),
            d => Assert.Equal(DayOfWeek.Wednesday, d.Day));
    }

    [Fact]
    public void Parses_Interval_And_Count()
    {
        var rule = SchedulerRRuleParser.Parse("FREQ=WEEKLY;INTERVAL=2;COUNT=10");

        Assert.Equal(2, rule.Interval);
        Assert.Equal(10, rule.Count);
        Assert.Null(rule.Until);
    }

    [Theory]
    [InlineData("FREQ=DAILY;UNTIL=20261231")]
    [InlineData("FREQ=DAILY;UNTIL=20261231T235959")]
    [InlineData("FREQ=DAILY;UNTIL=20261231T235959Z")]  // the end-of-day idiom real calendars emit
    public void Parses_Every_Until_Form(string rrule)
    {
        var rule = SchedulerRRuleParser.Parse(rrule);

        Assert.NotNull(rule.Until);
        // The Z form is accepted but NOT shifted to local time: the kernel is
        // wall-clock throughout, so converting only this value would make UNTIL
        // disagree with every other date in the same rule.
        Assert.Equal(new DateTime(2026, 12, 31), rule.Until!.Value.Date);
    }

    [Theory]
    [InlineData("2MO", 2, DayOfWeek.Monday)]
    [InlineData("-1FR", -1, DayOfWeek.Friday)]
    [InlineData("+3WE", 3, DayOfWeek.Wednesday)]
    public void Parses_Byday_Ordinals(string term, int expectedOrdinal, DayOfWeek expectedDay)
    {
        var rule = SchedulerRRuleParser.Parse($"FREQ=MONTHLY;BYDAY={term}");

        var day = Assert.Single(rule.ByDay!);
        Assert.Equal(expectedDay, day.Day);
        Assert.Equal(expectedOrdinal, day.Ordinal);
    }

    [Fact]
    public void A_Bare_Byday_Term_Has_No_Ordinal()
    {
        // Distinct from ordinal 1: the record documents null as "unset", and the
        // expander gives the two different meanings for weekly rules.
        var rule = SchedulerRRuleParser.Parse("FREQ=WEEKLY;BYDAY=FR");

        Assert.Null(Assert.Single(rule.ByDay!).Ordinal);
    }

    // ── Rejected forms ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("BYDAY=MO")]                       // FREQ is the one required part
    [InlineData("FREQ=YEARLY")]                    // outside the expander's own scope
    [InlineData("FREQ=HOURLY")]
    [InlineData("FREQ=WEEKLY;BYMONTHDAY=15")]      // unsupported part, must not be dropped
    [InlineData("FREQ=WEEKLY;BYSETPOS=-1")]
    [InlineData("FREQ=WEEKLY;WKST=SU")]
    [InlineData("FREQ=WEEKLY;COUNT=5;UNTIL=20261231")] // RFC 5545: mutually exclusive
    [InlineData("FREQ=WEEKLY;INTERVAL=0")]         // must be positive
    [InlineData("FREQ=WEEKLY;INTERVAL=-1")]
    [InlineData("FREQ=WEEKLY;COUNT=0")]
    [InlineData("FREQ=WEEKLY;BYDAY=XX")]           // not a weekday
    [InlineData("FREQ=WEEKLY;BYDAY=0MO")]          // RFC 5545 forbids ordinal 0
    [InlineData("FREQ=WEEKLY;BYDAY=54MO")]         // and bounds ordinals to +/-1..53
    [InlineData("FREQ=DAILY;BYDAY=MO")]            // daily expansion ignores ByDay entirely
    [InlineData("FREQ=WEEKLY;BYDAY=2MO")]          // weekly expansion ignores the ordinal
    [InlineData("FREQ=MONTHLY;BYDAY=MO")]          // unqualified monthly term means EVERY Monday
    [InlineData("FREQ=MONTHLY;BYDAY=1MO,1MO")]     // duplicate term would double-emit and burn COUNT
    [InlineData("FREQ=DAILY;INTERVAL=2;INTERVAL=3")] // duplicate part is ambiguous
    [InlineData(";FREQ=DAILY")]                    // empty leading segment
    [InlineData("FREQ=DAILY;")]                    // empty trailing segment
    [InlineData("FREQ=DAILY;;COUNT=2")]            // empty middle segment
    [InlineData("FREQ=DAILY;UNTIL=20260812T080000")] // a mid-day cutoff cannot survive date-only bounding
    [InlineData("FREQ=WEEKLY;BYDAY=")]             // empty value
    [InlineData("FREQ=DAILY;UNTIL=2026-12-31")]    // ISO-with-dashes is not RFC 5545's form
    [InlineData("FREQ=DAILY;FREQ=WEEKLY")]         // duplicate part is ambiguous
    [InlineData("FREQ")]                           // no value
    [InlineData("=WEEKLY")]                        // no name
    public void Rejects_Anything_Malformed_Or_Outside_The_Supported_Subset(string? rrule)
    {
        Assert.False(SchedulerRRuleParser.TryParse(rrule, out var rule));
        Assert.Null(rule);
        Assert.Throws<FormatException>(() => SchedulerRRuleParser.Parse(rrule));
    }

    // ── The point of the whole thing: the expander accepts what we produce ───

    [Fact]
    public void A_Parsed_Weekly_Rule_Expands_To_The_Dates_It_Describes()
    {
        // Structural assertions alone would pass for a rule the expander cannot
        // actually use, so the real proof is running the parser's output through
        // the engine it exists to feed.
        var rule = SchedulerRRuleParser.Parse("FREQ=WEEKLY;BYDAY=MO,WE;COUNT=4");
        var ev = new SchedulerEvent("ev", "Standup", Monday, MondayEnd) { Recurrence = rule };

        var instances = SchedulerRecurrenceExpander.Expand(ev, Monday, Monday.AddDays(21));

        Assert.Equal(4, instances.Count);
        Assert.Collection(instances,
            i => Assert.Equal(new DateTime(2026, 8, 10), i.Start.Date), // Mon
            i => Assert.Equal(new DateTime(2026, 8, 12), i.Start.Date), // Wed
            i => Assert.Equal(new DateTime(2026, 8, 17), i.Start.Date), // Mon
            i => Assert.Equal(new DateTime(2026, 8, 19), i.Start.Date)); // Wed
    }

    [Fact]
    public void A_Parsed_Monthly_Ordinal_Rule_Expands_To_The_Dates_It_Describes()
    {
        // "last Friday of the month" — the case the rule record's own remarks
        // call out, driven end-to-end from its RRULE text.
        var start = new DateTime(2026, 8, 28, 9, 0, 0); // last Friday of August 2026
        var rule = SchedulerRRuleParser.Parse("FREQ=MONTHLY;BYDAY=-1FR;COUNT=3");
        var ev = new SchedulerEvent("ev", "Retro", start, start.AddHours(1)) { Recurrence = rule };

        var instances = SchedulerRecurrenceExpander.Expand(ev, start, start.AddMonths(4));

        Assert.Collection(instances,
            i => Assert.Equal(new DateTime(2026, 8, 28), i.Start.Date),
            i => Assert.Equal(new DateTime(2026, 9, 25), i.Start.Date),
            i => Assert.Equal(new DateTime(2026, 10, 30), i.Start.Date));
    }
}
