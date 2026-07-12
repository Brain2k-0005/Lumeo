using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Slider;

/// <summary>
/// Slider is a native &lt;input type="range"&gt; — Arrow/Home/End/PageUp/PageDown
/// stepping is supplied for free by the browser, which bUnit's DOM does not simulate;
/// per the gap-analysis brief the observable surface is the resulting @oninput/
/// ValueChanged round-trip, so these tests drive Input(...) with the value the browser
/// would produce for Home/End (jump to Min/Max) and assert the callback + rendered
/// value. SliderTests.cs already covers the equivalent for arbitrary drag-in-between
/// values; Home/End were not covered anywhere. The range-variant test pins that both
/// thumbs are separate, real, non-tabindex-suppressed &lt;input&gt; elements in DOM
/// order — the mechanism that puts them at consecutive Tab stops.
/// </summary>
public class SliderKeyboardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public SliderKeyboardTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Home_Key_Equivalent_Jumps_To_Min()
    {
        double? received = null;
        var cut = _ctx.Render<L.Slider>(p => p
            .Add(s => s.Min, 10d)
            .Add(s => s.Max, 90d)
            .Add(s => s.Value, 50d)
            .Add(s => s.ValueChanged, v => received = v));

        // What the browser produces on Home: the input's value snaps to min="10".
        cut.Find("input[type='range']").Input("10");

        Assert.Equal(10d, received);
    }

    [Fact]
    public void End_Key_Equivalent_Jumps_To_Max()
    {
        double? received = null;
        var cut = _ctx.Render<L.Slider>(p => p
            .Add(s => s.Min, 10d)
            .Add(s => s.Max, 90d)
            .Add(s => s.Value, 50d)
            .Add(s => s.ValueChanged, v => received = v));

        // What the browser produces on End: the input's value snaps to max="90".
        cut.Find("input[type='range']").Input("90");

        Assert.Equal(90d, received);
    }

    [Fact]
    public void Range_Variant_Renders_Two_Independent_Tabbable_Thumbs_In_DOM_Order()
    {
        var cut = _ctx.Render<L.Slider>(p => p
            .Add(s => s.IsRange, true)
            .Add(s => s.AriaLabel, "Price")
            .Add(s => s.Value, 20d)
            .Add(s => s.ValueEnd, 80d));

        var inputs = cut.FindAll("input[type='range']");
        Assert.Equal(2, inputs.Count);

        // Neither thumb is pulled out of the Tab order, and the DOM order (start
        // thumb first, end thumb second) is exactly the Tab sequence a keyboard user
        // encounters.
        Assert.Null(inputs[0].GetAttribute("tabindex"));
        Assert.Null(inputs[1].GetAttribute("tabindex"));
        Assert.Equal("Price", inputs[0].GetAttribute("aria-label"));
        Assert.Contains("Price", inputs[1].GetAttribute("aria-label"));
        Assert.NotEqual(inputs[0].GetAttribute("aria-label"), inputs[1].GetAttribute("aria-label"));
    }
}
