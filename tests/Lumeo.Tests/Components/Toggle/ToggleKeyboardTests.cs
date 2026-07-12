using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Toggle;

/// <summary>
/// Toggle is a native &lt;button aria-pressed&gt; — Enter/Space activation is free via
/// browser default button semantics (ToggleTests/ToggleDataStateTests already pin the
/// click-driven Pressed/data-state outcome, so this file targets the two keyboard-
/// specific surfaces that were previously untested: a visible focus indicator (WCAG
/// 2.4.7 — required because a keyboard user tabbing to the toggle has no pointer hover
/// to rely on) and the accessible name an icon-only toggle needs so AT announces
/// something when keyboard focus lands on it.
/// </summary>
public class ToggleKeyboardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public ToggleKeyboardTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Toggle_Has_A_Visible_Focus_Ring_For_Keyboard_Users()
    {
        var cut = _ctx.Render<L.Toggle>(p => p.AddChildContent("B"));

        var cls = cut.Find("button").GetAttribute("class") ?? "";
        Assert.Contains("focus-visible:ring-2", cls);
        Assert.Contains("focus-visible:ring-ring", cls);
    }

    [Fact]
    public void AriaLabel_Renders_As_The_Accessible_Name_For_An_Icon_Only_Toggle()
    {
        var cut = _ctx.Render<L.Toggle>(p => p.Add(t => t.AriaLabel, "Bold"));

        Assert.Equal("Bold", cut.Find("button").GetAttribute("aria-label"));
    }
}
