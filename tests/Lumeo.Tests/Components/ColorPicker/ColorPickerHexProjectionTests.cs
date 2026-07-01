using AngleSharp.Dom;
using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.ColorPicker;

/// <summary>
/// Battle-wave2 #27 (medium, edge-data) — the bound Value is an rgba(...) string
/// whenever ShowAlpha is on and alpha drops below 100. That raw rgba string was
/// rendered into the *hex* &lt;input&gt; (value="@DisplayHex") and the trigger
/// caption, where it was both nonsensical AND silently chopped by the input's
/// maxlength="9" (e.g. "rgba(255, 0, 0, 0.5)" became "rgba(255,"). The fix
/// projects a dedicated hex string (DisplayHexField): #RRGGBBAA when alpha is
/// partial — exactly 9 chars so it fits maxlength and round-trips through
/// OnHexInput/SyncStateFromHex — otherwise the compact #RRGGBB. These tests
/// reproduce the partial-alpha edge input and assert the hex input / trigger
/// render a real hex, never the raw rgba string.
/// </summary>
public class ColorPickerHexProjectionTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public ColorPickerHexProjectionTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static IElement HexInput(IRenderedComponent<L.ColorPicker> cut)
        => cut.Find("input[type='text'][maxlength='9']");

    [Fact]
    public void Hex_Input_Renders_RRGGBBAA_Not_Raw_Rgba_When_Alpha_Is_Partial()
    {
        // Open on opaque red with the alpha channel enabled.
        var cut = _ctx.Render<L.ColorPicker>(p => p
            .Add(c => c.ShowAlpha, true)
            .Add(c => c.Open, true)
            .Add(c => c.Value, "#FF0000"));

        // Drop alpha to 50%. The component now emits Value = "rgba(255, 0, 0, 0.5)".
        cut.Find(".lumeo-cp-slider-alpha").Input("50");

        var hex = HexInput(cut);
        var value = hex.GetAttribute("value");

        // Without the fix the hex input rendered the raw rgba string (chopped by
        // maxlength to something like "rgba(255,"); with the fix it is a real
        // 9-char #RRGGBBAA hex.
        Assert.False(string.IsNullOrEmpty(value));
        Assert.StartsWith("#", value);
        Assert.DoesNotContain("rgba", value);
        Assert.DoesNotContain("(", value);
        // #RRGGBBAA = '#' + 8 hex digits.
        Assert.Equal(9, value!.Length);
        // Red at 50% alpha → FF0000 with alpha 0x80 (128/255 ≈ 50%).
        Assert.Equal("#FF000080", value);
    }

    [Fact]
    public void Hex_Input_Value_Fits_Within_MaxLength()
    {
        var cut = _ctx.Render<L.ColorPicker>(p => p
            .Add(c => c.ShowAlpha, true)
            .Add(c => c.Open, true)
            .Add(c => c.Value, "#3366CC"));

        cut.Find(".lumeo-cp-slider-alpha").Input("25");

        var hex = HexInput(cut);
        var value = hex.GetAttribute("value")!;
        var maxLength = int.Parse(hex.GetAttribute("maxlength")!);

        // The whole projected value must survive the native maxlength — i.e. it
        // is never truncated by the browser. The raw rgba string (length 20+)
        // would have been chopped.
        Assert.True(value.Length <= maxLength,
            $"hex value '{value}' (len {value.Length}) exceeds maxlength {maxLength}");
    }

    [Fact]
    public void Hex_Input_Stays_Compact_Hex_When_Alpha_Is_Full()
    {
        // Normal path must be preserved exactly: at full alpha the hex input
        // shows the compact #RRGGBB (no trailing alpha pair).
        var cut = _ctx.Render<L.ColorPicker>(p => p
            .Add(c => c.ShowAlpha, true)
            .Add(c => c.Open, true)
            .Add(c => c.Value, "#FF0000"));

        var value = HexInput(cut).GetAttribute("value");

        Assert.Equal("#FF0000", value);
    }

    [Fact]
    public void Trigger_Caption_Shows_Hex_Not_Raw_Rgba_When_Alpha_Is_Partial()
    {
        var cut = _ctx.Render<L.ColorPicker>(p => p
            .Add(c => c.ShowAlpha, true)
            .Add(c => c.Open, true)
            .Add(c => c.Value, "#00FF00"));

        cut.Find(".lumeo-cp-slider-alpha").Input("40");

        // The trigger caption is the font-mono <span> inside the trigger button.
        var caption = cut.Find("button .font-mono");

        Assert.DoesNotContain("rgba", caption.TextContent);
        Assert.StartsWith("#", caption.TextContent.Trim());
    }
}
