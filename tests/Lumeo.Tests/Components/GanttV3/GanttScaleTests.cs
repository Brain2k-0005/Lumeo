using System.Globalization;
using Lumeo.GanttV3;
using Xunit;

namespace Lumeo.Tests.Components.GanttV3;

/// <summary>
/// Pure-math regression tests for <see cref="GanttScale"/> — the C# port of
/// gantt-v2.js's <c>VIEW_MODES</c> table and its <c>dateToX</c>/<c>xToDate</c>/
/// <c>pixelsPerDay</c> helpers (wwwroot/js/gantt-v2.js). No bUnit/DOM involved:
/// this is plain date/pixel arithmetic, asserted against values hand-derived
/// from the JS source so a future edit to either side is caught by a mismatch
/// here instead of a silent visual regression in the v3 render tree.
///
/// TZ/DST note: every DateTime below is constructed with DateTimeKind.Utc so the
/// tests are deterministic regardless of the CI/dev machine's local timezone —
/// see the TZ/DST-safety comment on <see cref="GanttScale"/> for why the math
/// itself is Kind-agnostic (no ToLocalTime/TimeZoneInfo call anywhere in it).
///
/// Culture note (Codex round 2, P2 #4): GanttScale.UpperLabel/LowerLabel's month
/// names now follow CultureInfo.CurrentCulture (v2 parity — v2's fmtMonth/
/// fmtMonthShort use `toLocaleString(undefined, ...)`, the BROWSER's locale, not
/// hardcoded English), so the English month-name assertions below ("January",
/// "Nov", etc.) pin CurrentCulture to en-US for the class's lifetime — same
/// save/restore pattern as AnimatedBeamRegressionTests' StrokeWidths_Use_Invariant_
/// Decimal_Separator_On_Comma_Cultures — so these specs stay deterministic
/// regardless of the CI/dev machine's actual OS locale.
/// </summary>
public class GanttScaleTests : IDisposable
{
    private readonly CultureInfo _originalCulture = CultureInfo.CurrentCulture;

    public GanttScaleTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _originalCulture;
    }

    private static DateTime Utc(int y, int m, int d, int h = 0, int min = 0) =>
        new(y, m, d, h, min, 0, DateTimeKind.Utc);

    // ── VIEW_MODES table port sanity ────────────────────────────────────────

    // xunit [Theory]/[InlineData] would need these `internal` enum types in the
    // PUBLIC test method's signature (CS0051: a public member can't be less
    // accessible than its own parameter types) — InternalsVisibleTo grants the
    // assembly access but doesn't relax that rule. One [Fact] per mode instead;
    // still exhaustive over all 6 GanttViewMode values.
    private static void AssertConfig(
        GanttViewMode mode, int columnWidth, GanttScaleUnit unit, int step,
        int padBefore, int padAfter, GanttHeaderUpperKind upper, GanttHeaderLowerKind lower)
    {
        var cfg = GanttScale.GetConfig(mode);

        Assert.Equal(columnWidth, cfg.ColumnWidth);
        Assert.Equal(unit, cfg.Unit);
        Assert.Equal(step, cfg.Step);
        Assert.Equal(padBefore, cfg.PadBefore);
        Assert.Equal(padAfter, cfg.PadAfter);
        Assert.Equal(upper, cfg.HeaderUpper);
        Assert.Equal(lower, cfg.HeaderLower);
    }

    [Fact]
    public void GetConfig_QuarterDay_Matches_The_V2_VIEW_MODES_Table() =>
        AssertConfig(GanttViewMode.QuarterDay, 38, GanttScaleUnit.Hour, 6, 24, 24, GanttHeaderUpperKind.Day, GanttHeaderLowerKind.Time6h);

    [Fact]
    public void GetConfig_HalfDay_Matches_The_V2_VIEW_MODES_Table() =>
        AssertConfig(GanttViewMode.HalfDay, 38, GanttScaleUnit.Hour, 12, 24, 24, GanttHeaderUpperKind.Day, GanttHeaderLowerKind.Time12h);

    [Fact]
    public void GetConfig_Day_Matches_The_V2_VIEW_MODES_Table() =>
        AssertConfig(GanttViewMode.Day, 38, GanttScaleUnit.Day, 1, 60, 60, GanttHeaderUpperKind.Month, GanttHeaderLowerKind.DayNum);

    [Fact]
    public void GetConfig_Week_Matches_The_V2_VIEW_MODES_Table() =>
        AssertConfig(GanttViewMode.Week, 140, GanttScaleUnit.Day, 7, 16, 16, GanttHeaderUpperKind.Month, GanttHeaderLowerKind.WeekRange);

    [Fact]
    public void GetConfig_Month_Matches_The_V2_VIEW_MODES_Table() =>
        AssertConfig(GanttViewMode.Month, 120, GanttScaleUnit.Month, 1, 12, 12, GanttHeaderUpperKind.Year, GanttHeaderLowerKind.MonthName);

    [Fact]
    public void GetConfig_Year_Matches_The_V2_VIEW_MODES_Table() =>
        AssertConfig(GanttViewMode.Year, 120, GanttScaleUnit.Year, 1, 4, 6, GanttHeaderUpperKind.None, GanttHeaderLowerKind.YearNum);

    [Fact]
    public void Row_Bar_And_Header_Constants_Match_V2()
    {
        Assert.Equal(36, GanttScale.RowHeight);
        Assert.Equal(22, GanttScale.DefaultBarHeight);
        Assert.Equal(56, GanttScale.HeaderHeight);
    }

    // ── AlignToUnitStart (Codex round 3, P2 #6) ──────────────────────────────

    [Fact]
    public void AlignToUnitStart_Month_Snaps_A_Mid_Month_Date_To_Day_One()
    {
        var aligned = GanttScale.AlignToUnitStart(GanttViewMode.Month, Utc(2026, 3, 15));
        Assert.Equal(Utc(2026, 3, 1), aligned);
    }

    [Fact]
    public void AlignToUnitStart_Year_Snaps_A_Mid_Year_Date_To_Jan_One()
    {
        var aligned = GanttScale.AlignToUnitStart(GanttViewMode.Year, Utc(2026, 7, 19));
        Assert.Equal(Utc(2026, 1, 1), aligned);
    }

    [Fact]
    public void AlignToUnitStart_Day_And_Hour_Leave_The_Date_Unchanged()
    {
        var date = Utc(2026, 3, 15, 14, 30);
        Assert.Equal(date, GanttScale.AlignToUnitStart(GanttViewMode.Day, date));
        Assert.Equal(date, GanttScale.AlignToUnitStart(GanttViewMode.QuarterDay, date));
    }

    [Fact]
    public void AlignToUnitStart_Month_Restores_The_DateToPixel_Self_Origin_Invariant_A_Naive_Recenter_Breaks()
    {
        // The whole point of this fix (Month mode): DateToPixel(Month, origin,
        // origin) must be exactly 0 — every grid line / header column
        // boundary is drawn at exactly {index * columnWidth}px, which is only
        // consistent with DateToPixel's own "(date.Day-1)/30" fractional term
        // when origin sits exactly on day 1. A raw, TimeSpan-based recenter
        // (e.g. "today +/- half the window") can land the origin mid-month,
        // silently violating this — every bar in that mode ends up shifted by
        // a constant (origin.Day-1)/30 columns relative to the header grid.
        var misaligned = Utc(2026, 3, 15);
        Assert.NotEqual(0, GanttScale.DateToPixel(GanttViewMode.Month, misaligned, misaligned));

        var aligned = GanttScale.AlignToUnitStart(GanttViewMode.Month, misaligned);
        Assert.Equal(0, GanttScale.DateToPixel(GanttViewMode.Month, aligned, aligned));
    }

    // ── Snap math (pixelsPerDay) ─────────────────────────────────────────────

    [Theory]
    [InlineData(GanttViewMode.QuarterDay, 152.0)] // 38 * 24 / 6
    [InlineData(GanttViewMode.HalfDay, 76.0)]     // 38 * 24 / 12
    [InlineData(GanttViewMode.Day, 38.0)]         // 38 / 1
    [InlineData(GanttViewMode.Week, 20.0)]        // 140 / 7
    [InlineData(GanttViewMode.Month, 4.0)]        // 120 / 30
    public void PixelsPerDay_Matches_V2_pixelsPerDay(GanttViewMode mode, double expected)
    {
        Assert.Equal(expected, GanttScale.PixelsPerDay(mode), precision: 10);
    }

    [Fact]
    public void PixelsPerDay_Year_Matches_V2_pixelsPerDay()
    {
        Assert.Equal(120.0 / 365.0, GanttScale.PixelsPerDay(GanttViewMode.Year), precision: 10);
    }

    // ── Culture-aware month names (Codex round 2, P2 #4) ────────────────────

    [Fact]
    public void Month_Names_Follow_CurrentCulture_Not_Hardcoded_English()
    {
        // Regression: proves the fix actually follows CultureInfo.CurrentCulture
        // (v2 parity — its fmtMonth/fmtMonthShort follow the BROWSER's locale via
        // `toLocaleString(undefined, ...)`) rather than merely happening to still
        // pass under the class's en-US pin above, which alone couldn't distinguish
        // "genuinely culture-aware" from "still hardcoded to English by coincidence".
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            var units = GanttScale.BuildDateUnits(GanttViewMode.Day, Utc(2026, 1, 30), Utc(2026, 2, 2));

            var upper = GanttScale.UpperRuns(GanttViewMode.Day, units);
            Assert.Equal(new[]
            {
                new GanttHeaderRun(0, 2, "Januar"),
                new GanttHeaderRun(2, 2, "Februar"),
            }, upper);

            var monthUnits = GanttScale.BuildDateUnits(GanttViewMode.Month, Utc(2026, 1, 1), Utc(2026, 1, 1));
            Assert.Equal(new[] { "Jan" }, GanttScale.LowerLabels(GanttViewMode.Month, monthUnits));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    // ── Header segmentation — all 6 modes ───────────────────────────────────

    [Fact]
    public void Day_Header_Segments_Lower_DayNum_And_Upper_Month_Runs()
    {
        var units = GanttScale.BuildDateUnits(GanttViewMode.Day, Utc(2026, 1, 30), Utc(2026, 2, 2));
        Assert.Equal(new[] { Utc(2026, 1, 30), Utc(2026, 1, 31), Utc(2026, 2, 1), Utc(2026, 2, 2) }, units);

        var lower = GanttScale.LowerLabels(GanttViewMode.Day, units);
        Assert.Equal(new[] { "30", "31", "01", "02" }, lower);

        var upper = GanttScale.UpperRuns(GanttViewMode.Day, units);
        Assert.Equal(new[]
        {
            new GanttHeaderRun(0, 2, "January"),
            new GanttHeaderRun(2, 2, "February"),
        }, upper);
    }

    [Fact]
    public void Week_Header_Segments_Lower_WeekRange_And_Upper_Month_Run()
    {
        var units = GanttScale.BuildDateUnits(GanttViewMode.Week, Utc(2026, 1, 5), Utc(2026, 1, 26));
        Assert.Equal(4, units.Count);

        var lower = GanttScale.LowerLabels(GanttViewMode.Week, units);
        Assert.Equal(new[] { "5/1", "12/1", "19/1", "26/1" }, lower);

        var upper = GanttScale.UpperRuns(GanttViewMode.Week, units);
        Assert.Equal(new[] { new GanttHeaderRun(0, 4, "January") }, upper);
    }

    [Fact]
    public void Month_Header_Segments_Lower_MonthName_And_Upper_Year_Runs()
    {
        var units = GanttScale.BuildDateUnits(GanttViewMode.Month, Utc(2025, 11, 1), Utc(2026, 2, 1));
        Assert.Equal(4, units.Count);

        var lower = GanttScale.LowerLabels(GanttViewMode.Month, units);
        Assert.Equal(new[] { "Nov", "Dec", "Jan", "Feb" }, lower);

        var upper = GanttScale.UpperRuns(GanttViewMode.Month, units);
        Assert.Equal(new[]
        {
            new GanttHeaderRun(0, 2, "2025"),
            new GanttHeaderRun(2, 2, "2026"),
        }, upper);
    }

    [Fact]
    public void Year_Header_Has_Lower_YearNum_And_No_Upper_Row()
    {
        var units = GanttScale.BuildDateUnits(GanttViewMode.Year, Utc(2024, 1, 1), Utc(2027, 1, 1));
        Assert.Equal(4, units.Count);

        var lower = GanttScale.LowerLabels(GanttViewMode.Year, units);
        Assert.Equal(new[] { "2024", "2025", "2026", "2027" }, lower);

        var upper = GanttScale.UpperRuns(GanttViewMode.Year, units);
        Assert.Empty(upper); // v2: headerFmt.upper === '' -> falsy -> never emitted
    }

    [Fact]
    public void QuarterDay_Header_Segments_Lower_Time6h_And_Upper_Day_Run()
    {
        var units = GanttScale.BuildDateUnits(GanttViewMode.QuarterDay, Utc(2026, 1, 1), Utc(2026, 1, 2));
        Assert.Equal(4, units.Count);

        var lower = GanttScale.LowerLabels(GanttViewMode.QuarterDay, units);
        Assert.Equal(new[] { "0:00", "6:00", "12:00", "18:00" }, lower);

        var upper = GanttScale.UpperRuns(GanttViewMode.QuarterDay, units);
        Assert.Equal(new[] { new GanttHeaderRun(0, 4, "January 1") }, upper);
    }

    [Fact]
    public void HalfDay_Header_Segments_Lower_Time12h_And_Upper_Day_Runs()
    {
        var units = GanttScale.BuildDateUnits(GanttViewMode.HalfDay, Utc(2026, 1, 1), Utc(2026, 1, 3));
        Assert.Equal(4, units.Count);

        var lower = GanttScale.LowerLabels(GanttViewMode.HalfDay, units);
        Assert.Equal(new[] { "0:00", "12:00", "0:00", "12:00" }, lower);

        var upper = GanttScale.UpperRuns(GanttViewMode.HalfDay, units);
        Assert.Equal(new[]
        {
            new GanttHeaderRun(0, 2, "January 1"),
            new GanttHeaderRun(2, 2, "January 2"),
        }, upper);
    }

    // ── px <-> date roundtrips ───────────────────────────────────────────────

    [Theory]
    [InlineData(GanttViewMode.Day)]
    [InlineData(GanttViewMode.Week)]
    [InlineData(GanttViewMode.QuarterDay)]
    [InlineData(GanttViewMode.HalfDay)]
    [InlineData(GanttViewMode.Month)]
    [InlineData(GanttViewMode.Year)]
    public void DateToPixel_Then_PixelToDate_Roundtrips_On_A_Step_Aligned_Offset(GanttViewMode mode)
    {
        var cfg = GanttScale.GetConfig(mode);
        var origin = Utc(2026, 1, 1);
        DateTime date = cfg.Unit switch
        {
            GanttScaleUnit.Hour => origin.AddHours(cfg.Step * 5),   // exact multiple of the step
            GanttScaleUnit.Day => origin.AddDays(cfg.Step * 5),
            GanttScaleUnit.Month => origin.AddMonths(cfg.Step * 5),
            GanttScaleUnit.Year => origin.AddYears(cfg.Step * 5),
            _ => origin,
        };

        var px = GanttScale.DateToPixel(mode, origin, date);
        var roundtrip = GanttScale.PixelToDate(mode, origin, px);

        Assert.Equal(date, roundtrip);
    }

    // ── PixelToDateContinuous (Codex round 6, P1 #1) ────────────────────────

    [Theory]
    [InlineData(GanttViewMode.Month)]
    [InlineData(GanttViewMode.Year)]
    public void PixelToDateContinuous_Then_DateToPixel_Roundtrips_Within_Tolerance_For_A_MidUnit_Offset(GanttViewMode mode)
    {
        // A deliberately non-unit-aligned pixel offset (mid-month / mid-year) —
        // exactly the case PixelToDate's own whole-unit rounding snaps away
        // from. The round-trip isn't expected to be bit-exact (the
        // continuous inverse folds its fractional part back in as a plain
        // day count — see AddContinuousMonths/AddContinuousYears' own
        // remarks — which can leave a small residual time-of-day component
        // DateToPixel's day-of-month/day-of-year read then drops), so this
        // asserts "close" (sub-pixel) rather than exact.
        var origin = Utc(2026, 1, 1);
        var cfg = GanttScale.GetConfig(mode);
        var px = cfg.ColumnWidth * 2.5;

        var continuous = GanttScale.PixelToDateContinuous(mode, origin, px);
        var roundtripPx = GanttScale.DateToPixel(mode, origin, continuous);

        Assert.True(Math.Abs(roundtripPx - px) < 1.0,
            $"expected the continuous inverse to round-trip within ~1px, got px={px}, roundtripPx={roundtripPx}");
    }

    [Fact]
    public void PixelToDateContinuous_Lands_MidMonth_Instead_Of_Snapping_To_A_Whole_Month()
    {
        var origin = Utc(2026, 1, 1);
        var cfg = GanttScale.GetConfig(GanttViewMode.Month);
        var px = cfg.ColumnWidth * 2.5; // half-way between month 2 and month 3

        var snapped = GanttScale.PixelToDate(GanttViewMode.Month, origin, px);
        var continuous = GanttScale.PixelToDateContinuous(GanttViewMode.Month, origin, px);

        Assert.Equal(1, snapped.Day); // PixelToDate always lands on day-1 for Month mode
        Assert.NotEqual(1, continuous.Day); // continuous lands mid-month instead
    }

    [Fact]
    public void PixelToDateContinuous_Lands_MidYear_Instead_Of_Snapping_To_A_Whole_Year()
    {
        var origin = Utc(2026, 1, 1);
        var cfg = GanttScale.GetConfig(GanttViewMode.Year);
        var px = cfg.ColumnWidth * 2.5; // half-way between year 2 and year 3

        var snapped = GanttScale.PixelToDate(GanttViewMode.Year, origin, px);
        var continuous = GanttScale.PixelToDateContinuous(GanttViewMode.Year, origin, px);

        Assert.Equal(1, snapped.Month); // PixelToDate always lands on Jan 1 for Year mode
        Assert.NotEqual(1, continuous.Month); // continuous lands mid-year instead
    }

    // ── PixelToDateContinuous: short-month overflow fix (Codex round 6 review / cx6b, Important #1) ──

    [Fact]
    public void PixelToDateContinuous_Does_Not_Overflow_Into_The_Next_Month_For_A_Short_February()
    {
        // origin=Jan 1, 2026 (non-leap year - February has 28 days),
        // totalMonths=1.99 landed on 2026-03-02 under the OLD fixed-30-day-
        // per-month assumption: the exact analytic inverse of DateToPixel's
        // own /30.0 divisor implies a day offset of 0.99*30=29.7, which
        // silently overflowed past February's own last day when added via
        // plain AddDays. The fix clamps the day offset to the month's own
        // actual length, keeping the result inside February.
        var origin = Utc(2026, 1, 1);
        var cfg = GanttScale.GetConfig(GanttViewMode.Month);
        var px = cfg.ColumnWidth * 1.99;

        var continuous = GanttScale.PixelToDateContinuous(GanttViewMode.Month, origin, px);

        Assert.Equal(2026, continuous.Year);
        Assert.Equal(2, continuous.Month); // stays in February - NOT March
        Assert.True(continuous.Day <= 28,
            $"expected the result to stay within February 2026's own 28 days, got day={continuous.Day}");
    }

    [Fact]
    public void PixelToDateContinuous_Does_Not_Overflow_Past_A_Leap_Year_Februarys_29_Days()
    {
        // 2028 is a leap year (February has 29 days) - the same overflow
        // risk as the non-leap case above, confirming DateTime.DaysInMonth
        // (not a hardcoded 28) is what actually gates the clamp.
        var origin = Utc(2028, 1, 1);
        var cfg = GanttScale.GetConfig(GanttViewMode.Month);
        var px = cfg.ColumnWidth * 1.99;

        var continuous = GanttScale.PixelToDateContinuous(GanttViewMode.Month, origin, px);

        Assert.Equal(2028, continuous.Year);
        Assert.Equal(2, continuous.Month); // stays in February - NOT March
        Assert.True(continuous.Day <= 29,
            $"expected the result to stay within leap-year February 2028's own 29 days, got day={continuous.Day}");
    }

    [Fact]
    public void PixelToDateContinuous_Roundtrips_Precisely_For_A_Reachable_MidMonth_Offset_In_A_31Day_Month()
    {
        // A fraction WITHIN what a 31-day month can actually represent -
        // unlike the overflow tests above, which deliberately probe past a
        // SHORT month's own saturation point, this is the common case in
        // practice (Gantt3 only ever feeds this a live scroll position that
        // itself came from real rendered content) and round-trips exactly:
        // day offset 29 (of January's 31 days) is a WHOLE number, so there
        // is no residual time-of-day component for DateToPixel's own
        // integer day/month read to drop.
        var origin = Utc(2026, 1, 1); // January has 31 days
        var cfg = GanttScale.GetConfig(GanttViewMode.Month);
        var px = cfg.ColumnWidth * (29.0 / 30.0); // day offset 29 exactly -> Jan 30

        var continuous = GanttScale.PixelToDateContinuous(GanttViewMode.Month, origin, px);
        var roundtripPx = GanttScale.DateToPixel(GanttViewMode.Month, origin, continuous);

        Assert.Equal(1, continuous.Month);
        Assert.Equal(30, continuous.Day);
        Assert.True(Math.Abs(roundtripPx - px) < 1.0,
            $"expected an exact round-trip for a reachable mid-month offset, got px={px}, roundtripPx={roundtripPx}");
    }

    [Fact]
    public void DateToPixel_Roundtrip_Is_Stable_Across_The_2026_EU_Spring_Forward_DST_Date()
    {
        // 2026-03-29 is the EU spring-forward date (clocks jump 02:00 -> 03:00 in
        // Europe/Berlin local time). Both origin and date below are DateTimeKind.Utc
        // and the math never touches TimeZoneInfo/ToLocalTime — see the TZ/DST-safety
        // note on GanttScale. This test pins that guarantee: a hard-coded 30-hour
        // offset across this date must map to exactly 30 "hour" units and roundtrip
        // byte-for-byte, proving the local machine's timezone (which may itself be
        // mid-DST-transition on the day this suite runs) cannot perturb the result.
        var origin = Utc(2026, 3, 28); // day before the transition
        var date = origin.AddHours(30); // 2026-03-29T06:00:00Z

        var px = GanttScale.DateToPixel(GanttViewMode.QuarterDay, origin, date);
        Assert.Equal(190.0, px, precision: 10); // (30 / 6) * 38

        var roundtrip = GanttScale.PixelToDate(GanttViewMode.QuarterDay, origin, px);
        Assert.Equal(date, roundtrip);
        Assert.Equal(DateTimeKind.Utc, roundtrip.Kind);
    }

    [Fact]
    public void BuildDateUnits_Across_The_DST_Date_Produces_The_Expected_Hour_Columns()
    {
        // Same DST date as above, but exercising BuildDateUnits directly (the column
        // generation consumed by the v3 header/grid render) rather than a single
        // DateToPixel call.
        var units = GanttScale.BuildDateUnits(GanttViewMode.HalfDay, Utc(2026, 3, 28), Utc(2026, 3, 30));
        Assert.Equal(new[]
        {
            Utc(2026, 3, 28, 0, 0),
            Utc(2026, 3, 28, 12, 0),
            Utc(2026, 3, 29, 0, 0),
            Utc(2026, 3, 29, 12, 0),
        }, units);
    }

    // ── Negative-tie rounding (review finding: PixelToDate must match JS Math.round,
    //    which breaks exact half-integer ties toward POSITIVE infinity, NOT away from
    //    zero) ───────────────────────────────────────────────────────────────────────
    //
    // Every pixel value below was chosen so the division/multiplication chain inside
    // PixelToDate lands on an EXACT (bit-for-bit) half-integer — verified empirically
    // (dotnet-script) before writing these, not just hand-derived — so the assertions
    // pin real tie-breaking behavior, not floating-point noise near a tie:
    //   Day:        pixel / 38 * 1  -> -19  => -0.5,  -95  => -2.5 (38 = 2*19, so
    //               n/38 is exactly representable whenever n is a multiple of 19).
    //   QuarterDay: pixel / 38 * 6  -> -9.5 => -1.5, -28.5 => -4.5 (colW/step's 3
    //               factor cancels only against an odd multiple of colW/8, which is
    //               why -0.5/-2.5 themselves aren't exactly reachable for this mode —
    //               -1.5/-4.5 are the equivalent nearest exact ties).
    //
    // For every case, JS Math.round rounds the tie toward +infinity (the fractional
    // part rounds UP even though the number is negative), which is one less negative
    // than MidpointRounding.AwayFromZero would give — that divergence is exactly the
    // dormant bug this review caught (Day mode, pixel -19 -> v2 kept the same day,
    // a pre-fix v3 silently moved one day earlier).

    [Theory]
    [InlineData(-19.0, -0.5, 0)]   // JS Math.round(-0.5) === -0 (i.e. 0); AwayFromZero would give -1
    [InlineData(-95.0, -2.5, -2)]  // JS Math.round(-2.5) === -2; AwayFromZero would give -3
    public void PixelToDate_Day_Rounds_Negative_Ties_Toward_Positive_Infinity_Like_JS(
        double pixel, double expectedExactValue, int expectedDays)
    {
        var origin = Utc(2026, 6, 15);

        // Precondition: pin that this pixel really does produce an exact tie (not an
        // approximation near one) before asserting the rounding direction on it.
        Assert.Equal(expectedExactValue, (pixel / 38.0) * 1.0, precision: 15);

        var result = GanttScale.PixelToDate(GanttViewMode.Day, origin, pixel);
        Assert.Equal(origin.AddDays(expectedDays), result);
    }

    [Theory]
    [InlineData(-9.5, -1.5, -1)]   // JS Math.round(-1.5) === -1; AwayFromZero would give -2
    [InlineData(-28.5, -4.5, -4)]  // JS Math.round(-4.5) === -4; AwayFromZero would give -5
    public void PixelToDate_QuarterDay_Rounds_Negative_Ties_Toward_Positive_Infinity_Like_JS(
        double pixel, double expectedExactValue, int expectedHours)
    {
        var origin = Utc(2026, 6, 15);

        Assert.Equal(expectedExactValue, (pixel / 38.0) * 6.0, precision: 15);

        var result = GanttScale.PixelToDate(GanttViewMode.QuarterDay, origin, pixel);
        Assert.Equal(origin.AddHours(expectedHours), result);
    }

    // ── ColumnWidth override threading (Codex review wave) ──────────────────
    //
    // Regression: DateToPixel/PixelToDate/PixelsPerDay/BarGeometry previously
    // always scaled by the view mode's CONFIG column width, ignoring a
    // caller-supplied override entirely except for BarGeometry's milestone
    // center-of-column term. A consumer setting Gantt3.ColumnWidth got a
    // correctly-rescaled header/grid (GanttTimeline computes those directly
    // from EffectiveColumnWidth) but bars/arrows/today-marker stayed on the
    // mode's default width — visibly misaligned.

    [Fact]
    public void DateToPixel_Honors_A_ColumnWidth_Override_Instead_Of_The_Mode_Default()
    {
        var origin = Utc(2026, 3, 1);
        var date = Utc(2026, 3, 3); // 2 days after origin

        var withDefault = GanttScale.DateToPixel(GanttViewMode.Day, origin, date);
        var withOverride = GanttScale.DateToPixel(GanttViewMode.Day, origin, date, columnWidthOverride: 76);

        Assert.Equal(2 * 38.0, withDefault, precision: 10); // Day's default columnWidth is 38
        Assert.Equal(2 * 76.0, withOverride, precision: 10); // scales linearly with the override
    }

    [Fact]
    public void PixelToDate_Honors_A_ColumnWidth_Override_Instead_Of_The_Mode_Default()
    {
        var origin = Utc(2026, 3, 1);

        // 152px = 2 days at a 76px override (vs. 4 days at the 38px default).
        var result = GanttScale.PixelToDate(GanttViewMode.Day, origin, 152.0, columnWidthOverride: 76);

        Assert.Equal(origin.AddDays(2), result);
    }

    [Fact]
    public void PixelsPerDay_Honors_A_ColumnWidth_Override_Instead_Of_The_Mode_Default()
    {
        Assert.Equal(76.0, GanttScale.PixelsPerDay(GanttViewMode.Day, columnWidthOverride: 76), precision: 10);
        Assert.Equal(38.0, GanttScale.PixelsPerDay(GanttViewMode.Day), precision: 10); // unchanged default behavior
    }

    [Fact]
    public void BarGeometry_Bar_X_And_Width_Scale_With_A_ColumnWidth_Override()
    {
        var origin = Utc(2026, 3, 1);
        var task = new GanttTask("t1", "Task", Utc(2026, 3, 3), Utc(2026, 3, 5)); // 2-day span, starting 2 days after origin

        var (xDefault, widthDefault) = GanttScale.BarGeometry(task, GanttViewMode.Day, origin, columnWidth: 38, barHeight: 22);
        var (xOverride, widthOverride) = GanttScale.BarGeometry(task, GanttViewMode.Day, origin, columnWidth: 76, barHeight: 22);

        Assert.Equal(2 * 38.0, xDefault, precision: 10);
        Assert.Equal(3 * 38.0, widthDefault, precision: 10); // end+1day inclusive -> 3 columns wide
        Assert.Equal(2 * 76.0, xOverride, precision: 10);
        Assert.Equal(3 * 76.0, widthOverride, precision: 10);
    }

    [Fact]
    public void BarGeometry_Milestone_Center_And_Bounding_Box_Scale_With_A_ColumnWidth_Override()
    {
        var origin = Utc(2026, 3, 1);
        var milestone = new GanttTask("m1", "Kickoff", Utc(2026, 3, 3), Utc(2026, 3, 3), IsMilestone: true);

        var (xDefault, widthDefault) = GanttScale.BarGeometry(milestone, GanttViewMode.Day, origin, columnWidth: 38, barHeight: 22);
        var (xOverride, widthOverride) = GanttScale.BarGeometry(milestone, GanttViewMode.Day, origin, columnWidth: 76, barHeight: 22);

        // center = dateToX(start, colW) + colW/2; X = center - barHeight/2
        Assert.Equal((2 * 38.0) + 19.0 - 11.0, xDefault, precision: 10);
        Assert.Equal((2 * 76.0) + 38.0 - 11.0, xOverride, precision: 10);
        Assert.Equal(22, widthDefault); // milestone bounding box is always barHeight regardless of columnWidth
        Assert.Equal(22, widthOverride);
    }

    // ── RTL scrollLeft conversion math (Codex round 4, P2 #4) ───────────────
    //
    // gantt-v3.js's toNativeScrollLeft/fromNativeScrollLeft can't be unit
    // tested directly from C# (they're pure JS, and their INPUT — a live
    // browser's detected RTL convention — isn't something a C# test can
    // observe either). What CAN be tested independent of any browser is the
    // CONVERSION MATH itself, given a known convention label: this is a
    // faithful byte-for-byte port of both functions, asserted against all 3
    // conventions, so a future edit to either side is caught by a mismatch
    // here instead of a silent RTL regression only visible in a real browser.

    private enum RtlConvention { Default, Negative, Reverse }

    // Port of gantt-v3.js's toNativeScrollLeft.
    private static double ToNativeScrollLeft(RtlConvention convention, double maxScroll, double logicalTarget) => convention switch
    {
        RtlConvention.Negative => logicalTarget - maxScroll,
        RtlConvention.Reverse => maxScroll - logicalTarget,
        _ => logicalTarget,
    };

    // Port of gantt-v3.js's fromNativeScrollLeft (the exact inverse).
    private static double FromNativeScrollLeft(RtlConvention convention, double maxScroll, double nativeValue) => convention switch
    {
        RtlConvention.Negative => nativeValue + maxScroll,
        RtlConvention.Reverse => maxScroll - nativeValue,
        _ => nativeValue,
    };

    // xunit [Theory]/[InlineData] would need this `private` enum in the
    // PUBLIC test method's signature (CS0051 — same note as AssertConfig's
    // own remarks above) — one [Fact] per convention instead.
    private static void AssertRoundTrip(RtlConvention convention)
    {
        const double maxScroll = 1000;
        const double logicalTarget = 350;

        var native = ToNativeScrollLeft(convention, maxScroll, logicalTarget);
        var recovered = FromNativeScrollLeft(convention, maxScroll, native);

        Assert.Equal(logicalTarget, recovered, precision: 10);
    }

    [Fact]
    public void ToNativeScrollLeft_And_FromNativeScrollLeft_Are_Exact_Inverses_For_Default() =>
        AssertRoundTrip(RtlConvention.Default);

    [Fact]
    public void ToNativeScrollLeft_And_FromNativeScrollLeft_Are_Exact_Inverses_For_Negative() =>
        AssertRoundTrip(RtlConvention.Negative);

    [Fact]
    public void ToNativeScrollLeft_And_FromNativeScrollLeft_Are_Exact_Inverses_For_Reverse() =>
        AssertRoundTrip(RtlConvention.Reverse);

    [Fact]
    public void ToNativeScrollLeft_Default_Convention_Is_A_Pass_Through()
    {
        Assert.Equal(350, ToNativeScrollLeft(RtlConvention.Default, maxScroll: 1000, logicalTarget: 350));
    }

    [Fact]
    public void ToNativeScrollLeft_Negative_Convention_Shifts_By_MaxScroll_Below_Zero()
    {
        // 'negative': 0 = RTL start, -(maxScroll) = the far end — matches the
        // empirically-verified modern-Chromium behavior this fix's own JS
        // comment documents.
        Assert.Equal(-650, ToNativeScrollLeft(RtlConvention.Negative, maxScroll: 1000, logicalTarget: 350));
        Assert.Equal(0, ToNativeScrollLeft(RtlConvention.Negative, maxScroll: 1000, logicalTarget: 1000));
        Assert.Equal(-1000, ToNativeScrollLeft(RtlConvention.Negative, maxScroll: 1000, logicalTarget: 0));
    }

    [Fact]
    public void ToNativeScrollLeft_Reverse_Convention_Mirrors_Around_MaxScroll()
    {
        // 'reverse' (old WebKit): 0 = RTL start, +maxScroll = the far end,
        // increasing in the OPPOSITE direction from 'negative'.
        Assert.Equal(650, ToNativeScrollLeft(RtlConvention.Reverse, maxScroll: 1000, logicalTarget: 350));
        Assert.Equal(0, ToNativeScrollLeft(RtlConvention.Reverse, maxScroll: 1000, logicalTarget: 1000));
        Assert.Equal(1000, ToNativeScrollLeft(RtlConvention.Reverse, maxScroll: 1000, logicalTarget: 0));
    }
}
