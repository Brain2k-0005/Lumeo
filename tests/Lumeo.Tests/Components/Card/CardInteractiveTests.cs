using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Card;

/// <summary>
/// Covers the v2.0 UX-audit additions to <see cref="Lumeo.Card"/>:
/// <c>OnClick</c> turns the card into a button-role surface with
/// <c>cursor-pointer</c> + focus-visible ring and honours
/// <see cref="Lumeo.Button.ButtonPressEffect"/>.
/// </summary>
public class CardInteractiveTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public CardInteractiveTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Card_Without_OnClick_Has_No_Cursor_Pointer()
    {
        var cut = _ctx.Render<L.Card>(p => p.AddChildContent("Static"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.DoesNotContain("cursor-pointer", cls);
        // Non-interactive cards should not receive a role="button".
        Assert.NotEqual("button", cut.Find("div").GetAttribute("role"));
    }

    [Fact]
    public void Card_With_OnClick_Has_Cursor_Pointer_And_Button_Role()
    {
        var cut = _ctx.Render<L.Card>(p => p
            .Add(c => c.OnClick, _ => { })
            .AddChildContent("Clickable"));

        var div = cut.Find("div");
        Assert.Contains("cursor-pointer", div.GetAttribute("class"));
        Assert.Equal("button", div.GetAttribute("role"));
        Assert.Equal("0", div.GetAttribute("tabindex"));
    }

    [Fact]
    public void Card_With_OnClick_Fires_Click()
    {
        var clicked = false;
        var cut = _ctx.Render<L.Card>(p => p
            .Add(c => c.OnClick, _ => clicked = true)
            .AddChildContent("Clickable"));

        cut.Find("div").Click();
        Assert.True(clicked);
    }

    [Fact]
    public void Card_With_OnClick_Plus_Scale_Adds_Press_Class()
    {
        var cut = _ctx.Render<L.Card>(p => p
            .Add(c => c.OnClick, _ => { })
            .Add(c => c.PressEffect, L.Button.ButtonPressEffect.Scale)
            .AddChildContent("Scale"));

        Assert.Contains("lumeo-press-scale", cut.Find("div").GetAttribute("class"));
    }

    [Fact]
    public void Card_Without_OnClick_Ignores_PressEffect()
    {
        // Non-interactive card shouldn't receive press classes even if
        // the consumer passes PressEffect by accident.
        var cut = _ctx.Render<L.Card>(p => p
            .Add(c => c.PressEffect, L.Button.ButtonPressEffect.Scale)
            .AddChildContent("Static"));

        Assert.DoesNotContain("lumeo-press-scale", cut.Find("div").GetAttribute("class"));
    }
}
