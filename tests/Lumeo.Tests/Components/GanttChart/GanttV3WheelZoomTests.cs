using System.Collections.Generic;
using Bunit;
using Lumeo.GanttV3;
using Lumeo.Services;
using Microsoft.Extensions.DependencyInjection;
using Lumeo.Tests.Helpers;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.GanttV3;

/// <summary>
/// Wheel zoom — GanttV3's own previously entirely-missing feature: zero
/// wheel-related code existed anywhere in gantt-v3.js before this. Covers
/// the C#-side half bUnit's headless DOM CAN exercise without a real
/// browser 'wheel' event: the registration channel (options pushed to JS,
/// gated on <see cref="L.GanttTimeline.WheelZoom"/> exactly like Readonly
/// gates the drag channel), <c>CommitWheelZoom</c>'s anchor-date math and
/// defensive guards, and — at the <see cref="L.GanttChart"/> level — proof
/// that a wheel-zoom recenter actually uses the POINTER-anchored
/// <c>GanttV3ScrollToOffsetAsync</c> path, never the viewport-CENTERED
/// <c>GanttV3ScrollToXAsync</c> path every other <c>ViewMode</c> trigger
/// (the toolbar included) still uses unchanged.
///
/// The actual 'wheel' event listener + its synchronous zoom-limit decision
/// live entirely in gantt-v3.js and are NOT exercised here (no real DOM
/// wheel event in bUnit) — that is Playwright E2E territory.
/// </summary>
public class GanttV3WheelZoomTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public GanttV3WheelZoomTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static DateTime D(int y, int m, int d) => new(y, m, d);

    private static List<L.GanttTask> Fixture() => new()
    {
        new("t1", "Design", D(2026, 1, 2), D(2026, 1, 6)),
    };

    // ── Registration channel ────────────────────────────────────────────

    [Fact]
    public void WheelZoom_Defaults_True_And_Registers_With_The_Default_Resolved_Levels()
    {
        _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10)));

        Assert.Equal(1, _interop.GanttV3RegisterWheelZoomCallCount);
        var options = Assert.IsType<Dictionary<string, object?>>(_interop.LastGanttV3WheelZoomOptions);
        Assert.Equal("Day", options["currentMode"]);
        // GanttZoomLevelModel.DefaultLevels — the SAME set GanttNav's toolbar
        // and GanttZoomControl's stepper resolve to with no ZoomLevels
        // override (Quarter deliberately excluded — see that type's remarks).
        Assert.Equal(
            new[] { "Day", "Week", "Month", "Year" },
            Assert.IsType<string[]>(options["levels"]));
    }

    [Fact]
    public void WheelZoom_False_Registers_No_Interop_Channel_At_All()
    {
        // Mirrors Readonly's own "no listener at all, not merely a no-op
        // listener" contract for the drag channel. Predicted wrong value if
        // this regressed to a JS-side-only gate: GanttV3RegisterWheelZoomCallCount
        // would be 1 instead of 0.
        _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.WheelZoom, false));

        Assert.Equal(0, _interop.GanttV3RegisterWheelZoomCallCount);
    }

    [Fact]
    public void WheelZoom_Runtime_Flip_To_False_Unregisters_The_Channel()
    {
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10)));
        Assert.Equal(1, _interop.GanttV3RegisterWheelZoomCallCount);

        cut.Render(p => p
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.WheelZoom, false));

        Assert.Equal(1, _interop.GanttV3UnregisterWheelZoomCallCount);
    }

    [Fact]
    public void ZoomLevels_Override_Threads_Into_The_Registered_Wheel_Zoom_Options()
    {
        // Same resolved list GanttZoomControl/GanttNav step through — never a
        // second, independently-tunable list (design requirement).
        _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.ViewMode, L.GanttViewMode.Week)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.ZoomLevels, new List<L.GanttViewMode> { L.GanttViewMode.Week, L.GanttViewMode.Month }));

        var options = Assert.IsType<Dictionary<string, object?>>(_interop.LastGanttV3WheelZoomOptions);
        Assert.Equal(new[] { "Week", "Month" }, Assert.IsType<string[]>(options["levels"]));
        Assert.Equal("Week", options["currentMode"]);
    }

    [Fact]
    public void ViewMode_Change_Reregisters_With_The_New_CurrentMode()
    {
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10)));

        cut.Render(p => p
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.ViewMode, L.GanttViewMode.Month)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10)));

        var options = Assert.IsType<Dictionary<string, object?>>(_interop.LastGanttV3WheelZoomOptions);
        Assert.Equal("Month", options["currentMode"]);
    }

    // ── CommitWheelZoom: anchor math + defensive guards ────────────────────

    [Fact]
    public async Task CommitWheelZoom_Computes_The_Pointer_Anchor_Date_Under_The_Old_Scale()
    {
        // Day mode: ColumnWidth=38, 1 column = 1 day (GanttScale's own
        // ViewModes table). contentX = 133px = 3.5 columns from Origin, so
        // the CONTINUOUS (unsnapped) anchor date is Origin + 3.5 days —
        // exactly the precision PixelToDateContinuous exists to preserve
        // (PixelToDate would instead ROUND to the nearest whole day, which
        // is the wrong math here — see that method's own remarks).
        (L.GanttViewMode Mode, DateTime AnchorDate, double AnchorOffsetPx)? received = null;
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.OnWheelZoomRequested, (ValueTuple<L.GanttViewMode, DateTime, double> r) => received = r));

        var origin = GanttScale.BuildDateUnits(L.GanttViewMode.Day, D(2026, 1, 1), D(2026, 1, 10))[0];
        var expectedAnchor = origin.AddDays(3.5);

        await cut.InvokeAsync(() => cut.Instance.CommitWheelZoom("Week", 133.0, 42.0));

        Assert.NotNull(received);
        Assert.Equal(L.GanttViewMode.Week, received!.Value.Mode);
        Assert.Equal(expectedAnchor, received.Value.AnchorDate);
        Assert.Equal(42.0, received.Value.AnchorOffsetPx);
    }

    [Fact]
    public async Task CommitWheelZoom_Falls_Back_To_ViewModeChanged_When_No_Richer_Host_Is_Listening()
    {
        // Standalone-GanttTimeline contract (no GanttChart wrapping it, so no
        // anchor-preserving recenter pipeline exists to feed) — wheel-zoom
        // still changes the mode via the plain ViewModeChanged callback every
        // OTHER GanttTimeline zoom trigger (GanttZoomControl) already uses.
        L.GanttViewMode? pushed = null;
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.ViewModeChanged, (L.GanttViewMode m) => pushed = m));
        // Deliberately no OnWheelZoomRequested wired.

        await cut.InvokeAsync(() => cut.Instance.CommitWheelZoom("Year", 200.0, 10.0));

        Assert.Equal(L.GanttViewMode.Year, pushed);
    }

    [Fact]
    public async Task CommitWheelZoom_Prefers_OnWheelZoomRequested_Over_ViewModeChanged_When_Both_Are_Wired()
    {
        // Exactly ONE of the two must fire (see OnWheelZoomRequested's own
        // remarks — firing both would double-apply the same mode change
        // through GanttChart's ApplyViewModeIntentAsync guard). Predicted
        // wrong value if this regressed to firing both: viewModeChangedFired
        // would be true instead of false.
        var richPathFired = false;
        var viewModeChangedFired = false;
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.OnWheelZoomRequested, (ValueTuple<L.GanttViewMode, DateTime, double> _) => richPathFired = true)
            .Add(c => c.ViewModeChanged, (L.GanttViewMode _) => viewModeChangedFired = true));

        await cut.InvokeAsync(() => cut.Instance.CommitWheelZoom("Week", 100.0, 5.0));

        Assert.True(richPathFired);
        Assert.False(viewModeChangedFired);
    }

    [Fact]
    public async Task CommitWheelZoom_NoOps_When_WheelZoom_Is_False()
    {
        // Defensive: JSInvokable is a public surface a stray/buggy JS caller
        // could still hit even with the feature turned off.
        var fired = false;
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.WheelZoom, false)
            .Add(c => c.ViewModeChanged, (L.GanttViewMode _) => fired = true));

        await cut.InvokeAsync(() => cut.Instance.CommitWheelZoom("Week", 100.0, 5.0));

        Assert.False(fired);
    }

    [Fact]
    public async Task CommitWheelZoom_NoOps_For_An_Unparseable_Mode_String()
    {
        var fired = false;
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.ViewModeChanged, (L.GanttViewMode _) => fired = true));

        await cut.InvokeAsync(() => cut.Instance.CommitWheelZoom("NotAViewMode", 100.0, 5.0));

        Assert.False(fired);
    }

    [Fact]
    public async Task CommitWheelZoom_NoOps_When_The_Requested_Mode_Equals_The_Current_One()
    {
        var fired = false;
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.ViewModeChanged, (L.GanttViewMode _) => fired = true));

        await cut.InvokeAsync(() => cut.Instance.CommitWheelZoom("Day", 100.0, 5.0));

        Assert.False(fired);
    }

    // ── GanttChart integration: pointer anchor, not viewport center ───────

    [Fact]
    public async Task WheelZoom_Recenter_Goes_Through_ScrollToOffset_Not_ScrollToCenter()
    {
        // THE core "anchored on the pointer" proof: a wheel-zoom-driven mode
        // change must land its recenter through GanttV3ScrollToOffsetAsync
        // (pointer-local-offset placement), never through the
        // GanttV3ScrollToXAsync/centerOn path every OTHER ViewMode trigger
        // uses. Predicted wrong value against the pre-fix state (before
        // ReconcileAsync/EmitScrollIntent/ScrollToCenterAsync gained the
        // offset-override plumbing): GanttV3ScrollToOffsetCalls.Count would
        // stay 0 and the mode-change recenter would show up as one MORE
        // GanttV3ScrollToXAsync call instead (viewport-centered, not
        // pointer-anchored).
        var state = new GanttState();
        var cut = _ctx.Render<L.GanttChart>(p => p
            .Add(c => c.State, state)
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.ViewMode, L.GanttViewMode.Day));

        var scrollToXCountBefore = _interop.GanttV3ScrollToXCalls.Count;

        await cut.InvokeAsync(() => _interop.SimulateGanttV3WheelZoom("Week", 500.0, 42.5));

        Assert.Equal(L.GanttViewMode.Week, state.ViewMode);
        Assert.Single(_interop.GanttV3ScrollToOffsetCalls);
        // The pointer's own local offset is forwarded VERBATIM (see
        // GanttTimeline.ScrollToCenterAsync's own remarks — offsetOverride is
        // never transformed, only targetX gets ScrollHostLeadingOffset added)
        // — an exact, concretely-predictable round-trip check.
        Assert.Equal(42.5, _interop.GanttV3ScrollToOffsetCalls[^1].OffsetPx);
        // No EXTRA viewport-centered scroll was issued for this same gesture.
        Assert.Equal(scrollToXCountBefore, _interop.GanttV3ScrollToXCalls.Count);
    }

    [Fact]
    public void Toolbar_Driven_ViewMode_Change_Still_Recenters_On_The_Viewport_Center()
    {
        // Regression guard for the EXISTING (unrelated-to-wheel-zoom) path —
        // a disable-check that always passes would mean the fixture is
        // wrong, so this specifically proves the offset-override plumbing
        // does NOT leak into the toolbar's own recenter: it must keep using
        // GanttV3ScrollToXAsync, and must NEVER call GanttV3ScrollToOffsetAsync
        // at all. Predicted wrong value if EmitScrollIntent's new
        // offsetOverride parameter were wired unconditionally instead of
        // wheel-zoom-only: GanttV3ScrollToOffsetCalls would be non-empty here.
        var state = new GanttState();
        var cut = _ctx.Render<L.GanttChart>(p => p
            .Add(c => c.State, state)
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.ShowZoomControl, true));

        // GanttZoomControl's own floating stepper — a plain <Button
        // aria-label="Zoom out">, steps Day -> Week (DefaultLevels' own
        // coarsest-last order — see that control's remarks).
        cut.Find("button[aria-label='Zoom out']").Click();

        Assert.Equal(L.GanttViewMode.Week, state.ViewMode);
        Assert.Empty(_interop.GanttV3ScrollToOffsetCalls);
    }
}
