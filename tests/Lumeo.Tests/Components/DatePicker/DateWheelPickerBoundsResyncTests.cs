using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.DatePicker;

/// <summary>
/// Battle-wave1 #54 + #55 (state-on-data-change) — DateWheelPicker bounds resync.
///
/// The wheel columns are scroll-positioned only programmatically, so any change
/// that reshapes a column's geometry must re-run the scroll seeding. Two cases
/// were missed:
///
/// #54 — An external Min/Max change rebuilds the year column's row set
/// (<c>_years</c>) but, when <c>Value</c> is unchanged, never marked
/// <c>_resyncPending</c>. The year column kept its old scroll offset against the
/// new, differently-sized row list, so the next user scroll committed the wrong
/// year. Fix: track the last (minYear, maxYear) bounds and OR their change into
/// <c>_resyncPending</c>.
///
/// #55 — A user scroll commit whose raw selection fell outside Min/Max was
/// clamped by RaiseChange, which pre-set <c>_lastSyncedValue</c> to the clamp.
/// The columns stayed parked on the out-of-range row while the committed value
/// was the clamp, and OnParametersSet (Value == _lastSyncedValue) skipped the
/// resync. Fix: when RaiseChange actually clamps, set <c>_resyncPending</c> so
/// the columns snap back to the clamped selection.
///
/// Mirrors DateWheelPickerResyncTests (the WheelScrollTo seam) and the
/// commit-via-instance-method pattern of TimeWheelPickerUncontrolledCommitTests.
/// The real scroll path depends on JS scroll offsets + a timer debounce that
/// bUnit cannot reproduce, so the clamp commit is driven through the component's
/// CommitSelectionForTest seam, which runs the same RaiseChange the debounced
/// scroll handlers do.
/// </summary>
public class DateWheelPickerBoundsResyncTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public DateWheelPickerBoundsResyncTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private const int ItemHeight = 40; // must match DateWheelPicker's h-10 rows

    [Fact]
    public void External_Min_Max_Change_With_Same_Value_ReSyncs_Column_Scroll_Positions()
    {
        // #54: render with a value and an initial year range.
        var value = new DateOnly(2024, 3, 15);
        var cut = _ctx.Render<L.DateWheelPicker>(p => p
            .Add(c => c.Value, value)
            .Add(c => c.Min, new DateOnly(2020, 1, 1))
            .Add(c => c.Max, new DateOnly(2030, 12, 31)));

        // First render seeds + scrolls all three columns.
        cut.WaitForAssertion(() => Assert.Equal(3, _interop.WheelScrollToCallCount));

        // The bounds shift (year range now 2010..2040) while Value is unchanged.
        // The year column's row set is rebuilt, so its previously-seeded offset
        // is stale and must be re-synced. Without the fix this re-render issued
        // no WheelScrollTo (only Value changes triggered a resync).
        cut.Render(p => p
            .Add(c => c.Value, value)
            .Add(c => c.Min, new DateOnly(2010, 1, 1))
            .Add(c => c.Max, new DateOnly(2040, 12, 31)));

        cut.WaitForAssertion(() => Assert.Equal(6, _interop.WheelScrollToCallCount));
        // Year column re-positions to 2024's new index within the 2010..2040 list.
        var yearIdx = 2024 - 2010;
        Assert.Equal(yearIdx * ItemHeight, _interop.WheelScrollToTops[5]); // year (3rd of the resync batch)
    }

    [Fact]
    public void Same_Min_Max_Rerender_Does_Not_ReScroll()
    {
        // Guards against the #54 fix over-correcting: an unrelated re-render that
        // leaves Value AND the bounds unchanged must not issue a resync scroll.
        var value = new DateOnly(2024, 3, 15);
        var min = new DateOnly(2020, 1, 1);
        var max = new DateOnly(2030, 12, 31);
        var cut = _ctx.Render<L.DateWheelPicker>(p => p
            .Add(c => c.Value, value)
            .Add(c => c.Min, min)
            .Add(c => c.Max, max));
        cut.WaitForAssertion(() => Assert.Equal(3, _interop.WheelScrollToCallCount));

        cut.Render(p => p
            .Add(c => c.Value, value)
            .Add(c => c.Min, min)
            .Add(c => c.Max, max));

        Assert.Equal(3, _interop.WheelScrollToCallCount);
    }

    [Fact]
    public async Task Clamped_Scroll_Commit_ReScrolls_Columns_Back_To_The_Clamp()
    {
        // #55: a value with Min/Max bounds. The user scrolls the day column below
        // the Min day; RaiseChange clamps the commit to Min.
        var cut = _ctx.Render<L.DateWheelPicker>(p => p
            .Add(c => c.Value, new DateOnly(2024, 3, 15))
            .Add(c => c.Min, new DateOnly(2024, 3, 10))
            .Add(c => c.Max, new DateOnly(2024, 3, 20)));
        cut.WaitForAssertion(() => Assert.Equal(3, _interop.WheelScrollToCallCount));

        // User settles the day wheel on day 1 (2024-03-01), which is < Min
        // (2024-03-10). RaiseChange clamps to 2024-03-10. This runs the same
        // commit the debounced scroll handlers do.
        await cut.InvokeAsync(() => cut.Instance.CommitSelectionForTest(2024, 3, 1));

        // The parent adopts the clamped value (controlled usage). Because
        // RaiseChange pre-synced _lastSyncedValue to the clamp, OnParametersSet
        // sees Value == _lastSyncedValue and would NOT trigger a resync on its
        // own — the resync must come from the clamp flag set inside RaiseChange.
        cut.Render(p => p
            .Add(c => c.Value, new DateOnly(2024, 3, 10))
            .Add(c => c.Min, new DateOnly(2024, 3, 10))
            .Add(c => c.Max, new DateOnly(2024, 3, 20)));

        // Without the fix the columns stayed parked on day 1 (out of range) and
        // no resync scroll was issued; with the fix the columns snap back to the
        // clamped day (10).
        cut.WaitForAssertion(() => Assert.True(_interop.WheelScrollToCallCount > 3,
            "clamped commit should force a column resync"));
        // The day column re-scrolls to the clamped day (index 9 → day 10).
        Assert.Equal((10 - 1) * ItemHeight, _interop.WheelScrollToTops[3]);
    }
}
