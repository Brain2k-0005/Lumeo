using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.ShimmerButton;

public class ShimmerButtonPressEffectTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ShimmerButtonPressEffectTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void ShimmerButton_Has_Cursor_Pointer_By_Default()
    {
        var cut = _ctx.Render<Lumeo.ShimmerButton>(p => p.AddChildContent("Go"));

        Assert.Contains("cursor-pointer", cut.Find("button").GetAttribute("class"));
    }

    [Fact]
    public void ShimmerButton_PressEffect_Brightness_Adds_Class()
    {
        var cut = _ctx.Render<Lumeo.ShimmerButton>(p => p
            .Add(b => b.PressEffect, Lumeo.Button.ButtonPressEffect.Brightness)
            .AddChildContent("Go"));

        Assert.Contains("lumeo-press-brightness", cut.Find("button").GetAttribute("class"));
    }
}
