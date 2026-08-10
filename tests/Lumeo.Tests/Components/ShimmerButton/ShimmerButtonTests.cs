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
    public void Size_Sm_Renders_TextSm_Not_TextXs()
    {
        // Wave-0 fix (C3): ShimmerButton's CssClass is a plain string.Join with no
        // conflict resolution, so BaseClasses' text-sm and SizeClasses' text-xs both
        // ended up in the class attribute — stylesheet source order let .text-xs win,
        // rendering 12px instead of the correct 14px. Deleting the stray text-xs from
        // Sm's SizeClasses removes the conflict entirely.
        var cut = _ctx.Render<Lumeo.ShimmerButton>(p => p
            .Add(b => b.Size, Lumeo.Button.ButtonSize.Sm)
            .AddChildContent("X"));

        var cls = cut.Find("button").GetAttribute("class") ?? "";
        Assert.Contains("text-sm", cls);
        Assert.DoesNotContain("text-xs", cls);
    }

    [Fact]
    public void Size_Xs_Applies_ExtraSmall_Classes()
    {
        // Codex review of PR #386, finding 2: Button.ButtonSize.Xs went public but
        // ShimmerButton's SizeClasses switch (a hand-copied mirror of Button's own,
        // predating Xs) had no Xs arm, so it fell through to the `_ => ""` default —
        // no height, no padding, no text-size override at all. h-6/px-2 match Button's
        // Comfortable-density Xs geometry (Button.razor's SizeClasses); gap-1/text-xs
        // match Button's SizeOverrideClass, folded in directly since ShimmerButton has
        // no separate override slot to merge one in.
        var cut = _ctx.Render<Lumeo.ShimmerButton>(p => p
            .Add(b => b.Size, Lumeo.Button.ButtonSize.Xs)
            .AddChildContent("X"));

        var cls = cut.Find("button").GetAttribute("class") ?? "";
        Assert.Contains("h-6", cls);
        Assert.Contains("px-2", cls);
        Assert.Contains("text-xs", cls);
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
