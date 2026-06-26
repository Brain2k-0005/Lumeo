using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.ColorPicker;

/// <summary>
/// Battle-wave2 #147 (low, state-on-data-change) — the in-progress hue / SV
/// position was silently re-derived (and could JUMP) when the parent fed the
/// value back in a different string form than the component emitted. The old
/// OnParametersSet compared Value against _lastSyncedValue with a raw
/// OrdinalIgnoreCase string compare, so a normalised round-trip (e.g. our
/// "#FFFFFF" coming back as "rgb(255, 255, 255)") looked like a real change and
/// re-ran SyncStateFromHex, whose RgbToHsv re-derivation reset the ambiguous
/// hue of an achromatic colour back to 0. The fix compares colours by PARSED
/// equivalence and skips the re-sync when the incoming colour resolves to the
/// RGB(+alpha) already held, preserving _h/_s/_v.
/// </summary>
public class ColorPickerValueReprojectionTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public ColorPickerValueReprojectionTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Hue_Survives_Same_Colour_Fed_Back_In_A_Different_String_Form()
    {
        // White is achromatic: its hue is ambiguous, so the user's chosen hue
        // lives only in _h (the RGB stays #FFFFFF). Render open on white.
        var cut = _ctx.Render<L.ColorPicker>(p => p
            .Add(c => c.Open, true)
            .Add(c => c.Value, "#FFFFFF"));

        // Move the hue slider to 200 while the colour is white. The component
        // keeps RGB = white (S=0) but records _h = 200 and re-emits "#FFFFFF".
        var hue = cut.Find(".lumeo-cp-hue");
        hue.Input("200");

        hue = cut.Find(".lumeo-cp-hue");
        Assert.Equal("200", hue.GetAttribute("aria-valuenow"));

        // The parent normalises and feeds the SAME colour back in a different
        // string form ("rgb(255, 255, 255)" vs the emitted "#FFFFFF").
        cut.Render(p => p
            .Add(c => c.Open, true)
            .Add(c => c.Value, "rgb(255, 255, 255)"));

        // Without the fix the raw string differs from _lastSyncedValue, so
        // SyncStateFromHex re-derives the hue back to 0. With the fix the
        // parsed-equivalence guard keeps the user's in-progress hue at 200.
        hue = cut.Find(".lumeo-cp-hue");
        Assert.Equal("200", hue.GetAttribute("aria-valuenow"));
    }
}
