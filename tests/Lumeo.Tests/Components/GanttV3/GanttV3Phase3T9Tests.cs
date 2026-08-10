using Bunit;
using Lumeo.GanttV3;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.GanttV3;

/// <summary>
/// Gantt v3 Phase 3, T9 — horizontal infinite scroll (<c>Gantt3.InfiniteScroll</c>/
/// <c>GanttTimeline.InfiniteScroll</c>/<c>OnRangeExtensionRequested</c>) plus the
/// <see cref="L.GanttSettingsMenu"/> toggle T8 reserved for it. Component-level
/// integration coverage — see <c>GanttV3InfiniteScrollTests</c> (E2E) for real-browser
/// proof: a genuine native 'scroll' event driving the rAF-throttled report, the ±1px
/// bar-stability assertion, and the RTL/parity specs bUnit cannot exercise.
/// </summary>
public class GanttV3Phase3T9Tests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public GanttV3Phase3T9Tests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static DateTime D(int y, int m, int d) => new(y, m, d);

    private static List<L.GanttTask> Fixture() => new()
    {
        new("t1", "Task", D(2026, 3, 1), D(2026, 3, 4)),
    };

    // ── GanttSettingsMenu: the T9 wiring point (design spec Phase 3, T8/T9) ────

    private static AngleSharp.Dom.IElement FindCheckboxByLabel(IRenderedComponent<L.GanttSettingsMenu> cut, string labelText)
    {
        var label = cut.FindAll("label").Single(l => l.TextContent.Trim() == labelText);
        var forId = label.GetAttribute("for");
        return cut.Find($"#{forId}");
    }

    // DISABLE-CHECK target: if EffectiveInfiniteScroll ever stopped consulting
    // InfiniteScrollChanged.HasDelegate and just read the raw parameter
    // unconditionally, an UNCONTROLLED menu would still show this correctly
    // (both read the same true default) — the REAL regression this guards is
    // the sibling "uncontrolled toggle click never flips visually" test below,
    // same pairing T8's own ShowOffscreenIndicators tests use.
    [Fact]
    public void InfiniteScroll_Uncontrolled_Default_Matches_Gantt3s_Own_True_Default()
    {
        var cut = _ctx.Render<L.GanttSettingsMenu>();
        cut.Find("button[aria-label='Settings']").Click();

        Assert.Equal("true", FindCheckboxByLabel(cut, "Infinite scroll").GetAttribute("aria-checked"));
    }

    [Fact]
    public async Task Uncontrolled_InfiniteScroll_Checkbox_Toggle_Flips_And_Stays_Flipped_Locally()
    {
        var cut = _ctx.Render<L.GanttSettingsMenu>();
        cut.Find("button[aria-label='Settings']").Click();

        await cut.InvokeAsync(() => FindCheckboxByLabel(cut, "Infinite scroll").Click());

        Assert.Equal("false", FindCheckboxByLabel(cut, "Infinite scroll").GetAttribute("aria-checked"));
    }

    [Fact]
    public async Task Controlled_InfiniteScroll_Checkbox_Click_Notifies_Changed_With_The_New_Value()
    {
        bool? notified = null;
        var cut = _ctx.Render<L.GanttSettingsMenu>(p => p
            .Add(c => c.InfiniteScroll, true)
            .Add(c => c.InfiniteScrollChanged, (bool v) => notified = v));
        cut.Find("button[aria-label='Settings']").Click();

        await cut.InvokeAsync(() => FindCheckboxByLabel(cut, "Infinite scroll").Click());

        Assert.False(notified);
    }

    [Fact]
    public async Task Reset_Restores_A_Controlled_InfiniteScroll_Own_MountTime_Value_Not_The_Librarys_Hardcoded_Default()
    {
        // Mirrors GanttV3Phase3T8Tests' identical ShowSummaryBars disable-check
        // pattern (decision 2): a consumer's app default (false here, the
        // OPPOSITE of the library's own true default) must survive Reset.
        var current = false;
        var cut = _ctx.Render<L.GanttSettingsMenu>(p => p
            .Add(c => c.InfiniteScroll, current)
            .Add(c => c.InfiniteScrollChanged, (bool v) => current = v));
        cut.Find("button[aria-label='Settings']").Click();
        Assert.Equal("false", FindCheckboxByLabel(cut, "Infinite scroll").GetAttribute("aria-checked"));

        await cut.InvokeAsync(() => FindCheckboxByLabel(cut, "Infinite scroll").Click());
        cut.Render(p => p
            .Add(c => c.InfiniteScroll, current)
            .Add(c => c.InfiniteScrollChanged, (bool v) => current = v));
        Assert.Equal("true", FindCheckboxByLabel(cut, "Infinite scroll").GetAttribute("aria-checked"));

        await cut.InvokeAsync(() => cut.Find("[data-testid='gantt-settings-reset']").Click());
        cut.Render(p => p
            .Add(c => c.InfiniteScroll, current)
            .Add(c => c.InfiniteScrollChanged, (bool v) => current = v));

        Assert.Equal("false", FindCheckboxByLabel(cut, "Infinite scroll").GetAttribute("aria-checked"));
    }

    // ── Gantt3/GanttTimeline: the extension pipeline itself ─────────────────

    // Every Gantt3 instance performs its OWN scroll-to-today/center attempt on
    // mount (ScrollToTodayRequestId starts pending — see GanttTimeline's own
    // remarks) — in a REAL browser, that recenter's resulting native 'scroll'
    // event is what ScrollToCenterAsync's own _suppressNextExtensionReport
    // flag is FOR (see its remarks): the settling report from a DELIBERATE
    // recenter must never itself read as "the user wants more range". bUnit
    // never runs the real gantt-v3.js, so nothing ever generates that
    // settling report on its own — flushing it HERE with an explicitly
    // SAFE (nowhere near either edge) position mirrors what a real browser
    // would have already produced by the time a test's own RaiseGanttV3VerticalScroll
    // call runs, so every test below observes the SAME "mount has already
    // settled, now the user scrolls" sequence a real page does.
    private async Task<IRenderedComponent<L.Gantt3>> RenderChart(bool? infiniteScroll = null)
    {
        var cut = _ctx.Render<L.Gantt3>(p =>
        {
            p.Add(c => c.Tasks, Fixture());
            p.Add(c => c.ViewMode, L.GanttViewMode.Day);
            p.Add(c => c.ShowTreePane, false); // zeroes ScrollHostLeadingOffset — see GanttViewportGeometry.LeadingOffset
            if (infiniteScroll is { } v) p.Add(c => c.InfiniteScroll, v);
        });

        var timeline = cut.FindComponent<L.GanttTimeline>();
        var cfg = GanttScale.GetConfig(L.GanttViewMode.Day);
        var totalWidth = GanttScale.BuildDateUnits(L.GanttViewMode.Day, timeline.Instance.RangeStart, timeline.Instance.RangeEnd).Count * (double)cfg.ColumnWidth;
        await cut.InvokeAsync(() => _interop.RaiseGanttV3VerticalScroll(scrollTop: 0, clientHeight: 200, scrollLeft: totalWidth / 2, clientWidth: 300));

        return cut;
    }

    // Design spec Phase 3, T9, decision 1: default true is proven implicitly
    // by every test below that never sets InfiniteScroll at all and still
    // observes an extension — this is that proof's first instance.
    [Fact]
    public async Task Scrolling_Near_The_Leading_Edge_Extends_VisibleRange_Backward_By_Exactly_One_Page_And_Recenters()
    {
        var cut = await RenderChart();
        var timeline = cut.FindComponent<L.GanttTimeline>();
        var oldStart = timeline.Instance.RangeStart;
        var oldEnd = timeline.Instance.RangeEnd;
        // Gantt3.VisibleCenterDate's own formula — ResolveCurrentCenterDateAsync's
        // fallback when GanttV3GetScrollCenterXAsync reports null (the
        // TrackingInteropService default), which every test in this file relies
        // on rather than wiring a live scroll-center double.
        var oldCenter = oldStart + new TimeSpan((oldEnd - oldStart).Ticks / 2);

        var scrollCallsBefore = _interop.GanttV3ScrollToXCallCount;
        // scrollLeft:0, ShowTreePane:false -> VisibleTimelineWindow.Start == 0,
        // which is < any positive clientWidth -> ALWAYS "near the leading edge"
        // regardless of the fixture's own computed range width.
        await cut.InvokeAsync(() => _interop.RaiseGanttV3VerticalScroll(scrollTop: 0, clientHeight: 200, scrollLeft: 0, clientWidth: 1000));

        var cfg = GanttScale.GetConfig(L.GanttViewMode.Day);
        var expectedNewStart = oldStart.AddDays(-cfg.PadBefore * cfg.Step); // "one page" == the SAME PadBefore magnitude ApplyPadding's own initial window already used
        Assert.Equal(expectedNewStart, timeline.Instance.RangeStart);
        Assert.Equal(oldEnd, timeline.Instance.RangeEnd); // trailing boundary untouched by a leading extension

        // Recenter fired, and lands EXACTLY where the FALLBACK center date
        // (TrackingInteropService's own default null GanttV3ScrollCenterXToReturn
        // — see GanttTimeline.ScrollTargetXOverride's own remarks: no live
        // pixel reading means this exercises HandleRangeExtensionRequestAsync's
        // OWN date-based fallback branch, not its normal pixel-precise one)
        // resolves under the NEW origin. See
        // A_Leading_Extension_Recenters_Pixel_Exactly_When_A_Live_Scroll_Center_Is_Available
        // below for the NORMAL (pixel-precise, not date-fallback) path this
        // test does not exercise.
        Assert.Equal(scrollCallsBefore + 1, _interop.GanttV3ScrollToXCallCount);
        var expectedTargetX = GanttScale.DateToPixel(L.GanttViewMode.Day, expectedNewStart, oldCenter, cfg.ColumnWidth);
        Assert.Equal(expectedTargetX, _interop.GanttV3ScrollToXCalls[^1], precision: 6);
    }

    // The design spec's own explicit proof requirement ("the ±1px E2E
    // assertion is the proof — make it real"): this is that proof's unit-level
    // counterpart, isolating the EXACT pixel-translation math
    // HandleRangeExtensionRequestAsync's leading branch performs when a live
    // scroll-center IS available (GanttV3InfiniteScrollTests, E2E, proves the
    // SAME math survives real CSS/layout rounding to within ±1px — this test
    // proves it is EXACT, not merely close, at the arithmetic level).
    [Fact]
    public async Task A_Leading_Extension_Recenters_Pixel_Exactly_When_A_Live_Scroll_Center_Is_Available()
    {
        var cut = await RenderChart();
        var timeline = cut.FindComponent<L.GanttTimeline>();
        var oldStart = timeline.Instance.RangeStart;
        var cfg = GanttScale.GetConfig(L.GanttViewMode.Day);

        // A deliberately ARBITRARY (not column-aligned) live logical center —
        // proves the pixel path preserves SUB-COLUMN precision the date-based
        // fallback (see the sibling test above) provably cannot (GanttScale.DateToPixel's
        // Day-mode branch truncates to whole days).
        const double liveLogicalCenterX = 1234.75;
        _interop.GanttV3ScrollCenterXToReturn = liveLogicalCenterX;

        var scrollCallsBefore = _interop.GanttV3ScrollToXCallCount;
        await cut.InvokeAsync(() => _interop.RaiseGanttV3VerticalScroll(scrollTop: 0, clientHeight: 200, scrollLeft: 0, clientWidth: 1000));

        var expectedNewStart = oldStart.AddDays(-cfg.PadBefore * cfg.Step);
        Assert.Equal(expectedNewStart, timeline.Instance.RangeStart);

        // Exactly the live center, shifted by the EXACT origin delta — no
        // DateTime intermediary, no truncation. ScrollHostLeadingOffset is 0
        // here (ShowTreePane:false — see RenderChart's own remarks), so the
        // interop call's own raw argument equals this timeline-relative value
        // directly.
        var addedPixels = GanttScale.DateToPixel(L.GanttViewMode.Day, expectedNewStart, oldStart, cfg.ColumnWidth);
        var expectedTargetX = liveLogicalCenterX + addedPixels;
        Assert.Equal(scrollCallsBefore + 1, _interop.GanttV3ScrollToXCallCount);
        Assert.Equal(expectedTargetX, _interop.GanttV3ScrollToXCalls[^1], precision: 9);
    }

    [Fact]
    public async Task Scrolling_Near_The_Trailing_Edge_Extends_VisibleRange_Forward_Without_Recentering()
    {
        var cut = await RenderChart();
        var timeline = cut.FindComponent<L.GanttTimeline>();
        var oldStart = timeline.Instance.RangeStart;
        var oldEnd = timeline.Instance.RangeEnd;

        var cfg = GanttScale.GetConfig(L.GanttViewMode.Day);
        var totalWidth = GanttScale.BuildDateUnits(L.GanttViewMode.Day, oldStart, oldEnd).Count * (double)cfg.ColumnWidth;
        const double clientWidth = 300;
        // Positions the viewport's own END edge 10px short of the total
        // rendered width -> "near the trailing edge" (< clientWidth away),
        // regardless of the fixture's own exact total width.
        var scrollLeft = totalWidth - clientWidth - 10;

        var scrollCallsBefore = _interop.GanttV3ScrollToXCallCount;
        await cut.InvokeAsync(() => _interop.RaiseGanttV3VerticalScroll(scrollTop: 0, clientHeight: 200, scrollLeft: scrollLeft, clientWidth: clientWidth));

        var expectedNewEnd = oldEnd.AddDays(cfg.PadAfter * cfg.Step);
        Assert.Equal(oldStart, timeline.Instance.RangeStart); // leading boundary + origin untouched
        Assert.Equal(expectedNewEnd, timeline.Instance.RangeEnd);

        // No recenter — a trailing extension never shifts the origin (see
        // HandleRangeExtensionRequestAsync's own remarks), so the DOM's
        // current scroll position stays exactly correct as-is.
        Assert.Equal(scrollCallsBefore, _interop.GanttV3ScrollToXCallCount);
    }

    // REGRESSION test for a REAL bug found via the local E2E gate (design
    // spec Phase 3, T9) — see ScrollToCenterAsync's own remarks in
    // GanttTimeline.razor for the full root-cause: GanttV3NavTests' Today/
    // Next-Previous specs shifted VisibleRange.Start by an EXTRA Month-mode
    // PadBefore (12 months) immediately after the very recenter they were
    // asserting on, because a deliberate recenter's OWN resulting native
    // 'scroll' event can legitimately land the viewport within one page of
    // VisibleRange's edge whenever the range isn't drastically wider than
    // the viewport. Reproduces the SAME shape here: click the REAL "Today"
    // button (Gantt3's own GoToTodayAsync, exactly what a live NavTests spec
    // drives), then simulate the settling report a real browser's native
    // 'scroll' event would produce for that recenter — landing intentionally
    // at logical 0 (guaranteed "near leading edge" per every other test in
    // this file) — and confirm it does NOT ALSO trigger an extension on top
    // of the recenter.
    [Fact]
    public async Task A_Todays_Own_Recenter_Settling_Report_Never_Also_Triggers_An_Extension()
    {
        var cut = await RenderChart();
        var timeline = cut.FindComponent<L.GanttTimeline>();

        await cut.InvokeAsync(() => cut.FindAll("button").First(b => b.TextContent.Trim() == "Today").Click());
        var rangeAfterToday = (timeline.Instance.RangeStart, timeline.Instance.RangeEnd);

        // The settling report a real browser would produce immediately after
        // Today's own centerOn call — see this test's own remarks.
        await cut.InvokeAsync(() => _interop.RaiseGanttV3VerticalScroll(scrollTop: 0, clientHeight: 200, scrollLeft: 0, clientWidth: 1000));

        Assert.Equal(rangeAfterToday, (timeline.Instance.RangeStart, timeline.Instance.RangeEnd));
    }

    [Fact]
    public async Task Scrolling_Well_Within_The_Range_Never_Triggers_An_Extension()
    {
        var cut = await RenderChart();
        var timeline = cut.FindComponent<L.GanttTimeline>();
        var oldStart = timeline.Instance.RangeStart;
        var oldEnd = timeline.Instance.RangeEnd;
        var cfg = GanttScale.GetConfig(L.GanttViewMode.Day);
        var totalWidth = GanttScale.BuildDateUnits(L.GanttViewMode.Day, oldStart, oldEnd).Count * (double)cfg.ColumnWidth;

        await cut.InvokeAsync(() => _interop.RaiseGanttV3VerticalScroll(scrollTop: 0, clientHeight: 200, scrollLeft: totalWidth / 2, clientWidth: 300));

        Assert.Equal(oldStart, timeline.Instance.RangeStart);
        Assert.Equal(oldEnd, timeline.Instance.RangeEnd);
    }

    // DISABLE-CHECK target (design spec Phase 3, T9, decision 1's own parity
    // half): InfiniteScroll=false must byte-match v2's fixed-padding behavior
    // — VisibleRange never grows, no matter how close to either edge the
    // reported scroll position gets. See GanttV3InfiniteScrollTests (E2E) for
    // the actual v2-vs-v3 route comparison this unit-level test complements.
    //
    // A first version of this test asserted ONLY RangeStart/RangeEnd/
    // GanttV3ScrollToXCallCount and PASSED even with GanttTimeline's own
    // `InfiniteScroll &&` gate disabled — a blind disable-check (rigor
    // standard: "if a disable-check passes, your fixture is wrong"), because
    // Gantt3.HandleRangeExtensionRequestAsync's OWN independent `!InfiniteScroll`
    // check (defense-in-depth, same shape as decision 3's two-layer drag gate)
    // still caught it. Rebuilt to additionally assert
    // GanttV3HasActiveDragCallCount == 0: GanttTimeline's own gate is what
    // stops TryRequestRangeExtensionAsync from EVER starting at all (never
    // even reaching its own drag check) — a signal ONLY that layer, not
    // Gantt3's later one, can produce.
    [Fact]
    public async Task InfiniteScroll_False_Never_Extends_Even_Scrolled_To_The_Leading_Edge()
    {
        var cut = await RenderChart(infiniteScroll: false);
        var timeline = cut.FindComponent<L.GanttTimeline>();
        var oldStart = timeline.Instance.RangeStart;
        var oldEnd = timeline.Instance.RangeEnd;
        var scrollCallsBefore = _interop.GanttV3ScrollToXCallCount;

        await cut.InvokeAsync(() => _interop.RaiseGanttV3VerticalScroll(scrollTop: 0, clientHeight: 200, scrollLeft: 0, clientWidth: 1000));

        Assert.Equal(oldStart, timeline.Instance.RangeStart);
        Assert.Equal(oldEnd, timeline.Instance.RangeEnd);
        Assert.Equal(scrollCallsBefore, _interop.GanttV3ScrollToXCallCount);
        Assert.Equal(0, _interop.GanttV3HasActiveDragCallCount);
    }

    // ── Decision 4 — unbounded-growth hard cap ───────────────────────────────

    // DISABLE-CHECK target (design spec Phase 3, T9, decision 4 — see
    // GanttScale.MaxInfiniteScrollRangeUnits' own remarks for the full "hard
    // cap, what breaks first" reasoning): a single wide task's own min/max
    // dates seed ComputeInitialRange/ApplyPadding's own padded window at
    // EXACTLY the cap (both are pure calendar-date math, computed the same
    // way here as the production code computes it, not a hand-picked magic
    // number) — proving the boundary precisely, not just "somewhere well
    // past it". A further leading extension would exceed the cap by
    // construction (its own PadBefore magnitude alone pushes it over) and
    // must be a complete no-op: no VisibleRange change, no recenter attempt
    // (Predicted, if the cap guard is removed: RangeStart moves to
    // oldStart.AddDays(-60) exactly like every other successful leading
    // extension in this file — manually verified by commenting out the guard
    // during this task's own gate).
    [Fact]
    public async Task A_Leading_Extension_That_Would_Exceed_The_Unit_Cap_Is_A_No_Op()
    {
        var cfg = GanttScale.GetConfig(L.GanttViewMode.Day);
        var start = D(2020, 1, 1);
        // N chosen so the padded initial range's own unit count lands EXACTLY
        // at the cap: totalDays = N + 1 (task span, inclusive) + PadBefore +
        // PadAfter == MaxInfiniteScrollRangeUnits (see BuildDateUnits' own Day
        // branch: totalDays = (rangeEnd-rangeStart).Days + 1).
        var spanDays = GanttScale.MaxInfiniteScrollRangeUnits - 1 - cfg.PadBefore - cfg.PadAfter;
        var end = start.AddDays(spanDays);
        var tasks = new List<L.GanttTask> { new("wide", "Wide", start, end) };

        var cut = _ctx.Render<L.Gantt3>(p =>
        {
            p.Add(c => c.Tasks, tasks);
            p.Add(c => c.ViewMode, L.GanttViewMode.Day);
            p.Add(c => c.ShowTreePane, false);
        });
        var timeline = cut.FindComponent<L.GanttTimeline>();

        // Flush the mount's own settling report from a safe (mid-range)
        // position — see RenderChart's own remarks above for why this is
        // needed before any test-driven report can be trusted.
        var totalWidthAtMount = GanttScale.BuildDateUnits(L.GanttViewMode.Day, timeline.Instance.RangeStart, timeline.Instance.RangeEnd).Count * (double)cfg.ColumnWidth;
        await cut.InvokeAsync(() => _interop.RaiseGanttV3VerticalScroll(scrollTop: 0, clientHeight: 200, scrollLeft: totalWidthAtMount / 2, clientWidth: 300));

        var oldStart = timeline.Instance.RangeStart;
        var oldEnd = timeline.Instance.RangeEnd;
        var initialCount = GanttScale.BuildDateUnits(L.GanttViewMode.Day, oldStart, oldEnd).Count;
        // Fixture-setup invariants — if either fails, the fixture itself
        // (not the production code) is wrong; see the class's own rigor-
        // standard remarks on why a disable-check needs a fixture that can
        // actually observe the guarded behavior.
        Assert.Equal(GanttScale.MaxInfiniteScrollRangeUnits, initialCount);
        Assert.True(initialCount + cfg.PadBefore * cfg.Step > GanttScale.MaxInfiniteScrollRangeUnits);

        var scrollCallsBefore = _interop.GanttV3ScrollToXCallCount;
        await cut.InvokeAsync(() => _interop.RaiseGanttV3VerticalScroll(scrollTop: 0, clientHeight: 200, scrollLeft: 0, clientWidth: 1000));

        Assert.Equal(oldStart, timeline.Instance.RangeStart);
        Assert.Equal(oldEnd, timeline.Instance.RangeEnd);
        Assert.Equal(scrollCallsBefore, _interop.GanttV3ScrollToXCallCount); // nothing committed -> no recenter attempt either
    }

    // ── Decision 3 — gesture suppression ─────────────────────────────────────

    // DISABLE-CHECK target: if GanttTimeline.TryRequestRangeExtensionAsync's own
    // GanttV3HasActiveDragAsync gate were removed, RangeStart would still stay
    // put — Gantt3's OWN independent re-check (decision 3's second layer,
    // exercised separately by A_Drag_That_Starts_During_The_Live_Center_Capture_Aborts_The_Pending_Extension
    // below) would still catch it, defense-in-depth. A first version of this
    // test asserted ONLY RangeStart and PASSED even with GanttTimeline's own
    // gate disabled — a blind disable-check per the rigor standard ("if a
    // disable-check passes, your fixture is wrong"). Rebuilt to additionally
    // assert GanttV3GetScrollCenterXCallCount == 0: GanttTimeline's cheap gate
    // is specifically what stops Gantt3 from EVER attempting its own
    // live-center capture in the first place (see TryRequestRangeExtensionAsync's
    // own remarks — "avoids even asking Gantt3") — a signal ONLY the cheap
    // gate, not Gantt3's later re-check, can produce, since by the time
    // Gantt3's re-check runs the capture has ALREADY happened.
    [Fact]
    public async Task A_Report_While_A_Drag_Is_Active_Never_Extends()
    {
        var cut = await RenderChart();
        var timeline = cut.FindComponent<L.GanttTimeline>();
        var oldStart = timeline.Instance.RangeStart;
        _interop.GanttV3HasActiveDragToReturn = true;

        await cut.InvokeAsync(() => _interop.RaiseGanttV3VerticalScroll(scrollTop: 0, clientHeight: 200, scrollLeft: 0, clientWidth: 1000));

        Assert.Equal(oldStart, timeline.Instance.RangeStart);
        // The cheap gate short-circuits BEFORE ever reaching Gantt3's own
        // EventCallback — proves this is GanttTimeline's own check firing,
        // not some other unrelated no-op.
        Assert.Equal(1, _interop.GanttV3HasActiveDragCallCount);
        // The precise signal that isolates THIS layer from Gantt3's own
        // re-check — see this test's own remarks.
        Assert.Equal(0, _interop.GanttV3GetScrollCenterXCallCount);
    }

    // The race the plan explicitly calls out: a drag starts DURING the one
    // await (ResolveCurrentCenterDateAsync's live-scroll-center read) that
    // sits between GanttTimeline's own (already-passed, cheap) gate and
    // Gantt3's actual commit. Gantt3's OWN re-check (HandleRangeExtensionRequestAsync's
    // second GanttV3HasActiveDragAsync call, AFTER the capture resolves) must
    // catch it and abandon — proving decision 3's "suppress while a drag is in
    // flight" is sufficient even across this specific timing gap, not just the
    // common case A_Report_While_A_Drag_Is_Active_Never_Extends above covers.
    [Fact]
    public async Task A_Drag_That_Starts_During_The_Live_Center_Capture_Aborts_The_Pending_Extension()
    {
        var cut = await RenderChart();
        var timeline = cut.FindComponent<L.GanttTimeline>();
        var oldStart = timeline.Instance.RangeStart;

        var gate = new TaskCompletionSource<double?>();
        _interop.GanttV3ScrollCenterXGate = gate;

        // Fires the extension request; GanttTimeline's own (first) drag check
        // passes (false), Gantt3.HandleRangeExtensionRequestAsync starts its
        // live-center capture and suspends on the gate — synchronously, up to
        // that exact point, within this single InvokeAsync call (every OTHER
        // interop call involved resolves immediately, so nothing else yields
        // first).
        await cut.InvokeAsync(() => _interop.RaiseGanttV3VerticalScroll(scrollTop: 0, clientHeight: 200, scrollLeft: 0, clientWidth: 1000));
        Assert.Equal(oldStart, timeline.Instance.RangeStart); // nothing committed yet — still mid-capture

        // A gesture starts NOW, while the capture above is still in flight.
        _interop.GanttV3HasActiveDragToReturn = true;

        // Resume the capture — Gantt3's own re-check (immediately after) now
        // observes the drag and abandons instead of committing.
        await cut.InvokeAsync(() => gate.SetResult(0));

        Assert.Equal(oldStart, timeline.Instance.RangeStart);
        Assert.Equal(2, _interop.GanttV3HasActiveDragCallCount); // GanttTimeline's initial check + Gantt3's own re-check
    }

    // GanttTimeline's OWN re-entrancy guard (_rangeExtensionInFlight): a
    // SECOND near-edge report arriving while the FIRST request is still deep
    // in Gantt3's own async pipeline (not merely GanttTimeline's own cheap
    // gate — see the class remarks on where _rangeExtensionInFlight is
    // actually set) must be dropped before it ever re-checks
    // GanttV3HasActiveDragAsync a second time, and the extension must land
    // EXACTLY once, not twice, once the first request resolves.
    [Fact]
    public async Task A_Second_Overlapping_Report_Is_Dropped_While_The_First_Extension_Is_Still_In_Flight()
    {
        var cut = await RenderChart();
        var timeline = cut.FindComponent<L.GanttTimeline>();
        var oldStart = timeline.Instance.RangeStart;
        var oldEnd = timeline.Instance.RangeEnd;

        var gate = new TaskCompletionSource<double?>();
        _interop.GanttV3ScrollCenterXGate = gate;

        // First report: suspends inside Gantt3's OWN pipeline (past
        // GanttTimeline's _rangeExtensionInFlight = true — see
        // TryRequestRangeExtensionAsync's own remarks), so the guard is
        // ACTUALLY armed for the second call below (unlike gating on
        // GanttV3HasActiveDragAsync itself, which resolves BEFORE the flag is
        // set and so would not exercise this guard at all).
        await cut.InvokeAsync(() => _interop.RaiseGanttV3VerticalScroll(scrollTop: 0, clientHeight: 200, scrollLeft: 0, clientWidth: 1000));
        var dragCallsAfterFirst = _interop.GanttV3HasActiveDragCallCount;
        Assert.Equal(1, dragCallsAfterFirst);

        // Second, overlapping report — same near-leading-edge condition.
        await cut.InvokeAsync(() => _interop.RaiseGanttV3VerticalScroll(scrollTop: 0, clientHeight: 200, scrollLeft: 0, clientWidth: 1000));

        // Dropped before ever reaching the drag check a second time.
        Assert.Equal(dragCallsAfterFirst, _interop.GanttV3HasActiveDragCallCount);

        var cfg = GanttScale.GetConfig(L.GanttViewMode.Day);
        var expectedNewStart = oldStart.AddDays(-cfg.PadBefore * cfg.Step);

        await cut.InvokeAsync(() => gate.SetResult(0));

        // Extended EXACTLY once, not twice (would be oldStart.AddDays(-120) if
        // the dropped second call had also landed).
        Assert.Equal(expectedNewStart, timeline.Instance.RangeStart);
        Assert.Equal(oldEnd, timeline.Instance.RangeEnd);
    }

    // Bug fix (Codex review, P2 #6): TryRequestRangeExtensionAsync's own
    // `_rangeExtensionInFlight` guard used to be checked BEFORE
    // `await Interop.GanttV3HasActiveDragAsync()` but only SET true AFTER
    // that await resolved — so two reports arriving while that FIRST await
    // was still in flight both passed the (still-false) guard and both
    // proceeded, firing the range extension TWICE for a single edge
    // encounter (confirmed by temporarily reverting the fix and rerunning
    // this spec: it extended twice, landing one WHOLE extra page short of
    // `expectedNewStart` below). TrackingInteropService's own
    // GanttV3HasActiveDragGate exists specifically to suspend HERE (its doc
    // comment already describes proving this exact guard) but was never
    // actually wired into a test until now.
    [Fact]
    public async Task Two_Reports_Racing_The_HasActiveDrag_Check_Still_Extend_Only_Once()
    {
        var cut = await RenderChart();
        var timeline = cut.FindComponent<L.GanttTimeline>();
        var oldStart = timeline.Instance.RangeStart;
        var oldEnd = timeline.Instance.RangeEnd;

        var dragGate = new TaskCompletionSource<bool>();
        _interop.GanttV3HasActiveDragGate = dragGate;

        // Two near-leading-edge reports. Post-fix, the SECOND is dropped
        // immediately by the (now correctly pre-armed) _rangeExtensionInFlight
        // guard — it never even reaches GanttV3HasActiveDragAsync.
        await cut.InvokeAsync(() => _interop.RaiseGanttV3VerticalScroll(scrollTop: 0, clientHeight: 200, scrollLeft: 0, clientWidth: 1000));
        await cut.InvokeAsync(() => _interop.RaiseGanttV3VerticalScroll(scrollTop: 0, clientHeight: 200, scrollLeft: 0, clientWidth: 1000));

        Assert.Equal(1, _interop.GanttV3HasActiveDragCallCount); // only the FIRST call ever reached the check

        await cut.InvokeAsync(() => dragGate.SetResult(false));
        for (var i = 0; i < 20; i++) await Task.Yield();
        await Task.Delay(50);

        var cfg = GanttScale.GetConfig(L.GanttViewMode.Day);
        var expectedNewStart = oldStart.AddDays(-cfg.PadBefore * cfg.Step); // ONE page's worth — the correct, single-extension outcome

        Assert.Equal(expectedNewStart, timeline.Instance.RangeStart);
        Assert.Equal(oldEnd, timeline.Instance.RangeEnd);
    }

    // Bug fix (Codex review, P2 #6), TRAILING direction: HandleRangeExtensionRequestAsync's
    // trailing (`leading: false`) branch has NO await at all inside it — it
    // synchronously reads VisibleRange.End and commits — so it has none of
    // the leading branch's own "capture before the await, recheck after"
    // staleness protection (Decision 3's re-check) to incidentally catch a
    // second overlapping request. GanttTimeline's own _rangeExtensionInFlight
    // guard was the ONLY thing standing between two overlapping trailing
    // reports and a genuine double-extension — and it was timing-broken the
    // same way the leading-direction test above describes.
    [Fact]
    public async Task Two_Trailing_Reports_Racing_The_HasActiveDrag_Check_Extend_Only_Once()
    {
        var cut = await RenderChart();
        var timeline = cut.FindComponent<L.GanttTimeline>();
        var oldStart = timeline.Instance.RangeStart;
        var oldEnd = timeline.Instance.RangeEnd;

        var cfg = GanttScale.GetConfig(L.GanttViewMode.Day);
        var totalWidth = GanttScale.BuildDateUnits(L.GanttViewMode.Day, oldStart, oldEnd).Count * (double)cfg.ColumnWidth;
        const double clientWidth = 300;
        var scrollLeft = totalWidth - clientWidth - 10; // "near the trailing edge" — mirrors Scrolling_Near_The_Trailing_Edge_... above

        var dragGate = new TaskCompletionSource<bool>();
        _interop.GanttV3HasActiveDragGate = dragGate;

        await cut.InvokeAsync(() => _interop.RaiseGanttV3VerticalScroll(scrollTop: 0, clientHeight: 200, scrollLeft: scrollLeft, clientWidth: clientWidth));
        await cut.InvokeAsync(() => _interop.RaiseGanttV3VerticalScroll(scrollTop: 0, clientHeight: 200, scrollLeft: scrollLeft, clientWidth: clientWidth));

        Assert.Equal(1, _interop.GanttV3HasActiveDragCallCount); // only the FIRST call ever reached the check

        await cut.InvokeAsync(() => dragGate.SetResult(false));
        // Let any continuation still queued behind the first actually run
        // before asserting — a fire-and-forget task's continuation isn't
        // guaranteed to have completed the instant SetResult's own
        // InvokeAsync returns.
        for (var i = 0; i < 20; i++) await Task.Yield();
        await Task.Delay(50);

        var expectedNewEnd = oldEnd.AddDays(cfg.PadAfter * cfg.Step); // ONE page's worth — the correct, single-extension outcome

        // Confirmed by temporarily reverting the fix and rerunning this
        // spec: pre-fix, both overlapping calls landed unconditionally (the
        // trailing branch never re-validates staleness), extending TWICE
        // for one edge encounter.
        Assert.Equal(oldStart, timeline.Instance.RangeStart);
        Assert.Equal(expectedNewEnd, timeline.Instance.RangeEnd);
    }
}
