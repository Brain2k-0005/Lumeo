using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Button;

/// <summary>
/// Tests for the UX audit additions: <c>cursor-pointer</c> default,
/// <c>disabled</c>-driven cursor, and the opt-in <see cref="Lumeo.Button.ButtonPressEffect"/>
/// parameter (classes only — the JS ripple attach is not exercised in bUnit).
/// </summary>
public class ButtonPressEffectTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ButtonPressEffectTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Button_Has_Cursor_Pointer_By_Default()
    {
        var cut = _ctx.Render<Lumeo.Button>(p => p.AddChildContent("Click"));

        Assert.Contains("cursor-pointer", cut.Find("button").GetAttribute("class"));
    }

    [Fact]
    public void Button_Disabled_Has_Cursor_Not_Allowed()
    {
        var cut = _ctx.Render<Lumeo.Button>(p => p
            .Add(b => b.Disabled, true)
            .AddChildContent("Disabled"));

        var cls = cut.Find("button").GetAttribute("class");
        Assert.Contains("disabled:cursor-not-allowed", cls);
    }

    [Fact]
    public void Button_PressEffect_None_By_Default()
    {
        var cut = _ctx.Render<Lumeo.Button>(p => p.AddChildContent("None"));

        var cls = cut.Find("button").GetAttribute("class");
        Assert.DoesNotContain("lumeo-press-scale", cls);
        Assert.DoesNotContain("lumeo-press-brightness", cls);
        Assert.DoesNotContain("lumeo-press-ripple", cls);
    }

    [Fact]
    public void Button_PressEffect_Scale_Adds_Class()
    {
        var cut = _ctx.Render<Lumeo.Button>(p => p
            .Add(b => b.PressEffect, Lumeo.Button.ButtonPressEffect.Scale)
            .AddChildContent("Scale"));

        Assert.Contains("lumeo-press-scale", cut.Find("button").GetAttribute("class"));
    }

    [Fact]
    public void Button_PressEffect_Brightness_Adds_Class()
    {
        var cut = _ctx.Render<Lumeo.Button>(p => p
            .Add(b => b.PressEffect, Lumeo.Button.ButtonPressEffect.Brightness)
            .AddChildContent("Brightness"));

        Assert.Contains("lumeo-press-brightness", cut.Find("button").GetAttribute("class"));
    }

    [Fact]
    public void Button_PressEffect_Ripple_Adds_Class()
    {
        // JS attach is not observable in bUnit (loose-mode mock returns defaults),
        // but the class still lands on the rendered element.
        var cut = _ctx.Render<Lumeo.Button>(p => p
            .Add(b => b.PressEffect, Lumeo.Button.ButtonPressEffect.Ripple)
            .AddChildContent("Ripple"));

        Assert.Contains("lumeo-press-ripple", cut.Find("button").GetAttribute("class"));
    }

    [Fact]
    public void Button_PressEffect_Scale_Does_Not_Add_Other_Effects()
    {
        var cut = _ctx.Render<Lumeo.Button>(p => p
            .Add(b => b.PressEffect, Lumeo.Button.ButtonPressEffect.Scale)
            .AddChildContent("Scale"));

        var cls = cut.Find("button").GetAttribute("class");
        Assert.Contains("lumeo-press-scale", cls);
        Assert.DoesNotContain("lumeo-press-brightness", cls);
        Assert.DoesNotContain("lumeo-press-ripple", cls);
    }
}
