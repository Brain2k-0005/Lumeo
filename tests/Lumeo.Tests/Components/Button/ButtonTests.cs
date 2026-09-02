using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Button;

public class ButtonTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ButtonTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_With_Default_Variant_And_Size()
    {
        var cut = _ctx.Render<Lumeo.Button>(p => p
            .AddChildContent("Click me"));

        var button = cut.Find("button");
        Assert.Contains("bg-primary", button.GetAttribute("class"));
        Assert.Contains("h-8 px-2.5", button.GetAttribute("class"));
        Assert.Equal("Click me", button.TextContent.Trim());
    }

    [Fact]
    public void Button_Without_Ambient_TriggerSlot_Renders_No_Id()
    {
        // Byte-identity contract for the asChild (G34) slot plumbing: a normal Button
        // — no AsChild trigger cascading a TriggerSlot above it — must not pick up an
        // id="" from id="@Slot?.Id". Guards the null-Slot fast path against regressions.
        var cut = _ctx.Render<Lumeo.Button>(p => p.AddChildContent("Go"));

        Assert.False(cut.Find("button").HasAttribute("id"));
    }

    [Fact]
    public void Renders_Destructive_Variant()
    {
        var cut = _ctx.Render<Lumeo.Button>(p => p
            .Add(b => b.Variant, Lumeo.Button.ButtonVariant.Destructive)
            .AddChildContent("Delete"));

        var button = cut.Find("button");
        Assert.Contains("bg-destructive", button.GetAttribute("class"));
    }

    [Fact]
    public void Renders_Outline_Variant()
    {
        var cut = _ctx.Render<Lumeo.Button>(p => p
            .Add(b => b.Variant, Lumeo.Button.ButtonVariant.Outline)
            .AddChildContent("Outline"));

        var button = cut.Find("button");
        Assert.Contains("border-input", button.GetAttribute("class"));
    }

    [Fact]
    public void Renders_Secondary_Variant()
    {
        var cut = _ctx.Render<Lumeo.Button>(p => p
            .Add(b => b.Variant, Lumeo.Button.ButtonVariant.Secondary)
            .AddChildContent("Secondary"));

        var button = cut.Find("button");
        Assert.Contains("bg-secondary", button.GetAttribute("class"));
    }

    [Fact]
    public void Renders_Ghost_Variant()
    {
        var cut = _ctx.Render<Lumeo.Button>(p => p
            .Add(b => b.Variant, Lumeo.Button.ButtonVariant.Ghost)
            .AddChildContent("Ghost"));

        var button = cut.Find("button");
        Assert.Contains("hover:bg-accent", button.GetAttribute("class"));
    }

    [Fact]
    public void Renders_Link_Variant()
    {
        var cut = _ctx.Render<Lumeo.Button>(p => p
            .Add(b => b.Variant, Lumeo.Button.ButtonVariant.Link)
            .AddChildContent("Link"));

        var button = cut.Find("button");
        Assert.Contains("underline-offset-4", button.GetAttribute("class"));
    }

    [Theory]
    [InlineData(Lumeo.Button.ButtonSize.Sm, "h-7")]
    [InlineData(Lumeo.Button.ButtonSize.Lg, "h-9")]
    [InlineData(Lumeo.Button.ButtonSize.Icon, "w-8")]
    public void Renders_Correct_Size_Classes(Lumeo.Button.ButtonSize size, string expectedClass)
    {
        var cut = _ctx.Render<Lumeo.Button>(p => p
            .Add(b => b.Size, size)
            .AddChildContent("Btn"));

        var button = cut.Find("button");
        Assert.Contains(expectedClass, button.GetAttribute("class"));
    }

    [Fact]
    public void Sm_Size_Renders_Shadcns_Text0Point8Rem()
    {
        // Wave-0 no-op proof (C3): SizeClasses used to emit a dead " text-xs" for
        // Sm that Cx.Merge always discarded (Size runs before Base in CssClass's
        // merge order, and Base's text-sm — the LAST font-size utility in source
        // order — always wins the conflict group). Deleting the dead token must
        // render byte-identical: text-sm present, text-xs absent, both before and
        // after the cleanup.
        var cut = _ctx.Render<Lumeo.Button>(p => p
            .Add(b => b.Size, Lumeo.Button.ButtonSize.Sm)
            .AddChildContent("Btn"));

        var cls = cut.Find("button").GetAttribute("class") ?? "";
        Assert.Contains("text-[0.8rem]", cls);
        Assert.DoesNotContain("text-xs", cls);
    }

    [Fact]
    public void Xs_Size_Renders_Shadcn_Parity_Classes()
    {
        // shadcn `xs`: "h-6 gap-1 rounded-md px-2 text-xs ...". Comfortable/default
        // density is the row that must match shadcn exactly (shadcn has no density
        // concept). Predicted-vs-actual (disable check, see
        // Xs_Size_Gap_And_Text_Survive_BaseClasses_Merge_Order below): with the
        // SizeOverrideClass merge removed, gap-1/text-xs are silently discarded by
        // Cx.Merge's last-wins resolution and BaseClasses' gap-2/text-sm win instead.
        var cut = _ctx.Render<Lumeo.Button>(p => p
            .Add(b => b.Size, Lumeo.Button.ButtonSize.Xs)
            .AddChildContent("Btn"));

        var cls = cut.Find("button").GetAttribute("class") ?? "";
        Assert.Contains("h-6", cls);
        Assert.Contains("px-2", cls);
        Assert.Contains("gap-1", cls);
        Assert.Contains("text-xs", cls);
        Assert.DoesNotContain("gap-2", cls);
        Assert.DoesNotContain("text-sm", cls);
    }

    [Theory]
    [InlineData(Lumeo.Density.Compact, "h-5", "px-1.5")]
    [InlineData(Lumeo.Density.Spacious, "h-7", "px-2.5")]
    public void Xs_Size_Density_Rows_Are_Proportionate(Lumeo.Density density, string expectedHeight, string expectedPad)
    {
        var cut = _ctx.Render<Lumeo.Button>(p => p
            .Add(b => b.Size, Lumeo.Button.ButtonSize.Xs)
            .Add(b => b.Density, density)
            .AddChildContent("Btn"));

        var cls = cut.Find("button").GetAttribute("class") ?? "";
        Assert.Contains(expectedHeight, cls);
        Assert.Contains(expectedPad, cls);
    }

    [Fact]
    public void Sm_Size_Renders_Gap1_Not_Base_Gap2()
    {
        // shadcn `sm` carries gap-1; Lumeo's BaseClasses fixes gap-2 for every size.
        // Predicted-vs-actual disable check: reverting SizeOverrideClass to null for Sm
        // (or moving it before BaseClasses in the CssClass merge) makes this render
        // "gap-2" instead — confirmed manually before writing this assertion.
        var cut = _ctx.Render<Lumeo.Button>(p => p
            .Add(b => b.Size, Lumeo.Button.ButtonSize.Sm)
            .AddChildContent("Btn"));

        var cls = cut.Find("button").GetAttribute("class") ?? "";
        Assert.Contains("gap-1", cls);
        Assert.DoesNotContain("gap-2", cls);
    }

    [Fact]
    public void Lg_Comfortable_Padding_Matches_Shadcns_Uniform_Px2Point5()
    {
        // shadcn's `lg` is "h-9 px-2.5" today: they collapsed the per-size padding
        // ladder (px-3 / px-4 / px-6) onto a single value for sm, default and lg
        // alike. Measured in a project built with their CLI, since an older registry
        // file in their repository still carries the previous px-6.
        var cut = _ctx.Render<Lumeo.Button>(p => p
            .Add(b => b.Size, Lumeo.Button.ButtonSize.Lg)
            .AddChildContent("Btn"));

        var cls = cut.Find("button").GetAttribute("class") ?? "";
        Assert.Contains("px-2.5", cls);
        Assert.DoesNotContain("px-6", cls);
    }

    [Theory]
    [InlineData(Lumeo.Density.Compact, "px-2")]
    [InlineData(Lumeo.Density.Spacious, "px-3")]
    public void Lg_Density_Rows_Shift_Proportionately_With_Comfortable(Lumeo.Density density, string expectedPad)
    {
        // Compact/Spacious are Lumeo-only (shadcn has no density concept). They are kept
        // proportionate by shifting the whole family the same -2 units Comfortable moved
        // (px-8->px-6): Compact px-6->px-4, Spacious px-10->px-8 — preserving the
        // original +/-2 spread around Comfortable.
        var cut = _ctx.Render<Lumeo.Button>(p => p
            .Add(b => b.Size, Lumeo.Button.ButtonSize.Lg)
            .Add(b => b.Density, density)
            .AddChildContent("Btn"));

        var cls = cut.Find("button").GetAttribute("class") ?? "";
        Assert.Contains(expectedPad, cls);
    }

    [Fact]
    public void Outline_Variant_Has_No_Shadow()
    {
        // shadcn (measured live 2026-09-02): no resting shadow on any button variant.
        var cut = _ctx.Render<Lumeo.Button>(p => p
            .Add(b => b.Variant, Lumeo.Button.ButtonVariant.Outline)
            .AddChildContent("Outline"));

        Assert.DoesNotContain("shadow", cut.Find("button").GetAttribute("class"));
    }

    [Fact]
    public void Click_Event_Fires()
    {
        var clicked = false;
        var cut = _ctx.Render<Lumeo.Button>(p => p
            .Add(b => b.OnClick, _ => clicked = true)
            .AddChildContent("Click"));

        cut.Find("button").Click();
        Assert.True(clicked);
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.Button>(p => p
            .Add(b => b.Class, "my-custom-class")
            .AddChildContent("Styled"));

        var button = cut.Find("button");
        Assert.Contains("my-custom-class", button.GetAttribute("class"));
        Assert.Contains("inline-flex", button.GetAttribute("class"));
    }

    [Fact]
    public void Additional_Attributes_Are_Forwarded()
    {
        var cut = _ctx.Render<Lumeo.Button>(p => p
            .Add(b => b.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "my-button",
                ["aria-label"] = "Close dialog"
            })
            .AddChildContent("X"));

        var button = cut.Find("button");
        Assert.Equal("my-button", button.GetAttribute("data-testid"));
        Assert.Equal("Close dialog", button.GetAttribute("aria-label"));
    }

    // --- FullWidth ---

    [Fact]
    public void FullWidth_Adds_W_Full_Class()
    {
        var cut = _ctx.Render<Lumeo.Button>(p => p
            .Add(b => b.FullWidth, true)
            .AddChildContent("Full"));

        var button = cut.Find("button");
        Assert.Contains("w-full", button.GetAttribute("class"));
    }

    [Fact]
    public void Default_Does_Not_Have_W_Full_Class()
    {
        var cut = _ctx.Render<Lumeo.Button>(p => p
            .AddChildContent("Normal"));

        var button = cut.Find("button");
        Assert.DoesNotContain("w-full", button.GetAttribute("class"));
    }

    // --- LeftIcon / RightIcon ---

    [Fact]
    public void LeftIcon_Renders_Before_Content()
    {
        var cut = _ctx.Render<Lumeo.Button>(p => p
            .Add(b => b.LeftIcon, (Microsoft.AspNetCore.Components.RenderFragment)(builder =>
            {
                builder.OpenElement(0, "span");
                builder.AddAttribute(1, "class", "left-icon-marker");
                builder.AddContent(2, "L");
                builder.CloseElement();
            }))
            .AddChildContent("Text"));

        var html = cut.Find("button").InnerHtml;
        var leftIconPos = html.IndexOf("left-icon-marker");
        var textPos = html.IndexOf("Text");
        Assert.True(leftIconPos < textPos, "LeftIcon should render before content");
    }

    [Fact]
    public void RightIcon_Renders_After_Content()
    {
        var cut = _ctx.Render<Lumeo.Button>(p => p
            .Add(b => b.RightIcon, (Microsoft.AspNetCore.Components.RenderFragment)(builder =>
            {
                builder.OpenElement(0, "span");
                builder.AddAttribute(1, "class", "right-icon-marker");
                builder.AddContent(2, "R");
                builder.CloseElement();
            }))
            .AddChildContent("Text"));

        var html = cut.Find("button").InnerHtml;
        var textPos = html.IndexOf("Text");
        var rightIconPos = html.IndexOf("right-icon-marker");
        Assert.True(rightIconPos > textPos, "RightIcon should render after content");
    }
}
