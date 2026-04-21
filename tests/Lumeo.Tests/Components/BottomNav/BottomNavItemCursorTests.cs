using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.BottomNav;

/// <summary>
/// UX-audit regression tests: every rendering of <see cref="BottomNavItem"/>
/// (button and anchor flavours) must include <c>cursor-pointer</c>.
/// </summary>
public class BottomNavItemCursorTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public BottomNavItemCursorTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Button_Variant_Has_Cursor_Pointer()
    {
        var cut = _ctx.Render<BottomNavItem>(p => p
            .Add(i => i.Label, "Home"));

        Assert.Contains("cursor-pointer", cut.Find("button").GetAttribute("class"));
    }

    [Fact]
    public void Anchor_Variant_Has_Cursor_Pointer()
    {
        var cut = _ctx.Render<BottomNavItem>(p => p
            .Add(i => i.Href, "/home")
            .Add(i => i.Label, "Home"));

        Assert.Contains("cursor-pointer", cut.Find("a").GetAttribute("class"));
    }

    [Fact]
    public void PressEffect_Scale_Adds_Class()
    {
        var cut = _ctx.Render<BottomNavItem>(p => p
            .Add(i => i.Label, "Home")
            .Add(i => i.PressEffect, Lumeo.Button.ButtonPressEffect.Scale));

        Assert.Contains("lumeo-press-scale", cut.Find("button").GetAttribute("class"));
    }
}
