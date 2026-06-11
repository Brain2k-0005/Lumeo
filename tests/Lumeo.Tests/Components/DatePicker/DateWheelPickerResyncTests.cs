using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.DatePicker;

/// <summary>
/// Regression tests: wheel columns were scroll-positioned only on firstRender,
/// so an external Value change left the columns at stale offsets and the next
/// user scroll committed from stale geometry. The fix re-syncs scroll positions
/// (via the existing WheelScrollTo interop) whenever the bound value changes
/// externally — but not when the parent merely echoes the wheel's own commit.
/// </summary>
public class DateWheelPickerResyncTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public DateWheelPickerResyncTests()
    {
        _ctx.AddLumeoServices();
        // Replace the loose-mode interop with the tracking fake so WheelScrollTo
        // calls (and their target offsets) can be asserted.
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private const int ItemHeight = 40; // must match DateWheelPicker's h-10 rows

    [Fact]
    public void First_Render_Positions_All_Three_Columns()
    {
        var cut = _ctx.Render<L.DateWheelPicker>(p => p.Add(c => c.Value, new DateOnly(2024, 3, 15)));

        cut.WaitForAssertion(() => Assert.Equal(3, _interop.WheelScrollToCallCount));
        Assert.Equal((15 - 1) * ItemHeight, _interop.WheelScrollToTops[0]); // day
        Assert.Equal((3 - 1) * ItemHeight, _interop.WheelScrollToTops[1]);  // month
    }

    [Fact]
    public void ReRender_With_Same_Value_Does_Not_ReScroll()
    {
        var value = new DateOnly(2024, 3, 15);
        var cut = _ctx.Render<L.DateWheelPicker>(p => p.Add(c => c.Value, value));
        cut.WaitForAssertion(() => Assert.Equal(3, _interop.WheelScrollToCallCount));

        cut.Render(p => p.Add(c => c.Value, value));

        Assert.Equal(3, _interop.WheelScrollToCallCount);
    }

    [Fact]
    public void External_Value_Change_ReSyncs_Column_Scroll_Positions()
    {
        var cut = _ctx.Render<L.DateWheelPicker>(p => p.Add(c => c.Value, new DateOnly(2024, 3, 15)));
        cut.WaitForAssertion(() => Assert.Equal(3, _interop.WheelScrollToCallCount));

        cut.Render(p => p.Add(c => c.Value, new DateOnly(2025, 7, 4)));

        cut.WaitForAssertion(() => Assert.Equal(6, _interop.WheelScrollToCallCount));
        Assert.Equal((4 - 1) * ItemHeight, _interop.WheelScrollToTops[3]); // day → 4th
        Assert.Equal((7 - 1) * ItemHeight, _interop.WheelScrollToTops[4]); // month → July
    }
}
