using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Kbd;

public class KbdTests : IDisposable
{
    private readonly BunitContext _ctx = new();

    public KbdTests()
    {
        _ctx.AddLumeoServices();
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public void Renders_Kbd_Element()
    {
        var cut = _ctx.Render<L.Kbd>(p => p.AddChildContent("Ctrl"));

        Assert.NotNull(cut.Find("kbd"));
    }

    [Fact]
    public void Renders_Child_Content()
    {
        var cut = _ctx.Render<L.Kbd>(p => p.AddChildContent("Ctrl"));

        Assert.Equal("Ctrl", cut.Find("kbd").TextContent.Trim());
    }

    [Fact]
    public void Has_Default_Base_Classes()
    {
        var cut = _ctx.Render<L.Kbd>(p => p.AddChildContent("K"));

        var cls = cut.Find("kbd").GetAttribute("class");
        Assert.Contains("pointer-events-none", cls);
        Assert.Contains("inline-flex", cls);
        Assert.Contains("select-none", cls);
        Assert.Contains("items-center", cls);
        Assert.Contains("rounded", cls);
        Assert.Contains("border", cls);
        Assert.Contains("bg-muted", cls);
        Assert.Contains("font-mono", cls);
        Assert.Contains("font-medium", cls);
        Assert.Contains("text-muted-foreground", cls);
    }

    [Fact]
    public void Default_Size_Has_Correct_Classes()
    {
        var cut = _ctx.Render<L.Kbd>(p => p.AddChildContent("Enter"));

        var cls = cut.Find("kbd").GetAttribute("class");
        Assert.Contains("h-5", cls);
        Assert.Contains("px-1.5", cls);
    }

    [Fact]
    public void Small_Size_Has_Correct_Classes()
    {
        var cut = _ctx.Render<L.Kbd>(p => p
            .Add(k => k.Size, L.Kbd.KbdSize.Sm)
            .AddChildContent("K"));

        var cls = cut.Find("kbd").GetAttribute("class");
        Assert.Contains("h-5", cls);
        Assert.Contains("px-1", cls);
    }

    [Fact]
    public void Large_Size_Has_Correct_Classes()
    {
        var cut = _ctx.Render<L.Kbd>(p => p
            .Add(k => k.Size, L.Kbd.KbdSize.Lg)
            .AddChildContent("Esc"));

        var cls = cut.Find("kbd").GetAttribute("class");
        Assert.Contains("h-6", cls);
        Assert.Contains("px-2", cls);
        Assert.Contains("text-xs", cls);
    }

    [Theory]
    [InlineData(L.Kbd.KbdSize.Sm, "h-5", "px-1")]
    [InlineData(L.Kbd.KbdSize.Default, "h-5", "px-1.5")]
    [InlineData(L.Kbd.KbdSize.Lg, "h-6", "px-2")]
    public void Size_Variants_Have_Correct_Classes(L.Kbd.KbdSize size, string expectedH, string expectedPx)
    {
        var cut = _ctx.Render<L.Kbd>(p => p
            .Add(k => k.Size, size)
            .AddChildContent("K"));

        var cls = cut.Find("kbd").GetAttribute("class");
        Assert.Contains(expectedH, cls);
        Assert.Contains(expectedPx, cls);
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<L.Kbd>(p => p
            .Add(k => k.Class, "my-kbd-class")
            .AddChildContent("Tab"));

        var cls = cut.Find("kbd").GetAttribute("class");
        Assert.Contains("my-kbd-class", cls);
        Assert.Contains("font-mono", cls);
    }

    [Fact]
    public void Additional_Attributes_Are_Forwarded()
    {
        var cut = _ctx.Render<L.Kbd>(p => p
            .Add(k => k.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "my-kbd",
                ["aria-label"] = "Control key"
            })
            .AddChildContent("Ctrl"));

        var kbd = cut.Find("kbd");
        Assert.Equal("my-kbd", kbd.GetAttribute("data-testid"));
        Assert.Equal("Control key", kbd.GetAttribute("aria-label"));
    }
}
