using System.Globalization;
using AngleSharp.Dom;
using Bunit;
using Lumeo.Tests.Helpers;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Slider;

/// <summary>
/// Issue #555 — range slider thumbs crossed over and the filled bar overshot them.
///
/// Root cause 1: each native range input was given its own reachable min/max
/// (start: Min..ValueEnd, end: Value..Max). A native range maps value to pixel
/// through ITS OWN min/max, so the two thumbs were drawn on different scales (and the
/// bar, computed on Min..Max, matched neither). Both inputs must share the full
/// Min..Max scale; the reachable bound lives in the clamp and in aria-valuemin/max.
///
/// Root cause 2: a clamped drag that lands back on the model value produced no
/// render-tree diff, so Blazor never wrote the value to the DOM and the thumb stayed
/// past its neighbour. The handler now writes the clamped value onto the element.
///
/// Root cause 3: the bar used 0..100% of the track but a native thumb centre travels
/// from half a thumb to (width - half a thumb).
/// </summary>
public class SliderRangeNativeBoundsTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly BunitJSModuleInterop _module;

    public SliderRangeNativeBoundsTests()
    {
        _ctx.AddLumeoServices();
        _module = _ctx.SetupComponentsModule();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static double Attr(IElement el, string name)
        => double.Parse(el.GetAttribute(name)!, NumberStyles.Any, CultureInfo.InvariantCulture);

    private IRenderedComponent<L.Slider> Range(double value, double end, int minSteps = 0, double step = 1,
        Action<double>? onStart = null, Action<double>? onEnd = null)
        => _ctx.Render<L.Slider>(p => p
            .Add(s => s.IsRange, true)
            .Add(s => s.Min, 0d)
            .Add(s => s.Max, 100d)
            .Add(s => s.Step, step)
            .Add(s => s.MinStepsBetweenThumbs, minSteps)
            .Add(s => s.Value, value)
            .Add(s => s.ValueEnd, end)
            .Add(s => s.ValueChanged, v => onStart?.Invoke(v))
            .Add(s => s.ValueEndChanged, v => onEnd?.Invoke(v)));

    [Fact]
    public void Both_native_inputs_share_the_full_scale()
    {
        var cut = Range(20, 40);
        var inputs = cut.FindAll("input[type=range]");

        foreach (var i in inputs)
        {
            Assert.Equal(0d, Attr(i, "min"));
            Assert.Equal(100d, Attr(i, "max"));
        }
    }

    [Fact]
    public void Aria_bounds_still_announce_only_the_reachable_range()
    {
        var cut = Range(30, 60, minSteps: 2, step: 5);
        var inputs = cut.FindAll("input[type=range]");

        Assert.Equal(50d, Attr(inputs[0], "aria-valuemax")); // 60 - 10
        Assert.Equal(40d, Attr(inputs[1], "aria-valuemin")); // 30 + 10
    }

    [Fact]
    public async Task Start_thumb_dragged_past_end_is_clamped_to_end()
    {
        double? start = null;
        var cut = Range(25, 75, onStart: v => start = v);

        await cut.InvokeAsync(() => cut.FindAll("input[type=range]")[0].Input("100"));

        Assert.Equal(75d, start);
        _module.VerifyInvoke("setInputValue");
    }

    [Fact]
    public async Task Start_thumb_clamps_at_end_minus_MinGap()
    {
        double? start = null;
        var cut = Range(20, 60, minSteps: 2, step: 5, onStart: v => start = v);

        await cut.InvokeAsync(() => cut.FindAll("input[type=range]")[0].Input("90"));

        Assert.Equal(50d, start); // 60 - 2*5
    }

    [Fact]
    public async Task End_thumb_dragged_below_start_is_clamped_to_start()
    {
        double? end = null;
        var cut = Range(25, 75, onEnd: v => end = v);

        await cut.InvokeAsync(() => cut.FindAll("input[type=range]")[1].Input("3"));

        Assert.Equal(25d, end);
        _module.VerifyInvoke("setInputValue");
    }

    [Fact]
    public async Task End_thumb_clamps_at_start_plus_MinGap()
    {
        double? end = null;
        var cut = Range(30, 80, minSteps: 3, step: 2, onEnd: v => end = v);

        await cut.InvokeAsync(() => cut.FindAll("input[type=range]")[1].Input("0"));

        Assert.Equal(36d, end); // 30 + 3*2
    }

    [Fact]
    public async Task Clamped_input_to_unchanged_model_value_still_resyncs_the_dom()
    {
        // Model already at the bound: no value change, so without an explicit DOM write the
        // native thumb would remain at the raw dragged position.
        var cut = Range(75, 75);

        await cut.InvokeAsync(() => cut.FindAll("input[type=range]")[0].Input("90"));

        _module.VerifyInvoke("setInputValue");
    }

    [Fact]
    public void Range_bar_is_inset_by_half_a_thumb_so_it_ends_at_the_thumb_centres()
    {
        var cut = Range(25, 75);
        var bar = cut.FindAll("div.rounded-full").First(d => d.GetAttribute("style")?.Contains("margin-left") == true);
        var style = bar.GetAttribute("style")!;

        // centre of a native thumb: 8px + (100% - 16px) * fraction
        Assert.Contains("margin-left: calc(8px + (100% - 16px) * 0.25)", style);
        Assert.Contains("width: calc((100% - 16px) * 0.5)", style);
    }
}
