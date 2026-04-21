using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Lumeo.Tests.Components.ShimmerButton;

public class ShimmerButtonTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ShimmerButtonTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_As_Button()
    {
        var cut = _ctx.Render<Lumeo.ShimmerButton>(p => p
            .AddChildContent("Click me"));

        Assert.NotNull(cut.Find("button"));
    }

    [Fact]
    public void ChildContent_Renders()
    {
        var cut = _ctx.Render<Lumeo.ShimmerButton>(p => p
            .AddChildContent("Click me"));

        Assert.Contains("Click me", cut.Markup);
    }

    [Fact]
    public async Task OnClick_Fires_When_Clicked()
    {
        var fired = false;
        var cut = _ctx.Render<Lumeo.ShimmerButton>(p => p
            .Add(b => b.OnClick, EventCallback.Factory.Create<MouseEventArgs>(this, _ => fired = true))
            .AddChildContent("X"));

        await cut.Find("button").ClickAsync(new MouseEventArgs());
        Assert.True(fired);
    }

    [Fact]
    public void Shimmer_True_Adds_Shimmer_Class()
    {
        var cut = _ctx.Render<Lumeo.ShimmerButton>(p => p
            .Add(b => b.Shimmer, true)
            .AddChildContent("X"));

        Assert.Contains("lumeo-shimmer", cut.Find("button").GetAttribute("class"));
    }

    [Fact]
    public void Shimmer_False_Omits_Shimmer_Class()
    {
        var cut = _ctx.Render<Lumeo.ShimmerButton>(p => p
            .Add(b => b.Shimmer, false)
            .AddChildContent("X"));

        Assert.DoesNotContain("lumeo-shimmer", cut.Find("button").GetAttribute("class"));
    }

    [Fact]
    public void ShimmerColor_Applies_To_Style()
    {
        var cut = _ctx.Render<Lumeo.ShimmerButton>(p => p
            .Add(b => b.ShimmerColor, "#fff")
            .AddChildContent("X"));

        Assert.Contains("--lumeo-shimmer-color: #fff", cut.Find("button").GetAttribute("style"));
    }

    [Fact]
    public void Disabled_True_Disables_Button()
    {
        var cut = _ctx.Render<Lumeo.ShimmerButton>(p => p
            .Add(b => b.Disabled, true)
            .AddChildContent("X"));

        Assert.True(cut.Find("button").HasAttribute("disabled"));
    }

    [Fact]
    public void Variant_Destructive_Applies_Destructive_Classes()
    {
        var cut = _ctx.Render<Lumeo.ShimmerButton>(p => p
            .Add(b => b.Variant, Lumeo.Button.ButtonVariant.Destructive)
            .AddChildContent("X"));

        Assert.Contains("bg-destructive", cut.Find("button").GetAttribute("class"));
    }

    [Fact]
    public void Size_Sm_Applies_Small_Classes()
    {
        var cut = _ctx.Render<Lumeo.ShimmerButton>(p => p
            .Add(b => b.Size, Lumeo.Button.ButtonSize.Sm)
            .AddChildContent("X"));

        Assert.Contains("h-8", cut.Find("button").GetAttribute("class"));
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.ShimmerButton>(p => p
            .Add(b => b.Class, "my-sb")
            .AddChildContent("X"));

        Assert.Contains("my-sb", cut.Find("button").GetAttribute("class"));
    }

    [Fact]
    public void Additional_Attributes_Forward()
    {
        var cut = _ctx.Render<Lumeo.ShimmerButton>(p => p
            .Add(b => b.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "sb"
            })
            .AddChildContent("X"));

        Assert.Equal("sb", cut.Find("button").GetAttribute("data-testid"));
    }
}
