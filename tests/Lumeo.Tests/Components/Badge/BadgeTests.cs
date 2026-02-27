using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Badge;

public class BadgeTests : IDisposable
{
    private readonly BunitContext _ctx = new();

    public BadgeTests()
    {
        _ctx.AddLumeoServices();
    }

    public void Dispose() => _ctx.Dispose();

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
        Assert.Contains("rounded-md", cls);
        Assert.Contains("border", cls);
        Assert.Contains("text-xs", cls);
        Assert.Contains("font-semibold", cls);
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
}
