using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Separator;

public class SeparatorTests : IDisposable
{
    private readonly BunitContext _ctx = new();

    public SeparatorTests()
    {
        _ctx.AddLumeoServices();
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public void Renders_Div_Element()
    {
        var cut = _ctx.Render<L.Separator>();

        Assert.NotNull(cut.Find("div"));
    }

    [Fact]
    public void Has_Role_None()
    {
        var cut = _ctx.Render<L.Separator>();

        Assert.Equal("none", cut.Find("div").GetAttribute("role"));
    }

    [Fact]
    public void Horizontal_Is_Default_Orientation()
    {
        var cut = _ctx.Render<L.Separator>();

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("h-px", cls);
        Assert.Contains("w-full", cls);
    }

    [Fact]
    public void Horizontal_Has_Correct_Classes()
    {
        var cut = _ctx.Render<L.Separator>(p => p
            .Add(s => s.Orientation, L.Separator.SeparatorOrientation.Horizontal));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("shrink-0", cls);
        Assert.Contains("bg-border", cls);
        Assert.Contains("h-px", cls);
        Assert.Contains("w-full", cls);
    }

    [Fact]
    public void Vertical_Has_Correct_Classes()
    {
        var cut = _ctx.Render<L.Separator>(p => p
            .Add(s => s.Orientation, L.Separator.SeparatorOrientation.Vertical));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("shrink-0", cls);
        Assert.Contains("bg-border", cls);
        Assert.Contains("h-full", cls);
        Assert.Contains("w-px", cls);
    }

    [Fact]
    public void Vertical_Does_Not_Have_Horizontal_Classes()
    {
        var cut = _ctx.Render<L.Separator>(p => p
            .Add(s => s.Orientation, L.Separator.SeparatorOrientation.Vertical));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.DoesNotContain("h-px", cls);
        Assert.DoesNotContain("w-full", cls);
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<L.Separator>(p => p
            .Add(s => s.Class, "my-separator"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("my-separator", cls);
        Assert.Contains("bg-border", cls);
    }

    [Fact]
    public void Additional_Attributes_Are_Forwarded()
    {
        var cut = _ctx.Render<L.Separator>(p => p
            .Add(s => s.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "my-separator",
                ["aria-orientation"] = "horizontal"
            }));

        var div = cut.Find("div");
        Assert.Equal("my-separator", div.GetAttribute("data-testid"));
        Assert.Equal("horizontal", div.GetAttribute("aria-orientation"));
    }
}
