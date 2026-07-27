using Bunit;
using Lumeo.GanttV3;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.GanttV3;

/// <summary>
/// Codex round 13 (PR #379, feat/gantt-v3) - the single remaining P2:
/// ColumnWidth was never part of the recenter reconcile's trigger set at
/// all. (a) A ColumnWidth-alone change (ViewMode unchanged) triggered no
/// reconciliation whatsoever, so the DOM's raw scrollLeft pixel value
/// stayed put while the pixel-per-column SCALE changed underneath it - a
/// visible date jump with nothing correcting it. (b) Even when ViewMode
/// ALSO changed in the same render (which DID already trigger a capture),
/// ResolveCurrentCenterDateAsync decoded the still-unmoved physical scroll
/// position using the ALREADY-NEW ColumnWidth parameter value instead of
/// the OLD one it was actually captured under. See
/// docs/superpowers/gantt-v3-cx13-report.md for the full writeup and the
/// extended tasks x mode x width matrix (also documented as a comment table
/// directly in Gantt3.razor's own OnParametersSetAsync/
/// ReconcileRecenterTriggersAsync).
/// </summary>
public class GanttV3CodexRound13Tests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public GanttV3CodexRound13Tests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<Lumeo.Services.IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static DateTime D(int y, int m, int d) => new(y, m, d);

    [Fact]
    public void Gantt3_Recenters_Preserving_The_Center_When_ColumnWidth_Changes_Alone()
    {
        // Bug fix (Codex round 13 review, P2, part a): a ColumnWidth-alone
        // change (no ViewMode change) previously triggered NO reconciliation
        // at all - the range's own Origin doesn't move (ColumnWidth plays no
        // part in ComputeInitialRange), but nothing ever re-requested the
        // DOM's scrollLeft to account for the new pixel-per-day scale, so
        // the SAME raw scroll pixel decoded to a DIFFERENT date under it.
        var task = new L.GanttTask("t1", "Task", D(2026, 1, 1), D(2026, 1, 5));
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { task })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.ShowTreePane, false));

        var dayCfg = GanttScale.GetConfig(L.GanttViewMode.Day);
        var rangeStart = D(2026, 1, 1).AddDays(-dayCfg.PadBefore * dayCfg.Step);
        var rangeEnd = D(2026, 1, 5).AddDays(dayCfg.PadAfter * dayCfg.Step);
        var origin = GanttScale.BuildDateUnits(L.GanttViewMode.Day, rangeStart, rangeEnd)[0];

        // Live scroll reading decoded under Day mode's own DEFAULT width
        // (38px) - no ColumnWidth override yet.
        var pannedToDate = origin.Date.AddDays(10);
        _interop.GanttV3ScrollCenterXToReturn = 10 * dayCfg.ColumnWidth;

        var scrollToCallsBefore = _interop.GanttV3ScrollToXCallCount;

        // ColumnWidth alone changes - SAME tasks reference, SAME ViewMode.
        const int newColumnWidth = 76;
        cut.Render(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { task })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.ShowTreePane, false)
            .Add(c => c.ColumnWidth, newColumnWidth));

        Assert.True(_interop.GanttV3ScrollToXCallCount > scrollToCallsBefore,
            "expected a ColumnWidth-alone change to request a recenter");

        // Ground truth: captured under the OLD width (Day's own default,
        // 38px) against the SAME Origin (no range rebuild for a pure
        // ColumnWidth change - only the pixel SCALE changed, not the date
        // window), then re-applied through the NEW width for the actual
        // scroll-to target.
        var expectedScrollToX = GanttScale.DateToPixel(L.GanttViewMode.Day, origin, pannedToDate, newColumnWidth);
        Assert.Equal(expectedScrollToX, _interop.GanttV3ScrollToXCalls[^1], 1);
    }

    [Fact]
    public void Gantt3_Recenters_Preserving_The_Center_When_ColumnWidth_And_ViewMode_Change_Together()
    {
        // Bug fix (Codex round 13 review, P2, part b): even though a
        // ViewMode change already triggered a capture, ResolveCurrentCenterDateAsync
        // used to read the LIVE ColumnWidth parameter internally - by the
        // time the capture ran, that parameter was ALREADY the new value,
        // so a caller changing BOTH ColumnWidth and ViewMode at once decoded
        // the STILL-UNMOVED physical scroll position using the wrong
        // (already-new) scale. This is the critical regression test: under
        // the old bug, the captured "center" would be wildly different from
        // pannedToDate (10 old-width columns decoded at the NEW width gives
        // a position under 2 days from Origin, not 10).
        var task = new L.GanttTask("t1", "Task", D(2026, 1, 1), D(2026, 1, 5));
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { task })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.ShowTreePane, false)
            .Add(c => c.ColumnWidth, 38)); // explicit override, same numeric value as Day's own default but now EXPLICIT so a later change is unambiguous

        var dayCfg = GanttScale.GetConfig(L.GanttViewMode.Day);
        var dayRangeStart = D(2026, 1, 1).AddDays(-dayCfg.PadBefore * dayCfg.Step);
        var dayRangeEnd = D(2026, 1, 5).AddDays(dayCfg.PadAfter * dayCfg.Step);
        var dayOrigin = GanttScale.BuildDateUnits(L.GanttViewMode.Day, dayRangeStart, dayRangeEnd)[0];

        const int oldColumnWidth = 38;
        var pannedToDate = dayOrigin.Date.AddDays(10);
        _interop.GanttV3ScrollCenterXToReturn = 10 * oldColumnWidth;

        // BOTH ColumnWidth AND ViewMode change in the SAME render.
        const int newColumnWidth = 200;
        cut.Render(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { task })
            .Add(c => c.ViewMode, L.GanttViewMode.Week)
            .Add(c => c.ShowTreePane, false)
            .Add(c => c.ColumnWidth, newColumnWidth));

        // Ground truth: the range self-centers (Week mode, ApplyPadding)
        // around pannedToDate - the CORRECTLY-decoded (under the OLD width)
        // center - then the final scroll-to target uses the NEW width for
        // rendering.
        var weekCfg = GanttScale.GetConfig(L.GanttViewMode.Week);
        var newRangeStart = pannedToDate.AddDays(-weekCfg.PadBefore * weekCfg.Step);
        var newRangeEnd = pannedToDate.AddDays(weekCfg.PadAfter * weekCfg.Step);
        var newOrigin = GanttScale.BuildDateUnits(L.GanttViewMode.Week, newRangeStart, newRangeEnd)[0];
        var expectedScrollToX = GanttScale.DateToPixel(L.GanttViewMode.Week, newOrigin, pannedToDate, newColumnWidth);

        Assert.NotEmpty(_interop.GanttV3ScrollToXCalls);
        Assert.Equal(expectedScrollToX, _interop.GanttV3ScrollToXCalls[^1], 1);
    }

    [Fact]
    public void Gantt3_Does_Not_Recenter_When_Neither_Tasks_Mode_Nor_ColumnWidth_Change()
    {
        // Regression guard: an unrelated re-render (nothing in the tracked
        // trigger set changed) must still be a no-op, same as before this
        // round's own ColumnWidth addition.
        var task = new L.GanttTask("t1", "Task", D(2026, 1, 1), D(2026, 1, 5));
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { task })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.ColumnWidth, 38));

        var scrollToCallsBefore = _interop.GanttV3ScrollToXCallCount;

        cut.Render(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { task })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.ColumnWidth, 38));

        Assert.Equal(scrollToCallsBefore, _interop.GanttV3ScrollToXCallCount);
    }
}
