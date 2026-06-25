using System.Globalization;
using AngleSharp.Dom;
using Bunit;
using Lumeo.Tests.Helpers;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Slider;

/// <summary>
/// Battle-wave2 #163 (low, keyboard-a11y) — on a range slider the two native
/// range <input>s used the FULL Min..Max as their native min/max while their
/// aria-valuemin/max already announced only the reachable bound (StartThumbMax /
/// EndThumbMin). The disagreement let a keyboard/pointer user drag a thumb into
/// the forbidden zone (past the other thumb / inside MinStepsBetweenThumbs)
/// before the C# clamp snapped it back, and reported a native range the user
/// could not actually reach. The fix sets the native max/min on each range input
/// to the reachable bound so the native control itself stops there, matching ARIA.
///
/// bUnit cannot move real DOM focus, so these tests assert the OBSERVABLE
/// mechanism: the rendered native min/max attributes on the two range inputs and
/// that they agree with the already-correct aria-valuemax/aria-valuemin.
/// </summary>
public class SliderRangeNativeBoundsTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SliderRangeNativeBoundsTests() => _ctx.AddLumeoServices();

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static double Attr(IElement el, string name)
        => double.Parse(el.GetAttribute(name)!, NumberStyles.Any, CultureInfo.InvariantCulture);

    [Fact]
    public void StartInput_NativeMax_StopsAtReachableBound_NotFullMax()
    {
        var cut = _ctx.Render<L.Slider>(p => p
            .Add(s => s.IsRange, true)
            .Add(s => s.Min, 0d)
            .Add(s => s.Max, 100d)
            .Add(s => s.Value, 20d)
            .Add(s => s.ValueEnd, 40d));

        var start = cut.FindAll("input[type=range]")[0];

        // Without the fix native max was 100 (the full Max), letting the start
        // thumb drag past the end thumb (40) before the C# clamp snapped it back.
        Assert.Equal(40d, Attr(start, "max"));
        // ...and it must agree with the aria value that was already correct.
        Assert.Equal(Attr(start, "aria-valuemax"), Attr(start, "max"));
    }

    [Fact]
    public void EndInput_NativeMin_StopsAtReachableBound_NotFullMin()
    {
        var cut = _ctx.Render<L.Slider>(p => p
            .Add(s => s.IsRange, true)
            .Add(s => s.Min, 0d)
            .Add(s => s.Max, 100d)
            .Add(s => s.Value, 30d)
            .Add(s => s.ValueEnd, 80d));

        var end = cut.FindAll("input[type=range]")[1];

        // Without the fix native min was 0 (the full Min), letting the end thumb
        // drag below the start thumb (30) before the C# clamp snapped it back.
        Assert.Equal(30d, Attr(end, "min"));
        Assert.Equal(Attr(end, "aria-valuemin"), Attr(end, "min"));
    }

    [Fact]
    public void NativeBounds_RespectMinStepsBetweenThumbs()
    {
        // MinGap = MinStepsBetweenThumbs * Step = 2 * 5 = 10. The start thumb may
        // only reach ValueEnd-10 and the end thumb only Value+10.
        var cut = _ctx.Render<L.Slider>(p => p
            .Add(s => s.IsRange, true)
            .Add(s => s.Min, 0d)
            .Add(s => s.Max, 100d)
            .Add(s => s.Step, 5d)
            .Add(s => s.MinStepsBetweenThumbs, 2)
            .Add(s => s.Value, 30d)
            .Add(s => s.ValueEnd, 60d));

        var inputs = cut.FindAll("input[type=range]");
        var start = inputs[0];
        var end = inputs[1];

        Assert.Equal(50d, Attr(start, "max")); // 60 - 10
        Assert.Equal(40d, Attr(end, "min"));   // 30 + 10
    }

    [Fact]
    public void StartNativeMax_Tracks_ValueEnd_Change()
    {
        var cut = _ctx.Render<L.Slider>(p => p
            .Add(s => s.IsRange, true)
            .Add(s => s.Min, 0d)
            .Add(s => s.Max, 100d)
            .Add(s => s.Value, 20d)
            .Add(s => s.ValueEnd, 40d));

        Assert.Equal(40d, Attr(cut.FindAll("input[type=range]")[0], "max"));

        // Moving the end thumb out should free the start thumb's reachable bound.
        cut.Render(p => p.Add(s => s.ValueEnd, 70d));

        Assert.Equal(70d, Attr(cut.FindAll("input[type=range]")[0], "max"));
    }
}
