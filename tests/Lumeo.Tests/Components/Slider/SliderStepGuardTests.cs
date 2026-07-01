using System.Globalization;
using AngleSharp.Dom;
using Bunit;
using Lumeo.Tests.Helpers;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Slider;

/// <summary>
/// Battle-wave2 #162 (low, edge-data) — the <c>Step</c> parameter had no
/// validation (default 1). A consumer-supplied <c>Step=0</c>, a negative Step, or
/// a non-finite Step (NaN / Infinity) was rendered raw as <c>step="@Step"</c> on
/// the native range input (making it inert / snap-to-nothing) and poisoned the
/// derived math: a NaN Step makes <c>MinGap = MinStepsBetweenThumbs * Step</c> NaN,
/// which scrambles the range thumbs' reachable bounds (StartThumbMax / EndThumbMin)
/// and the percentage math. The fix clamps Step to 1 for any non-positive or
/// non-finite value in OnParametersSet, mirroring native/Radix semantics.
///
/// These tests assert the OBSERVABLE mechanism: the rendered native
/// <c>step</c> attribute, and that a poisoned Step no longer produces a NaN range
/// bound.
/// </summary>
public class SliderStepGuardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SliderStepGuardTests() => _ctx.AddLumeoServices();

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static double Attr(IElement el, string name)
        => double.Parse(el.GetAttribute(name)!, NumberStyles.Any, CultureInfo.InvariantCulture);

    [Fact]
    public void ZeroStep_FallsBackToOne_OnNativeInput()
    {
        // Without the fix the input rendered step="0" (inert). With the fix Step
        // is clamped to 1 in OnParametersSet.
        var cut = _ctx.Render<L.Slider>(p => p
            .Add(s => s.Min, 0d)
            .Add(s => s.Max, 100d)
            .Add(s => s.Value, 50d)
            .Add(s => s.Step, 0d));

        var input = cut.Find("input[type=range]");

        Assert.Equal(1d, Attr(input, "step"));
    }

    [Fact]
    public void NegativeStep_FallsBackToOne_OnNativeInput()
    {
        var cut = _ctx.Render<L.Slider>(p => p
            .Add(s => s.Min, 0d)
            .Add(s => s.Max, 100d)
            .Add(s => s.Value, 50d)
            .Add(s => s.Step, -5d));

        var input = cut.Find("input[type=range]");

        Assert.Equal(1d, Attr(input, "step"));
    }

    [Fact]
    public void NaNStep_DoesNotPoison_RangeThumbBounds()
    {
        // A NaN Step would make MinGap = MinStepsBetweenThumbs * Step NaN, which
        // propagates into StartThumbMax/EndThumbMin (the native + aria bounds of the
        // two range thumbs) — a NaN min/max is non-sensical. The guard clamps Step
        // to 1 first, so MinGap = 2 * 1 = 2 and the bounds stay finite.
        var cut = _ctx.Render<L.Slider>(p => p
            .Add(s => s.IsRange, true)
            .Add(s => s.Min, 0d)
            .Add(s => s.Max, 100d)
            .Add(s => s.MinStepsBetweenThumbs, 2)
            .Add(s => s.Value, 30d)
            .Add(s => s.ValueEnd, 60d)
            .Add(s => s.Step, double.NaN));

        var inputs = cut.FindAll("input[type=range]");
        var startMax = Attr(inputs[0], "max"); // StartThumbMax = ValueEnd - MinGap
        var endMin = Attr(inputs[1], "min");   // EndThumbMin   = Value + MinGap

        Assert.False(double.IsNaN(startMax));
        Assert.False(double.IsNaN(endMin));
        // Step clamped to 1 -> MinGap = 2*1 = 2 -> 60-2=58, 30+2=32.
        Assert.Equal(58d, startMax);
        Assert.Equal(32d, endMin);
    }

    [Fact]
    public void PositiveStep_IsPreserved_Exactly()
    {
        // Normal-path behaviour must be untouched: a valid Step renders as-is.
        var cut = _ctx.Render<L.Slider>(p => p
            .Add(s => s.Min, 0d)
            .Add(s => s.Max, 100d)
            .Add(s => s.Value, 50d)
            .Add(s => s.Step, 2.5d));

        var input = cut.Find("input[type=range]");

        Assert.Equal(2.5d, Attr(input, "step"));
    }
}
