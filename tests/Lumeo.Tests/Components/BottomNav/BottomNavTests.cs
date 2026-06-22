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

    [Fact]
    public void ReserveSpace_False_By_Default_Renders_No_Spacer()
    {
        var cut = _ctx.Render<Lumeo.BottomNav>();

        Assert.Empty(cut.FindAll(".lumeo-bottom-nav-spacer"));
    }

    [Fact]
    public void ReserveSpace_True_When_Fixed_Renders_AriaHidden_Spacer()
    {
        var cut = _ctx.Render<Lumeo.BottomNav>(p => p
            .Add(n => n.Fixed, true)
            .Add(n => n.ReserveSpace, true));

        var spacer = cut.Find(".lumeo-bottom-nav-spacer");
        Assert.Equal("true", spacer.GetAttribute("aria-hidden"));

        // Pin the reserved height: the default token (4rem — the real Default nav
        // content height is 3.875rem) + the safe-area inset exactly once. Guards
        // against a regression to the under-reserving 3.5rem the first cut shipped.
        var style = spacer.GetAttribute("style") ?? "";
        Assert.Contains("var(--lumeo-bottom-nav-height, 4rem)", style);
        Assert.Contains("env(safe-area-inset-bottom", style);
        Assert.DoesNotContain("3.5rem", style);
    }

    [Fact]
    public void ReserveSpace_Spacer_Is_Sibling_Immediately_Before_Nav()
    {
        var cut = _ctx.Render<Lumeo.BottomNav>(p => p
            .Add(n => n.Fixed, true)
            .Add(n => n.ReserveSpace, true));

        var spacer = cut.Find(".lumeo-bottom-nav-spacer");
        Assert.Equal("NAV", spacer.NextElementSibling?.TagName);
    }

    [Fact]
    public void ReserveSpace_Pill_Variant_Reserves_Extra_Float_Gap()
    {
        // The Pill bar floats with a pb-3 (0.75rem) gap below it, so its spacer
        // must reserve more than the Default variant — not silently share the token.
        var cut = _ctx.Render<Lumeo.BottomNav>(p => p
            .Add(n => n.Fixed, true)
            .Add(n => n.ReserveSpace, true)
            .Add(n => n.Variant, Lumeo.BottomNav.BottomNavVariant.Pill));

        var style = cut.Find(".lumeo-bottom-nav-spacer").GetAttribute("style") ?? "";
        Assert.Contains("0.75rem", style);
    }

    [Fact]
    public void ReserveSpace_Ignored_When_Not_Fixed()
    {
        // An inline nav already occupies flow space, so reserving more would
        // double-pad. The spacer must only appear for a fixed (overlapping) bar.
        var cut = _ctx.Render<Lumeo.BottomNav>(p => p
            .Add(n => n.Fixed, false)
            .Add(n => n.ReserveSpace, true));

        Assert.Empty(cut.FindAll(".lumeo-bottom-nav-spacer"));
    }
}
