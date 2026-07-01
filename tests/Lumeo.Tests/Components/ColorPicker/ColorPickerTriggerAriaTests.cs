using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.ColorPicker;

/// <summary>
/// Battle-wave2 #28 (medium, keyboard-a11y) — the trigger button opened a
/// role="dialog" popover but never told assistive tech about it: it lacked
/// aria-haspopup, aria-expanded, and aria-controls. Screen-reader users got no
/// announcement that the button is a disclosure for a dialog, nor whether it is
/// currently open. The button now carries aria-haspopup="dialog", a live
/// aria-expanded reflecting Open, and aria-controls pointing at the popover's
/// stable content id while open. These tests assert that rendered-markup ARIA
/// surface (bUnit cannot move real focus, so we assert the attributes directly).
/// </summary>
public class ColorPickerTriggerAriaTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public ColorPickerTriggerAriaTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Trigger_Advertises_HasPopup_Dialog()
    {
        var cut = _ctx.Render<L.ColorPicker>();

        var trigger = cut.Find("button");
        // Without the fix the trigger had no aria-haspopup, so screen readers
        // never announced it as a dialog disclosure.
        Assert.Equal("dialog", trigger.GetAttribute("aria-haspopup"));
    }

    [Fact]
    public void Trigger_Reports_Collapsed_When_Closed()
    {
        var cut = _ctx.Render<L.ColorPicker>(p => p.Add(c => c.Open, false));

        var trigger = cut.Find("button");
        Assert.Equal("false", trigger.GetAttribute("aria-expanded"));
        // No popover exists while closed, so the control reference is dropped.
        Assert.Null(trigger.GetAttribute("aria-controls"));
    }

    [Fact]
    public void Trigger_Reports_Expanded_And_Controls_The_Popover_When_Open()
    {
        var cut = _ctx.Render<L.ColorPicker>(p => p.Add(c => c.Open, true));

        var trigger = cut.Find("button");
        Assert.Equal("true", trigger.GetAttribute("aria-expanded"));

        // aria-controls must point at the actual popover element so AT can
        // associate the disclosure with its dialog.
        var controls = trigger.GetAttribute("aria-controls");
        Assert.False(string.IsNullOrEmpty(controls));

        var dialog = cut.Find("[role='dialog']");
        Assert.Equal(dialog.Id, controls);
    }

    [Fact]
    public void Trigger_AriaExpanded_Tracks_Open_State_Change()
    {
        var cut = _ctx.Render<L.ColorPicker>(p => p.Add(c => c.Open, false));
        Assert.Equal("false", cut.Find("button").GetAttribute("aria-expanded"));

        cut.Render(p => p.Add(c => c.Open, true));

        Assert.Equal("true", cut.Find("button").GetAttribute("aria-expanded"));
    }
}
