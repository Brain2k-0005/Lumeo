using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Spinner;

public class SpinnerTests : IDisposable
{
    private readonly BunitContext _ctx = new();

    public SpinnerTests()
    {
        _ctx.AddLumeoServices();
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public void Renders_Svg_Element()
    {
        var cut = _ctx.Render<L.Spinner>();

        Assert.NotNull(cut.Find("svg"));
    }

    [Fact]
    public void Has_Animate_Spin_Class()
    {
        var cut = _ctx.Render<L.Spinner>();

        var cls = cut.Find("svg").GetAttribute("class");
        Assert.Contains("animate-spin", cls);
    }

    [Fact]
    public void Default_Size_Has_Correct_Classes()
    {
        var cut = _ctx.Render<L.Spinner>();

        var cls = cut.Find("svg").GetAttribute("class");
        Assert.Contains("h-6", cls);
        Assert.Contains("w-6", cls);
    }

    [Fact]
    public void Small_Size_Has_Correct_Classes()
    {
        var cut = _ctx.Render<L.Spinner>(p => p
            .Add(s => s.Size, L.Spinner.SpinnerSize.Sm));

        var cls = cut.Find("svg").GetAttribute("class");
        Assert.Contains("h-4", cls);
        Assert.Contains("w-4", cls);
    }

    [Fact]
    public void Large_Size_Has_Correct_Classes()
    {
        var cut = _ctx.Render<L.Spinner>(p => p
            .Add(s => s.Size, L.Spinner.SpinnerSize.Lg));

        var cls = cut.Find("svg").GetAttribute("class");
        Assert.Contains("h-8", cls);
        Assert.Contains("w-8", cls);
    }

    [Theory]
    [InlineData(L.Spinner.SpinnerSize.Sm, "h-4", "w-4")]
    [InlineData(L.Spinner.SpinnerSize.Default, "h-6", "w-6")]
    [InlineData(L.Spinner.SpinnerSize.Lg, "h-8", "w-8")]
    public void Size_Variants_Have_Correct_Dimensions(L.Spinner.SpinnerSize size, string expectedH, string expectedW)
    {
        var cut = _ctx.Render<L.Spinner>(p => p
            .Add(s => s.Size, size));

        var cls = cut.Find("svg").GetAttribute("class");
        Assert.Contains(expectedH, cls);
        Assert.Contains(expectedW, cls);
    }

    [Fact]
    public void Svg_Has_Circle_And_Path_Children()
    {
        var cut = _ctx.Render<L.Spinner>();

        Assert.NotNull(cut.Find("circle"));
        Assert.NotNull(cut.Find("path"));
    }

    [Fact]
    public void Svg_Has_Fill_None_Attribute()
    {
        var cut = _ctx.Render<L.Spinner>();

        Assert.Equal("none", cut.Find("svg").GetAttribute("fill"));
    }

    [Fact]
    public void Svg_Has_ViewBox_Attribute()
    {
        var cut = _ctx.Render<L.Spinner>();

        Assert.Equal("0 0 24 24", cut.Find("svg").GetAttribute("viewBox"));
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<L.Spinner>(p => p
            .Add(s => s.Class, "text-primary"));

        var cls = cut.Find("svg").GetAttribute("class");
        Assert.Contains("text-primary", cls);
        Assert.Contains("animate-spin", cls);
    }

    [Fact]
    public void Additional_Attributes_Are_Forwarded()
    {
        var cut = _ctx.Render<L.Spinner>(p => p
            .Add(s => s.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "my-spinner",
                ["aria-label"] = "Loading"
            }));

        var svg = cut.Find("svg");
        Assert.Equal("my-spinner", svg.GetAttribute("data-testid"));
        Assert.Equal("Loading", svg.GetAttribute("aria-label"));
    }
}
