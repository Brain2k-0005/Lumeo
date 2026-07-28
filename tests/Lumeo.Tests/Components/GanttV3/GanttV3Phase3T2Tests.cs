using System.Globalization;
using Bunit;
using Lumeo.GanttV3;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.GanttV3;

/// <summary>
/// Gantt v3 Phase 3, T2 — scale + region + day styling: Quarter mode, ISO
/// week bands (see <c>GanttScaleTests</c> for the exhaustive pure-math
/// coverage — this file is the COMPONENT-level integration layer: does
/// <see cref="L.GanttTimeline"/>/<see cref="L.Gantt3"/> actually wire the new
/// parameters into rendered markup), off-day column tint, the today-column
/// upgrade (tint + header dot + accent label), and the now-indicator line.
/// </summary>
public class GanttV3Phase3T2Tests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public GanttV3Phase3T2Tests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static DateTime D(int y, int m, int d) => new(y, m, d);

    // ── Quarter mode ──────────────────────────────────────────────────────

    [Fact]
    public void GanttTimeline_Quarter_Mode_Renders_QuarterNum_Lower_And_Year_Upper()
    {
        var rangeStart = D(2025, 10, 1);
        var rangeEnd = D(2026, 7, 1);
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.ViewMode, L.GanttViewMode.Quarter)
            .Add(c => c.RangeStart, rangeStart)
            .Add(c => c.RangeEnd, rangeEnd));

        Assert.Contains("Q4", cut.Markup);
        Assert.Contains("Q1", cut.Markup);
        Assert.Contains("Q2", cut.Markup);
        Assert.Contains("Q3", cut.Markup);
        Assert.Contains("2025", cut.Markup);
        Assert.Contains("2026", cut.Markup);
    }

    [Fact]
    public void Gantt3_PeriodLabel_Formats_Quarter_Mode_As_A_Quarter_Range()
    {
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.ViewMode, L.GanttViewMode.Quarter)
            .Add(c => c.Tasks, new List<L.GanttTask> { new("t1", "Design", D(2026, 1, 2), D(2026, 1, 6)) }));

        // The exact range depends on ComputeInitialRange's padding, but the
        // label must always be "Q<n> <year> – Q<n> <year>" shaped.
        Assert.Matches(@"Q[1-4] \d{4} . Q[1-4] \d{4}", cut.Markup);
    }

    [Fact]
    public void GanttNav_Quarter_Is_Not_In_The_Default_Toolbar_But_Is_Representable_When_Requested()
    {
        var defaultCut = _ctx.Render<L.GanttNav>();
        Assert.DoesNotContain("Quarter", defaultCut.Markup);

        var explicitCut = _ctx.Render<L.GanttNav>(p => p
            .Add(c => c.ZoomLevels, new[] { L.GanttViewMode.Day, L.GanttViewMode.Quarter, L.GanttViewMode.Year }));
        Assert.Contains("Quarter", explicitCut.Markup);
    }

    // ── Off-day column tint ───────────────────────────────────────────────

    [Fact]
    public void MarkOffDays_Tints_The_Correct_Weekend_Columns_In_Day_Mode()
    {
        // Pinned culture (en-US, global default region): this test exercises
        // the DEFAULT (unset OffDays) path, which reads CultureInfo.CurrentCulture
        // via GanttScale.DefaultOffDays — pinned here so the expected Sat/Sun
        // set is deterministic regardless of the test runner's ambient
        // culture (the same determinism concern GanttScaleTests' own class-
        // level pin exists for).
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        try
        {
            // 2026-01-05 (Mon) .. 2026-01-11 (Sun) — exactly one week, so the
            // off-day columns (Sat 01-10, Sun 01-11 under the default Sat/Sun
            // OffDays) are unambiguous: exactly 2 tint divs.
            var cut = _ctx.Render<L.GanttTimeline>(p => p
                .Add(c => c.ViewMode, L.GanttViewMode.Day)
                .Add(c => c.RangeStart, D(2026, 1, 5))
                .Add(c => c.RangeEnd, D(2026, 1, 11))
                .Add(c => c.MarkOffDays, true));

            var tints = cut.FindAll(".lumeo-gantt-v3-off-day");
            Assert.Equal(2, tints.Count);

            var colW = GanttScale.GetConfig(L.GanttViewMode.Day).ColumnWidth;
            var expectedLefts = new[] { 5 * colW, 6 * colW }; // Sat=index5, Sun=index6 (Mon=0)
            var actualLefts = tints.Select(t =>
            {
                var style = t.GetAttribute("style") ?? "";
                var m = System.Text.RegularExpressions.Regex.Match(style, @"left:(-?\d+(?:\.\d+)?)px");
                Assert.True(m.Success);
                return double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            }).OrderBy(x => x).ToArray();

            Assert.Equal(expectedLefts.Select(x => (double)x), actualLefts);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void MarkOffDays_False_Renders_No_Tint_Even_On_A_Weekend()
    {
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 5))
            .Add(c => c.RangeEnd, D(2026, 1, 11))
            .Add(c => c.MarkOffDays, false));

        Assert.Empty(cut.FindAll(".lumeo-gantt-v3-off-day"));
    }

    [Fact]
    public void MarkOffDays_Honors_An_Explicit_OffDays_Override()
    {
        // Friday+Saturday weekend (e.g. Israel/Gulf-region convention) —
        // 2026-01-05 is a Monday, so Fri=01-09 (index 4), Sat=01-10 (index 5).
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 5))
            .Add(c => c.RangeEnd, D(2026, 1, 11))
            .Add(c => c.MarkOffDays, true)
            .Add(c => c.OffDays, (IReadOnlySet<DayOfWeek>)new HashSet<DayOfWeek> { DayOfWeek.Friday, DayOfWeek.Saturday }));

        Assert.Equal(2, cut.FindAll(".lumeo-gantt-v3-off-day").Count);
    }

    [Fact]
    public void MarkOffDays_Renders_Nothing_In_Week_Mode_Where_A_Column_Spans_Many_Days()
    {
        // SupportsOffDayMarking excludes Week (Unit=Day but Step=7 — a whole
        // WEEK per column, not a single calendar day) — tinting a whole week
        // column because it happens to CONTAIN a Saturday would be a category
        // error (every week column contains a weekend).
        //
        // RangeStart is DELIBERATELY a Saturday (2026-01-10): Week mode's own
        // BuildDateUnits generates one column per RangeStart+7*i, so EVERY
        // rendered column's DayOfWeek is Saturday here — the exact day the
        // default OffDays set contains. This makes the assertion genuinely
        // sensitive to the Step==1 exclusion: disabling it (Unit==Day alone,
        // without the Step check) would make IsOffDayColumn true for EVERY
        // column and this test WOULD catch it (a range starting on a non-
        // weekend day, e.g. Monday, would pass either way — verified via a
        // real disable-check that this exact fixture was needed).
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.ViewMode, L.GanttViewMode.Week)
            .Add(c => c.RangeStart, D(2026, 1, 10))
            .Add(c => c.RangeEnd, D(2026, 2, 7))
            .Add(c => c.MarkOffDays, true));

        Assert.Empty(cut.FindAll(".lumeo-gantt-v3-off-day"));
    }

    // ── Today column upgrade (tint + dot + accent label) ─────────────────

    [Fact]
    public void TodayHighlight_Renders_Tint_Dot_And_Accent_Label_At_The_Correct_Column()
    {
        var today = D(2026, 1, 15); // Thursday
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 12))
            .Add(c => c.RangeEnd, D(2026, 1, 20))
            .Add(c => c.Today, today)
            .Add(c => c.TodayHighlight, true));

        var tint = cut.Find(".lumeo-gantt-v3-today-tint");
        var dot = cut.Find(".lumeo-gantt-v3-today-dot");
        Assert.NotNull(tint);
        Assert.NotNull(dot);

        var origin = GanttScale.BuildDateUnits(L.GanttViewMode.Day, D(2026, 1, 12), D(2026, 1, 20))[0];
        var expectedLeft = GanttScale.DateToPixel(L.GanttViewMode.Day, origin, today);
        Assert.Contains($"left:{expectedLeft.ToString(CultureInfo.InvariantCulture)}px", tint.GetAttribute("style"));

        // Accent label: the lower-row cell for today's own column carries the
        // accent class, no other cell does.
        var accentCells = cut.FindAll(".text-primary.font-bold");
        Assert.Single(accentCells);
        Assert.Equal("15", accentCells[0].TextContent.Trim());
    }

    [Fact]
    public void TodayHighlight_False_Suppresses_Tint_Dot_And_Accent_Label()
    {
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 12))
            .Add(c => c.RangeEnd, D(2026, 1, 20))
            .Add(c => c.Today, D(2026, 1, 15))
            .Add(c => c.TodayHighlight, false));

        Assert.Empty(cut.FindAll(".lumeo-gantt-v3-today-tint"));
        Assert.Empty(cut.FindAll(".lumeo-gantt-v3-today-dot"));
        Assert.Empty(cut.FindAll(".text-primary.font-bold"));
    }

    [Fact]
    public void TodayColumn_Tint_Left_Edge_Is_The_Whole_Containing_Column_In_Month_Mode()
    {
        // Month mode: a single day's DateToPixel value sits at a FRACTIONAL
        // position within its month column (Codex round 3, P2 #6's own
        // aligned-origin math) — TodayColumnLeft must floor that down to the
        // column's own left edge (index*colW), not leave the tint's left
        // edge at the fractional sub-column offset TodayX itself would give.
        var today = D(2026, 3, 15); // mid-March
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.ViewMode, L.GanttViewMode.Month)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 6, 1))
            .Add(c => c.Today, today)
            .Add(c => c.TodayHighlight, true));

        var origin = GanttScale.BuildDateUnits(L.GanttViewMode.Month, D(2026, 1, 1), D(2026, 6, 1))[0];
        var colW = GanttScale.GetConfig(L.GanttViewMode.Month).ColumnWidth;
        var rawTodayX = GanttScale.DateToPixel(L.GanttViewMode.Month, origin, today, colW);
        var expectedColumnLeft = 2 * colW; // March = index 2 (Jan=0, Feb=1, Mar=2)

        Assert.NotEqual(expectedColumnLeft, rawTodayX); // sanity: the raw (fractional) position is NOT the column edge

        var tint = cut.Find(".lumeo-gantt-v3-today-tint");
        Assert.Contains($"left:{((double)expectedColumnLeft).ToString(CultureInfo.InvariantCulture)}px", tint.GetAttribute("style"));
    }

    // ── Now indicator ──────────────────────────────────────────────────────

    [Fact]
    public void NowIndicator_Renders_A_Line_In_QuarterDay_Mode_At_The_Correct_Position()
    {
        var now = new DateTime(2026, 1, 15, 14, 0, 0);
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.ViewMode, L.GanttViewMode.QuarterDay)
            .Add(c => c.RangeStart, D(2026, 1, 15))
            .Add(c => c.RangeEnd, D(2026, 1, 16))
            .Add(c => c.NowIndicator, true)
            .Add(c => c.Now, now));

        var line = cut.Find(".lumeo-gantt-v3-now-line");
        var origin = GanttScale.BuildDateUnits(L.GanttViewMode.QuarterDay, D(2026, 1, 15), D(2026, 1, 16))[0];
        var expectedX = GanttScale.DateToPixel(L.GanttViewMode.QuarterDay, origin, now);
        Assert.Contains($"left:{expectedX.ToString(CultureInfo.InvariantCulture)}px", line.GetAttribute("style"));
    }

    [Fact]
    public void NowIndicator_Renders_Nothing_In_Day_Mode_Even_When_Enabled()
    {
        // SupportsNowIndicator excludes Day (and every other non-Hour-unit
        // mode) — "now" within a single day has no distinct pixel position at
        // Day-column granularity (that's what TodayHighlight's own column
        // already shows).
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 12))
            .Add(c => c.RangeEnd, D(2026, 1, 20))
            .Add(c => c.NowIndicator, true)
            .Add(c => c.Now, D(2026, 1, 15)));

        Assert.Empty(cut.FindAll(".lumeo-gantt-v3-now-line"));
    }

    [Fact]
    public void NowIndicator_False_Renders_Nothing_Even_In_A_Supported_Mode()
    {
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.ViewMode, L.GanttViewMode.QuarterDay)
            .Add(c => c.RangeStart, D(2026, 1, 15))
            .Add(c => c.RangeEnd, D(2026, 1, 16))
            .Add(c => c.NowIndicator, false)
            .Add(c => c.Now, new DateTime(2026, 1, 15, 14, 0, 0)));

        Assert.Empty(cut.FindAll(".lumeo-gantt-v3-now-line"));
    }

    [Fact]
    public async Task Gantt3_Resolves_NowIndicator_Time_Via_The_Extended_Interop_Family_When_Enabled()
    {
        // Design spec Phase 3, T2 — GanttV3GetLocalDateTimeAsync extends the
        // same GanttV3Get* browser-clock family GanttV3GetLocalDateAsync
        // already established. Gated on NowIndicator: a chart that never
        // enables the feature must not pay the extra interop round-trip.
        _interop.GanttV3LocalDateTimeToReturn = "2026-01-15T14:30:00";

        var cutWithout = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { new("t1", "Design", D(2026, 1, 2), D(2026, 1, 6)) })
            .Add(c => c.NowIndicator, false));
        await cutWithout.InvokeAsync(() => { });
        Assert.Equal(0, _interop.GanttV3GetLocalDateTimeCallCount);

        var cutWith = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { new("t1", "Design", D(2026, 1, 2), D(2026, 1, 6)) })
            .Add(c => c.ViewMode, L.GanttViewMode.QuarterDay)
            .Add(c => c.NowIndicator, true));
        await cutWith.InvokeAsync(() => { });

        Assert.True(_interop.GanttV3GetLocalDateTimeCallCount >= 1);
    }
}
