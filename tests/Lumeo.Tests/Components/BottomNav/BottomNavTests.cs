using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;

namespace Lumeo.Tests.Components.BottomNav;

public class BottomNavTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public BottomNavTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Nav_Element_With_Navigation_Role()
    {
        var cut = _ctx.Render<Lumeo.BottomNav>();

        var nav = cut.Find("nav");
        Assert.Equal("navigation", nav.GetAttribute("role"));
    }

    [Fact]
    public void Default_AriaLabel_Is_Bottom_Navigation()
    {
        var cut = _ctx.Render<Lumeo.BottomNav>();

        Assert.Equal("Bottom navigation", cut.Find("nav").GetAttribute("aria-label"));
    }

    [Fact]
    public void Custom_AriaLabel_Is_Applied()
    {
        var cut = _ctx.Render<Lumeo.BottomNav>(p => p
            .Add(n => n.AriaLabel, "Main menu"));

        Assert.Equal("Main menu", cut.Find("nav").GetAttribute("aria-label"));
    }

    [Fact]
    public void Fixed_True_Includes_Fixed_Positioning_Classes()
    {
        var cut = _ctx.Render<Lumeo.BottomNav>(p => p
            .Add(n => n.Fixed, true));

        var cls = cut.Find("nav").GetAttribute("class");
        Assert.Contains("fixed", cls);
        Assert.Contains("bottom-0", cls);
    }

    [Fact]
    public void Fixed_False_Uses_Full_Width_Instead()
    {
        var cut = _ctx.Render<Lumeo.BottomNav>(p => p
            .Add(n => n.Fixed, false));

        var cls = cut.Find("nav").GetAttribute("class");
        Assert.DoesNotContain("fixed", cls);
        Assert.Contains("w-full", cls);
    }

    [Fact]
    public void Default_Variant_Has_Background_And_Top_Border()
    {
        var cut = _ctx.Render<Lumeo.BottomNav>(p => p
            .Add(n => n.Variant, Lumeo.BottomNav.BottomNavVariant.Default));

        var cls = cut.Find("nav").GetAttribute("class");
        Assert.Contains("bg-background", cls);
        Assert.Contains("border-t", cls);
    }

    [Fact]
    public void Pill_Variant_Uses_Floating_Padding()
    {
        var cut = _ctx.Render<Lumeo.BottomNav>(p => p
            .Add(n => n.Variant, Lumeo.BottomNav.BottomNavVariant.Pill));

        var navCls = cut.Find("nav").GetAttribute("class");
        Assert.Contains("px-4", navCls);
        Assert.Contains("pb-3", navCls);

        // Inner pill container should have rounded-full
        var inner = cut.Find("nav > div");
        Assert.Contains("rounded-full", inner.GetAttribute("class"));
    }

    [Fact]
    public void ChildContent_Renders_Inside_Inner_Container()
    {
        var cut = _ctx.Render<Lumeo.BottomNav>(p => p
            .AddChildContent("<span data-testid='child'>Item</span>"));

        Assert.Contains("Item", cut.Markup);
    }

    [Fact]
    public void Custom_Class_Is_Appended_To_Nav()
    {
        var cut = _ctx.Render<Lumeo.BottomNav>(p => p
            .Add(n => n.Class, "my-nav-class"));

        Assert.Contains("my-nav-class", cut.Find("nav").GetAttribute("class"));
    }

    [Fact]
    public void Additional_Attributes_Are_Forwarded()
    {
        var cut = _ctx.Render<Lumeo.BottomNav>(p => p
            .Add(n => n.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "bn"
            }));

        Assert.Equal("bn", cut.Find("nav").GetAttribute("data-testid"));
    }
}
