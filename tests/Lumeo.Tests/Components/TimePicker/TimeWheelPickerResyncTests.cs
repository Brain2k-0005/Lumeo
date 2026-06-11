using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.TimePicker;

/// <summary>
/// Regression tests: TimeWheelPicker columns were scroll-positioned only on
/// firstRender — an external Value change left them at stale offsets. Mirrors
/// DateWheelPickerResyncTests.
/// </summary>
public class TimeWheelPickerResyncTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public TimeWheelPickerResyncTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private const int ItemHeight = 40; // must match TimeWheelPicker's h-10 rows

    [Fact]
    public void First_Render_Positions_Hour_And_Minute_Columns()
    {
        // Pin Use24Hour so the column count is culture-independent (no AM/PM column).
        var cut = _ctx.Render<L.TimeWheelPicker>(p => p
            .Add(c => c.Use24Hour, true)
            .Add(c => c.Value, new TimeSpan(2, 30, 0)));

        cut.WaitForAssertion(() => Assert.Equal(2, _interop.WheelScrollToCallCount));
        Assert.Equal(2 * ItemHeight, _interop.WheelScrollToTops[0]);  // hour 02
        Assert.Equal(30 * ItemHeight, _interop.WheelScrollToTops[1]); // minute 30
    }

    [Fact]
    public void ReRender_With_Same_Value_Does_Not_ReScroll()
    {
        var value = new TimeSpan(2, 30, 0);
        var cut = _ctx.Render<L.TimeWheelPicker>(p => p
            .Add(c => c.Use24Hour, true)
            .Add(c => c.Value, value));
        cut.WaitForAssertion(() => Assert.Equal(2, _interop.WheelScrollToCallCount));

        cut.Render(p => p.Add(c => c.Value, value));

        Assert.Equal(2, _interop.WheelScrollToCallCount);
    }

    [Fact]
    public void External_Value_Change_ReSyncs_Column_Scroll_Positions()
    {
        var cut = _ctx.Render<L.TimeWheelPicker>(p => p
            .Add(c => c.Use24Hour, true)
            .Add(c => c.Value, new TimeSpan(2, 30, 0)));
        cut.WaitForAssertion(() => Assert.Equal(2, _interop.WheelScrollToCallCount));

        cut.Render(p => p.Add(c => c.Value, new TimeSpan(5, 45, 0)));

        cut.WaitForAssertion(() => Assert.Equal(4, _interop.WheelScrollToCallCount));
        Assert.Equal(5 * ItemHeight, _interop.WheelScrollToTops[2]);  // hour 05
        Assert.Equal(45 * ItemHeight, _interop.WheelScrollToTops[3]); // minute 45
    }
}
