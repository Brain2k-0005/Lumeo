using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Badge;

public class BadgeTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public BadgeTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Default_Badge()
    {
        var cut = _ctx.Render<Lumeo.Badge>(p => p
            .AddChildContent("New"));

        var div = cut.Find("div");
        Assert.Equal("New", div.TextContent.Trim());
    }

    [Fact]
    public void Renders_Child_Content()
    {
        var cut = _ctx.Render<Lumeo.Badge>(p => p
            .AddChildContent("Hello World"));

        Assert.Equal("Hello World", cut.Find("div").TextContent.Trim());
    }

    [Fact]
    public void Renders_As_Div_Element()
    {
        var cut = _ctx.Render<Lumeo.Badge>(p => p
            .AddChildContent("Badge"));

        Assert.NotNull(cut.Find("div"));
    }

    [Fact]
    public void Default_Variant_Has_Primary_Classes()
    {
        var cut = _ctx.Render<Lumeo.Badge>(p => p
            .AddChildContent("Default"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("bg-primary", cls);
        Assert.Contains("text-primary-foreground", cls);
    }

    [Fact]
    public void Secondary_Variant_Has_Secondary_Classes()
    {
        var cut = _ctx.Render<Lumeo.Badge>(p => p
            .Add(b => b.Variant, Lumeo.Badge.BadgeVariant.Secondary)
            .AddChildContent("Secondary"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("bg-secondary", cls);
        Assert.Contains("text-secondary-foreground", cls);
    }

    [Fact]
    public void Destructive_Variant_Has_Destructive_Classes()
    {
        var cut = _ctx.Render<Lumeo.Badge>(p => p
            .Add(b => b.Variant, Lumeo.Badge.BadgeVariant.Destructive)
            .AddChildContent("Error"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("bg-destructive", cls);
        Assert.Contains("text-destructive-foreground", cls);
    }

    [Fact]
    public void Outline_Variant_Has_Foreground_Text()
    {
        var cut = _ctx.Render<Lumeo.Badge>(p => p
            .Add(b => b.Variant, Lumeo.Badge.BadgeVariant.Outline)
            .AddChildContent("Outline"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("text-foreground", cls);
    }

    [Fact]
    public void Success_Variant_Has_Success_Classes()
    {
        var cut = _ctx.Render<Lumeo.Badge>(p => p
            .Add(b => b.Variant, Lumeo.Badge.BadgeVariant.Success)
            .AddChildContent("OK"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("bg-success-light", cls);
        Assert.Contains("text-success-text", cls);
    }

    [Fact]
    public void Warning_Variant_Has_Warning_Classes()
    {
        var cut = _ctx.Render<Lumeo.Badge>(p => p
            .Add(b => b.Variant, Lumeo.Badge.BadgeVariant.Warning)
            .AddChildContent("Warning"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("bg-warning-light", cls);
        Assert.Contains("text-warning-text", cls);
    }

    [Fact]
    public void All_Badges_Have_Base_Classes()
    {
        var cut = _ctx.Render<Lumeo.Badge>(p => p
            .AddChildContent("Base"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("inline-flex", cls);
        Assert.Contains("items-center", cls);
        Assert.Contains("rounded-sm", cls);
        Assert.Contains("border", cls);
        Assert.Contains("text-xs", cls);
        Assert.Contains("font-medium", cls);
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.Badge>(p => p
            .Add(b => b.Class, "my-custom-class")
            .AddChildContent("Styled"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("my-custom-class", cls);
        Assert.Contains("inline-flex", cls);
    }

    [Fact]
    public void Additional_Attributes_Are_Forwarded()
    {
        var cut = _ctx.Render<Lumeo.Badge>(p => p
            .Add(b => b.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "my-badge",
                ["aria-label"] = "Status badge"
            })
            .AddChildContent("Badge"));

        var div = cut.Find("div");
        Assert.Equal("my-badge", div.GetAttribute("data-testid"));
        Assert.Equal("Status badge", div.GetAttribute("aria-label"));
    }

    // --- Pulse + IsDot ---

    [Fact]
    public void Pulse_IsDot_Renders_Animate_Ping_Element()
    {
        var cut = _ctx.Render<Lumeo.Badge>(p => p
            .Add(b => b.IsDot, true)
            .Add(b => b.Pulse, true));

        var spans = cut.FindAll("span");
        var hasPing = spans.Any(s =>
            (s.GetAttribute("class") ?? "").Contains("animate-ping"));
        Assert.True(hasPing, "Pulse dot badge should render an animate-ping element");
    }

    [Fact]
    public void IsDot_Without_Pulse_Does_Not_Render_Animate_Ping()
    {
        var cut = _ctx.Render<Lumeo.Badge>(p => p
            .Add(b => b.IsDot, true)
            .Add(b => b.Pulse, false));

        // IsDot without Pulse renders a simple div, no animate-ping
        Assert.Empty(cut.FindAll("span"));
    }

    // --- Icon ---

    [Fact]
    public void Icon_Renders_Custom_Icon_Content()
    {
        var cut = _ctx.Render<Lumeo.Badge>(p => p
            .Add(b => b.IconContent, (Microsoft.AspNetCore.Components.RenderFragment)(builder =>
            {
                builder.AddContent(0, "ICON");
            }))
            .AddChildContent("Badge Text"));

        Assert.Contains("ICON", cut.Markup);
        Assert.Contains("Badge Text", cut.Markup);
    }

    // --- Size (migrated from Badge.BadgeSize to Lumeo.Size) ---
    // Exact space-delimited token comparisons (never Assert.Contains — Tailwind's
    // scale is built from prefixes of itself, e.g. px-2 matches px-2.5) pinning the
    // pre-migration Sm/Md/Lg render byte-for-byte, at the default Variant/Density.

    private static string[] Tokens(AngleSharp.Dom.IElement el) =>
        (el.GetAttribute("class") ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);

    // The three lists below were frozen during the Size-scale migration to prove that migration
    // changed nothing. They moved once since, deliberately: w-fit / shrink-0 / justify-center /
    // whitespace-nowrap were added because inline-flex alone still stretches inside a flex column
    // or a stretched grid cell, so a badge in a table column rendered as wide as the column
    // (measured: 300px against 77.6px for the same content) and wrapped to two lines when squeezed.
    // They moved a second time in the 5.0 alignment onto reui's ladder (px-1.25 py-0.5 h-5 at
    // the default rung), and a third time in 5.7.0: the default rung is shadcn's badge again
    // (px-2 py-0.5 text-xs on a 16px line, 22px tall) because reui's leading-none clipped
    // descenders inside a truncating span (field report 2.2), and Lg moved up one step to stay
    // above it. What the tests still guard is that the rungs stay distinct and that nothing
    // silently drifts between releases.
    // A caller can still override the width: w-* is a merge conflict group, so Class="w-full" wins.
    [Fact]
    public void Size_Sm_Renders_Byte_Identical_To_Pre_Migration_BadgeSize_Sm()
    {
        var cut = _ctx.Render<Lumeo.Badge>(p => p
            .Add(b => b.Size, Lumeo.Size.Sm)
            .AddChildContent("X"));

        Assert.Equal(
            new[]
            {
                "relative", "inline-flex", "w-fit", "shrink-0", "items-center", "justify-center",
                "gap-1", "whitespace-nowrap", "border", "font-medium", "leading-none",
                "transition-colors",
                "focus:outline-none", "[&>svg]:size-3", "[&>svg]:pointer-events-none", "rounded-sm", "text-[10px]", "min-h-4.5", "min-w-4.5", "px-1", "py-0.25",
                "border-transparent", "bg-primary", "text-primary-foreground"
            },
            Tokens(cut.Find("div")));
    }

    [Fact]
    public void Size_Md_Renders_Byte_Identical_To_Pre_Migration_BadgeSize_Md_Default()
    {
        var cut = _ctx.Render<Lumeo.Badge>(p => p.AddChildContent("X"));

        Assert.Equal(
            new[]
            {
                "relative", "inline-flex", "w-fit", "shrink-0", "items-center", "justify-center",
                "gap-1", "whitespace-nowrap", "border", "font-medium",
                "transition-colors",
                "focus:outline-none", "[&>svg]:size-3", "[&>svg]:pointer-events-none", "rounded-sm", "text-xs", "leading-4", "min-h-5.5", "min-w-5.5", "px-2", "py-0.5",
                "border-transparent", "bg-primary", "text-primary-foreground"
            },
            Tokens(cut.Find("div")));
    }

    [Fact]
    public void Size_Lg_Renders_Byte_Identical_To_Pre_Migration_BadgeSize_Lg()
    {
        var cut = _ctx.Render<Lumeo.Badge>(p => p
            .Add(b => b.Size, Lumeo.Size.Lg)
            .AddChildContent("X"));

        Assert.Equal(
            new[]
            {
                "relative", "inline-flex", "w-fit", "shrink-0", "items-center", "justify-center",
                "gap-1", "whitespace-nowrap", "border", "font-medium", "leading-none",
                "transition-colors",
                "focus:outline-none", "[&>svg]:size-3", "[&>svg]:pointer-events-none", "rounded-sm", "text-sm", "min-h-6", "min-w-6", "px-2", "py-0.5",
                "border-transparent", "bg-primary", "text-primary-foreground"
            },
            Tokens(cut.Find("div")));
    }

    [Theory]
    [InlineData(Lumeo.Size.Xxs, "text-[8px]")]
    [InlineData(Lumeo.Size.Xs, "text-[9px]")]
    [InlineData(Lumeo.Size.Xl, "text-base")]
    [InlineData(Lumeo.Size.Xxl, "text-lg")]
    public void New_Rungs_Render_Their_Documented_Text_Size(Lumeo.Size size, string expectedTextClass)
    {
        var cut = _ctx.Render<Lumeo.Badge>(p => p.Add(b => b.Size, size).AddChildContent("X"));
        Assert.Contains(expectedTextClass, Tokens(cut.Find("div")));
    }

    [Fact]
    public void A_badge_stays_at_its_content_width_and_on_one_line()
    {
        // inline-flex alone still stretches inside a flex column or a stretched grid cell, so a
        // badge dropped into a table column came out as wide as the column - measured at 300px
        // against 77.6px for the same content. shadcn carries w-fit/shrink-0/whitespace-nowrap for
        // exactly that, and a badge squeezed in a narrow row used to wrap and change the row height.
        var cut = _ctx.Render<Lumeo.Badge>(p => p.AddChildContent("Gesammelt"));

        var cls = cut.Find("div").GetAttribute("class") ?? string.Empty;
        Assert.Contains("w-fit", cls);
        Assert.Contains("shrink-0", cls);
        Assert.Contains("whitespace-nowrap", cls);
    }

    [Fact]
    public void A_caller_can_still_stretch_a_badge_that_should_fill_its_container()
    {
        // w-fit is a default, not a decree: w-* is a merge conflict group and Class is merged last,
        // so anyone who wants the old full-width behaviour back asks for it and gets it.
        var cut = _ctx.Render<Lumeo.Badge>(p => p
            .Add(b => b.Class, "w-full")
            .AddChildContent("Gesammelt"));

        var cls = cut.Find("div").GetAttribute("class") ?? string.Empty;
        Assert.Contains("w-full", cls);
        Assert.DoesNotContain("w-fit", cls);
    }
}
