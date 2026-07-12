using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Alert;

/// <summary>
/// The dismiss control is a native &lt;button @onclick="Dismiss"&gt;, only rendered when
/// IsDismissible is set — so Enter/Space activation is free via the browser's default
/// button semantics (AlertTests already pins the click-driven dismiss outcome via
/// .Click(), which is the exact handler a synthesized Enter/Space keydown runs; bUnit
/// cannot dispatch a real native keydown-to-click translation). This file adds the two
/// keyboard-specific angles AlertTests does not cover: the dismiss button carries no
/// tabindex override (so native Tab actually reaches it), and — the flip side — a
/// non-dismissible alert introduces zero extra tab stops for a keyboard user to trip
/// over.
/// </summary>
public class AlertKeyboardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public AlertKeyboardTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Dismissible_Alert_Renders_A_Native_Tab_Reachable_Dismiss_Button()
    {
        var cut = _ctx.Render<L.Alert>(p => p.Add(a => a.IsDismissible, true));

        var button = cut.Find("button[aria-label='Dismiss']");
        Assert.Equal("button", button.GetAttribute("type"));
        Assert.False(button.HasAttribute("tabindex"));
        Assert.False(button.HasAttribute("disabled"));
    }

    [Fact]
    public void NonDismissible_Alert_Introduces_No_Extra_Tab_Stop()
    {
        var cut = _ctx.Render<L.Alert>(p => p.Add(a => a.IsDismissible, false));

        Assert.Empty(cut.FindAll("button"));
    }
}
