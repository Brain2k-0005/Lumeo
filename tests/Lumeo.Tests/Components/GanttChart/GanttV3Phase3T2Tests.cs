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
/// <see cref="L.GanttTimeline"/>/<see cref="L.GanttChart"/> actually wire the new
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
    public void GanttChart_PeriodLabel_Formats_Quarter_Mode_As_A_Quarter_Range()
    {
        var cut = _ctx.Render<L.GanttChart>(p => p
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

    // G7 (2026-08-10 shadcn alignment): the off-day tint used to start BELOW
    // the sticky header — the header's OWN lower-row cells had no off-day
    // branch, so a weekend date label stayed full-strength and the muted band
    // visibly broke at the header/canvas seam. ColumnCellClass now carries the
    // same MarkOffDays && SupportsOffDayMarking && IsOffDayColumn gate as the
    // canvas tint below it.
    [Fact]
    public void MarkOffDays_Tints_The_Header_Cell_For_An_Off_Day_Column_Too()
    {
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        try
        {
            // Same fixture as MarkOffDays_Tints_The_Correct_Weekend_Columns_In_Day_Mode:
            // 2026-01-05 (Mon) .. 2026-01-11 (Sun), default Sat/Sun off-days.
            var cut = _ctx.Render<L.GanttTimeline>(p => p
                .Add(c => c.ViewMode, L.GanttViewMode.Day)
                .Add(c => c.RangeStart, D(2026, 1, 5))
                .Add(c => c.RangeEnd, D(2026, 1, 11))
                .Add(c => c.MarkOffDays, true));

            // The lower header row renders one cell per unit, in order — cell
            // index 5 (Sat) and 6 (Sun) must carry the muted header treatment;
            // every weekday cell must NOT.
            var lowerCells = cut.FindAll("div.shrink-0.text-center.text-xs");
            Assert.True(lowerCells.Count >= 7, $"expected at least 7 lower-header cells, found {lowerCells.Count}");

            for (var i = 0; i < 7; i++)
            {
                var isOffDay = i == 5 || i == 6; // Sat/Sun (Mon=0)
                var cls = lowerCells[i].GetAttribute("class") ?? "";
                if (isOffDay)
                {
                    Assert.Contains("bg-muted/50", cls);
                    Assert.Contains("text-muted-foreground/70", cls);
                }
                else
                {
                    Assert.DoesNotContain("bg-muted/50", cls);
                }
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void MarkOffDays_False_Renders_No_Header_Tint_Even_On_A_Weekend()
    {
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 5))
            .Add(c => c.RangeEnd, D(2026, 1, 11))
            .Add(c => c.MarkOffDays, false));

        var lowerCells = cut.FindAll("div.shrink-0.text-center.text-xs");
        Assert.All(lowerCells, c => Assert.DoesNotContain("bg-muted/50", c.GetAttribute("class") ?? ""));
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

    // G8 (2026-08-10 shadcn alignment): a whole-column tint in Month mode is
    // exactly the "category error" this item calls out (the SAME reasoning
    // SupportsOffDayMarking already applies to off-day shading) — a Month
    // column spans ~30 days, so tinting the WHOLE thing as "today" is
    // materially imprecise. Coarse (non-day-granularity) modes now render a
    // precise 2px line at Today's own exact pixel instead; this test replaces
    // the old "whole containing column" assertion with the new precise-line
    // one. The OLD assertion (a full-column tint at the FLOORED column edge)
    // is exactly what a disable-check on IsDayGranularityMode reproduces —
    // see the T10 report.
    [Fact]
    public void TodayMarker_Renders_A_Precise_Line_Not_A_Whole_Column_Tint_In_Month_Mode()
    {
        // Month mode: a single day's DateToPixel value sits at a FRACTIONAL
        // position within its month column (Codex round 3, P2 #6's own
        // aligned-origin math) — the precise line must land at that EXACT
        // (unfloored) pixel, not the column's own floored left edge (which is
        // what the whole-column tint used to render at, and what the
        // day-granularity branch still floors to for TodayColumnLeft).
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
        var flooredColumnLeft = 2 * colW; // March = index 2 (Jan=0, Feb=1, Mar=2)

        Assert.NotEqual(flooredColumnLeft, rawTodayX); // sanity: the raw (fractional) position is NOT the column edge

        Assert.Empty(cut.FindAll(".lumeo-gantt-v3-today-tint")); // no whole-column band in a coarse mode
        var line = cut.Find(".lumeo-gantt-v3-today-line");
        Assert.Contains($"left:{rawTodayX.ToString(CultureInfo.InvariantCulture)}px", line.GetAttribute("style"));
    }

    [Fact]
    public void TodayMarker_Keeps_The_Whole_Column_Tint_In_Day_Mode()
    {
        // Day mode IS day-granularity — the T2 upgrade's whole-column tint
        // (and TodayColumnLeft == TodayX exactly, for a 1-day-wide column)
        // stays unchanged by G8; no line renders alongside it.
        var today = D(2026, 1, 15);
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 12))
            .Add(c => c.RangeEnd, D(2026, 1, 20))
            .Add(c => c.Today, today)
            .Add(c => c.TodayHighlight, true));

        Assert.Single(cut.FindAll(".lumeo-gantt-v3-today-tint"));
        Assert.Empty(cut.FindAll(".lumeo-gantt-v3-today-line"));
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
    public async Task GanttChart_Resolves_NowIndicator_Time_Via_The_Extended_Interop_Family_When_Enabled()
    {
        // Design spec Phase 3, T2 — GanttV3GetLocalDateTimeAsync extends the
        // same GanttV3Get* browser-clock family GanttV3GetLocalDateAsync
        // already established.
        //
        // CONTRACT CHANGE (Codex review of the styling-hooks PR, P2): this
        // used to additionally assert that a chart with NowIndicator=false
        // paid NO round trip at all. That cost gate was correct while the
        // visual now-line was the browser clock's ONLY consumer. It is not
        // any more: the data-past styling hook renders on EVERY bar and is
        // not gated on NowIndicator (which defaults to false), so gating the
        // resolve left data-past evaluating the SERVER clock on a Blazor
        // Server circuit — a wrong past/not-past state for any user in a
        // different time zone. The resolve is therefore unconditional now,
        // and the assertion below pins the value being available rather than
        // the call being skipped.
        _interop.GanttV3LocalDateTimeToReturn = "2026-01-15T14:30:00";

        var cutWithout = _ctx.Render<L.GanttChart>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { new("t1", "Design", D(2026, 1, 2), D(2026, 1, 6)) })
            .Add(c => c.NowIndicator, false));
        await cutWithout.InvokeAsync(() => { });
        Assert.True(_interop.GanttV3GetLocalDateTimeCallCount >= 1);

        var cutWith = _ctx.Render<L.GanttChart>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { new("t1", "Design", D(2026, 1, 2), D(2026, 1, 6)) })
            .Add(c => c.ViewMode, L.GanttViewMode.QuarterDay)
            .Add(c => c.NowIndicator, true));
        await cutWith.InvokeAsync(() => { });

        Assert.True(_interop.GanttV3GetLocalDateTimeCallCount >= 1);
    }

    // Codex review (phase-3 fix round), P2 — "browser time is stale when
    // NowIndicator is enabled after mount". Two distinct failure modes, one
    // test each; see GanttChart.razor's own _lastNowIndicator/RefreshBrowserNowAsync
    // remarks for the fix. A single-task fixture pins ComputeInitialRange's
    // padded window deterministically (QuarterDay: task min/max date +/- 6
    // days — see ApplyPadding's Hour branch), independent of GroupBy/today.

    [Fact]
    public async Task NowIndicator_Enabled_After_Mount_Resolves_Browser_Now_Not_The_Servers_Clock()
    {
        // Failure mode 1: RefreshBrowserNowAsync's own guard makes the
        // firstRender call a no-op while NowIndicator starts false, so
        // _browserNow is never seeded. Without the OnParametersSetAsync
        // false->true edge trigger, turning the option on later leaves
        // _browserNow null forever (nothing else re-queries it), and
        // GanttTimeline's `Now ?? DateTime.Now` fallback renders the SERVER's
        // real wall-clock time instead.
        //
        // Task dates are pinned to year 2000 specifically so the predicted
        // failure is unambiguous regardless of which real-world date this
        // suite happens to run on: the visible window becomes a ~13-day band
        // around 2000-01-15, and DateTime.Now (today, decades later) cannot
        // land inside it by construction — so the broken behavior is not
        // merely "the wrong pixel", it is "no now-line renders at all"
        // (NowInRange's own TotalWidth bounds check fails), while the fixed
        // behavior renders the line at the EXACT interop-provided pixel.
        var tasks = new List<L.GanttTask> { new("t1", "Design", D(2000, 1, 15), D(2000, 1, 16)) };
        _interop.GanttV3LocalDateTimeToReturn = "2000-01-15T08:00:00";

        var cut = _ctx.Render<L.GanttChart>(p => p
            .Add(c => c.Tasks, tasks)
            .Add(c => c.ViewMode, L.GanttViewMode.QuarterDay)
            .Add(c => c.NowIndicator, false));
        await cut.InvokeAsync(() => { });
        // Contract change — see the sibling test's own remarks: the browser
        // clock is resolved even with the indicator off, because data-past
        // depends on it. The line itself must still stay unrendered, which is
        // what the surrounding assertions actually prove.
        Assert.True(_interop.GanttV3GetLocalDateTimeCallCount >= 1);
        Assert.Empty(cut.FindAll(".lumeo-gantt-v3-now-line"));

        // Enable post-mount — the false->true transition this fix reacts to.
        cut.Render(p => p
            .Add(c => c.Tasks, tasks)
            .Add(c => c.ViewMode, L.GanttViewMode.QuarterDay)
            .Add(c => c.NowIndicator, true));
        await cut.InvokeAsync(() => { });

        var rangeStart = D(2000, 1, 9);
        var rangeEnd = D(2000, 1, 22);
        var origin = GanttScale.BuildDateUnits(L.GanttViewMode.QuarterDay, rangeStart, rangeEnd)[0];
        var expectedX = GanttScale.DateToPixel(L.GanttViewMode.QuarterDay, origin, new DateTime(2000, 1, 15, 8, 0, 0));

        var line = cut.Find(".lumeo-gantt-v3-now-line"); // throws if absent — exactly the broken-code prediction above
        Assert.Contains($"left:{expectedX.ToString(CultureInfo.InvariantCulture)}px", line.GetAttribute("style"));
    }

    [Fact]
    public async Task NowIndicator_Reenabled_After_Disable_Refreshes_Browser_Now_Instead_Of_Reusing_The_Stale_Value()
    {
        // Failure mode 2: toggling NowIndicator off then back on is not
        // itself a refresh trigger, so a re-enable can redisplay whatever
        // _browserNow an earlier enable left behind — a long-mounted chart
        // can show an hours-old "now" line the instant it's turned back on.
        //
        // Fully deterministic, no real-wall-clock dependency: both the FIRST
        // (T1) and SECOND (T2) browser times are supplied by the interop
        // mock, 8 hours apart on the same day — a several-hour offset that
        // makes a wrong-value failure unambiguous either way.
        var tasks = new List<L.GanttTask> { new("t1", "Design", D(2026, 1, 15), D(2026, 1, 16)) };
        var t1 = new DateTime(2026, 1, 15, 6, 0, 0);
        var t2 = new DateTime(2026, 1, 15, 14, 0, 0); // 8h after t1

        _interop.GanttV3LocalDateTimeToReturn = t1.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
        var cut = _ctx.Render<L.GanttChart>(p => p
            .Add(c => c.Tasks, tasks)
            .Add(c => c.ViewMode, L.GanttViewMode.QuarterDay)
            .Add(c => c.NowIndicator, true));
        await cut.InvokeAsync(() => { });

        var rangeStart = D(2026, 1, 9);
        var rangeEnd = D(2026, 1, 22);
        var origin = GanttScale.BuildDateUnits(L.GanttViewMode.QuarterDay, rangeStart, rangeEnd)[0];
        var expectedX1 = GanttScale.DateToPixel(L.GanttViewMode.QuarterDay, origin, t1);
        Assert.Contains($"left:{expectedX1.ToString(CultureInfo.InvariantCulture)}px", cut.Find(".lumeo-gantt-v3-now-line").GetAttribute("style"));

        // Disable — _browserNow stays at t1's value, simply unused while off.
        cut.Render(p => p
            .Add(c => c.Tasks, tasks)
            .Add(c => c.ViewMode, L.GanttViewMode.QuarterDay)
            .Add(c => c.NowIndicator, false));
        await cut.InvokeAsync(() => { });
        Assert.Empty(cut.FindAll(".lumeo-gantt-v3-now-line")); // the disable-check alone is NOT the point of this test — see below

        // The browser's clock has since moved on 8 hours (t2). Re-enable.
        _interop.GanttV3LocalDateTimeToReturn = t2.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
        cut.Render(p => p
            .Add(c => c.Tasks, tasks)
            .Add(c => c.ViewMode, L.GanttViewMode.QuarterDay)
            .Add(c => c.NowIndicator, true));
        await cut.InvokeAsync(() => { });

        var expectedX2 = GanttScale.DateToPixel(L.GanttViewMode.QuarterDay, origin, t2);
        Assert.NotEqual(expectedX1, expectedX2); // sanity: the two candidate positions are genuinely distinct

        // Concretely predicted: fixed code re-queries on re-enable and renders
        // at t2's pixel; broken code never re-queries on re-enable and keeps
        // rendering at t1's (stale) pixel instead.
        var line = cut.Find(".lumeo-gantt-v3-now-line");
        Assert.Contains($"left:{expectedX2.ToString(CultureInfo.InvariantCulture)}px", line.GetAttribute("style"));
    }
}
