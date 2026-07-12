using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.ImageCompare;

/// <summary>
/// SPECIAL scout finding re-verified against the actual source: ImageCompare's
/// divider is a real &lt;input type="range" role="slider"&gt;, not a custom
/// div — verified by reading ImageCompare.razor before writing anything. A
/// native range input gets Left/Right/Up/Down/Home/End/PageUp/PageDown
/// keyboard travel from the BROWSER ITSELF (the same free semantics the
/// `Slider` component relies on — see SliderKeyboardTests / the gap-data note
/// "browser supplies full native ... keyboard support ... no custom key
/// handler is coded"). role="slider" on it is a no-op (matches the implicit
/// role); it does not disable native keyboard handling. There is therefore NO
/// product fix to make here — the "zero key handling" premise in the SPECIAL
/// brief was a false positive from a plain @onkeydown/KeyboardEventArgs grep
/// that doesn't know about native &lt;input type="range"&gt; semantics.
///
/// These tests verify (a) the element really is a native, Tab-reachable range
/// input (not a div impersonating one) and (b) OnInput — the ONE piece of
/// Lumeo-owned logic in the keyboard path — correctly clamps/reflects every
/// value the browser's native Home/End/Arrow handling can produce, mirroring
/// how SliderKeyboardTests drives `<input type=range>` via .Input() to stand
/// in for real keyboard travel (bUnit has no browser to synthesize the actual
/// key-to-value translation).
/// </summary>
public class ImageCompareKeyboardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ImageCompareKeyboardTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.ImageCompare> Render(int initialPosition = 50)
        => _ctx.Render<L.ImageCompare>(p => p
            .Add(c => c.BeforeSrc, "/b.jpg")
            .Add(c => c.AfterSrc, "/a.jpg")
            .Add(c => c.InitialPosition, initialPosition));

    [Fact]
    public void Slider_Is_A_Native_Range_Input_Not_A_Div()
    {
        var cut = Render();
        var slider = cut.Find("input[type='range']");

        Assert.Equal("input", slider.TagName.ToLowerInvariant());
        Assert.Equal("range", slider.GetAttribute("type"));
    }

    [Fact]
    public void Slider_Has_No_Tabindex_Override_And_Stays_In_The_Native_Tab_Order()
    {
        // A native <input> needs NO explicit tabindex to be focusable — an
        // explicit tabindex="-1" would be the only way to REMOVE it from Tab
        // order, and this component sets none.
        var cut = Render();
        var slider = cut.Find("input[type='range']");

        Assert.NotEqual("-1", slider.GetAttribute("tabindex"));
    }

    [Fact]
    public void Home_Key_Equivalent_Jumps_The_Divider_To_Zero()
    {
        // A real Home keypress on a native range input fires the browser's
        // built-in jump-to-min, which surfaces as an `input` event carrying
        // min ("0") — exactly what .Input("0") simulates here.
        var cut = Render(initialPosition: 50);

        cut.Find("input[type='range']").Input("0");

        var slider = cut.Find("input[type='range']");
        Assert.Equal("0", slider.GetAttribute("aria-valuenow"));
        Assert.Contains("inset(0 100.00% 0 0)", cut.Markup); // fully clipped — divider at the far edge
    }

    [Fact]
    public void End_Key_Equivalent_Jumps_The_Divider_To_A_Hundred()
    {
        var cut = Render(initialPosition: 50);

        cut.Find("input[type='range']").Input("100");

        var slider = cut.Find("input[type='range']");
        Assert.Equal("100", slider.GetAttribute("aria-valuenow"));
        Assert.Contains("inset(0 0.00% 0 0)", cut.Markup); // no clip — divider at the opposite edge
    }

    [Fact]
    public void Single_Arrow_Step_Equivalent_Moves_The_Divider_By_The_Configured_Step()
    {
        // ArrowRight on a native range input advances by its `step` (0.1 here,
        // per the markup's step="0.1"). Verify the fractional move is reflected
        // precisely in the clip-path (aria-valuenow rounds for the spoken
        // value, but the visual divider must track the exact position).
        var cut = Render(initialPosition: 50);

        cut.Find("input[type='range']").Input("50.1");

        Assert.Contains("left: 50.10%", cut.Markup);
        Assert.Contains("inset(0 49.90% 0 0)", cut.Markup);
    }

    [Fact]
    public void ValueChange_Beyond_The_Native_Bounds_Still_Clamps_Like_Home_End_Would()
    {
        // Defensive mirror of the existing "beyond range clamps to 100" test,
        // covering the symmetric lower bound (a malformed/out-of-range value
        // must never drive the divider negative).
        var cut = Render(initialPosition: 50);

        cut.Find("input[type='range']").Input("-25");

        Assert.Equal("0", cut.Find("input[type='range']").GetAttribute("aria-valuenow"));
    }
}
