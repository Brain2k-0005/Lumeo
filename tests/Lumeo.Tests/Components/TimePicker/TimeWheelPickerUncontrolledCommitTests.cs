using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.TimePicker;

/// <summary>
/// Battle-wave2 #68 (state-on-data-change) — TimeWheelPicker used UNCONTROLLED.
///
/// The wheel columns are scroll-positioned only programmatically, so a value
/// change must re-scroll them. RaiseChange (a user commit) used to record only
/// <c>_lastSyncedValue</c>. When the picker is uncontrolled — i.e. the parent
/// does NOT bind the committed value back, so <c>Value</c> stays null — the next
/// parent re-render hit <c>OnParametersSet</c> where <c>null != _lastSyncedValue</c>
/// (the committed time) read as an EXTERNAL change. The component then reseeded the
/// columns from <c>TimeSpan.Zero</c> and re-scrolled both columns back to 00:00,
/// wiping the user's just-committed selection.
///
/// The fix tracks the last value RaiseChange emitted in a separate
/// <c>_ownLastEmitted</c> field and (a) only treats a Value change as external when
/// it differs from BOTH that and <c>_lastSyncedValue</c>, and (b) seeds from
/// <c>_ownLastEmitted</c> (not zero) when Value is null after a self-commit — so an
/// uncontrolled re-render leaves the committed selection (and its scroll offsets)
/// intact.
///
/// Mirrors TimeWheelPickerResyncTests (the WheelScrollTo seam) and the commit-via-
/// instance-method pattern of CodeEditorControlledValueTests. The real scroll path
/// depends on JS scroll offsets + a timer debounce that bUnit cannot reproduce, so
/// the commit is driven through the component's CommitSelectionForTest seam, which
/// runs the same RaiseChange the debounced scroll handlers do.
/// </summary>
public class TimeWheelPickerUncontrolledCommitTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public TimeWheelPickerUncontrolledCommitTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private const int ItemHeight = 40; // must match TimeWheelPicker's h-10 rows

    [Fact]
    public async Task Uncontrolled_Commit_Then_Rerender_Does_Not_Reseed_Columns_To_Zero()
    {
        // Uncontrolled: a ValueChanged delegate is attached (so the commit is
        // observed) but it does NOT write the value back, so Value stays null —
        // exactly how an uncontrolled consumer behaves.
        TimeSpan? emitted = null;
        var cb = EventCallback.Factory.Create<TimeSpan?>(this, (TimeSpan? t) => emitted = t);
        var cut = _ctx.Render<L.TimeWheelPicker>(p => p
            .Add(c => c.Use24Hour, true)
            .Add(c => c.Value, (TimeSpan?)null)
            .Add(c => c.ValueChanged, cb));

        // First render seeds + scrolls both columns to 00:00 (idx 0,0).
        cut.WaitForAssertion(() => Assert.Equal(2, _interop.WheelScrollToCallCount));

        // User settles the wheels on 05:45. This runs the same RaiseChange the
        // debounced scroll handlers do, emitting the value to the (non-binding)
        // parent.
        await cut.InvokeAsync(() => cut.Instance.CommitSelectionForTest(new TimeSpan(5, 45, 0)));
        Assert.Equal(new TimeSpan(5, 45, 0), emitted);

        var scrollsAfterCommit = _interop.WheelScrollToCallCount;

        // An unrelated parent re-render: Value is STILL null (uncontrolled — the
        // parent never adopted the commit). Without the fix this was read as an
        // external clear and re-scrolled both columns back to 00:00. With the fix
        // it is recognised as our own non-adopted emission, so no resync scroll
        // is issued.
        cut.Render(p => p
            .Add(c => c.Use24Hour, true)
            .Add(c => c.Value, (TimeSpan?)null)
            .Add(c => c.ValueChanged, cb));

        // No new WheelScrollTo calls — the committed selection survived.
        Assert.Equal(scrollsAfterCommit, _interop.WheelScrollToCallCount);

        // And the selection itself is still 05:45 (not reseeded to zero): the
        // highlighted hour/minute cells carry the font-semibold highlight class.
        var highlighted = cut.FindAll(".font-semibold");
        Assert.Contains(highlighted, e => e.TextContent.Trim() == "05");
        Assert.Contains(highlighted, e => e.TextContent.Trim() == "45");
    }

    [Fact]
    public async Task Genuine_External_Value_Change_Still_ReSyncs_After_A_Commit()
    {
        // Guards against the fix over-correcting: a real external Value change (a
        // different non-null time) after a self-commit must still re-scroll the
        // columns to the new selection.
        var cut = _ctx.Render<L.TimeWheelPicker>(p => p
            .Add(c => c.Use24Hour, true)
            .Add(c => c.Value, (TimeSpan?)null));
        cut.WaitForAssertion(() => Assert.Equal(2, _interop.WheelScrollToCallCount));

        await cut.InvokeAsync(() => cut.Instance.CommitSelectionForTest(new TimeSpan(5, 45, 0)));
        var scrollsAfterCommit = _interop.WheelScrollToCallCount;

        // Parent genuinely sets a new, different value (e.g. 08:15).
        cut.Render(p => p
            .Add(c => c.Use24Hour, true)
            .Add(c => c.Value, new TimeSpan(8, 15, 0)));

        cut.WaitForAssertion(() => Assert.Equal(scrollsAfterCommit + 2, _interop.WheelScrollToCallCount));
        Assert.Equal(8 * ItemHeight, _interop.WheelScrollToTops[scrollsAfterCommit]);      // hour 08
        Assert.Equal(15 * ItemHeight, _interop.WheelScrollToTops[scrollsAfterCommit + 1]); // minute 15
    }
}
