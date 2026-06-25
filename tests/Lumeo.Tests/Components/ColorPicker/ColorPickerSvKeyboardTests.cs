using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.ColorPicker;

/// <summary>
/// Battle-wave2 #1 (high, keyboard-a11y) — the Saturation/Value 2D canvas was a
/// pointer-drag-only surface (Interop.RegisterSvDrag), so keyboard-only users
/// could never reach the inner S/V axis (WCAG 2.1.1). The canvas is now a
/// focusable role="slider" whose Arrow / Home / End / PageUp / PageDown keys
/// adjust _s/_v and commit via ApplyHsv(). These tests assert both the ARIA
/// surface and that an arrow keystroke actually mutates the bound colour.
/// </summary>
public class ColorPickerSvKeyboardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public ColorPickerSvKeyboardTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Sv_Canvas_Is_A_Focusable_Slider()
    {
        var cut = _ctx.Render<L.ColorPicker>(p => p.Add(c => c.Open, true));

        var sv = cut.Find("[id^='colorpicker-sv-']");
        // Without the fix the SV div had no role/tabindex — pointer-only.
        Assert.Equal("slider", sv.GetAttribute("role"));
        Assert.Equal("0", sv.GetAttribute("tabindex"));
        Assert.NotNull(sv.GetAttribute("aria-valuetext"));
    }

    [Fact]
    public void ArrowLeft_On_Sv_Canvas_Changes_The_Bound_Value()
    {
        // Start at pure red (H=0, S=100, V=100). Decreasing saturation by one
        // step pulls the colour off the fully-saturated edge, so RGB changes.
        string? committed = null;
        var cb = EventCallback.Factory.Create<string>(_ctx, (string v) => committed = v);
        var cut = _ctx.Render<L.ColorPicker>(p => p
            .Add(c => c.Open, true)
            .Add(c => c.Value, "#FF0000")
            .Add(c => c.ValueChanged, cb));

        var sv = cut.Find("[id^='colorpicker-sv-']");
        sv.KeyDown(new KeyboardEventArgs { Key = "ArrowLeft" });

        // The keystroke must commit a new colour through ValueChanged.
        Assert.NotNull(committed);
        Assert.NotEqual("#FF0000", committed);
    }

    [Fact]
    public void Sv_Canvas_Aria_ValueNow_Tracks_Value_Axis_After_ArrowDown()
    {
        // White is V=100; one ArrowDown drops V to 99 and re-renders the
        // aria-valuenow, proving the keyboard handler moved the value axis.
        var cut = _ctx.Render<L.ColorPicker>(p => p
            .Add(c => c.Open, true)
            .Add(c => c.Value, "#FFFFFF"));

        var sv = cut.Find("[id^='colorpicker-sv-']");
        Assert.Equal("100", sv.GetAttribute("aria-valuenow"));

        sv.KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });

        sv = cut.Find("[id^='colorpicker-sv-']");
        Assert.Equal("99", sv.GetAttribute("aria-valuenow"));
    }
}
