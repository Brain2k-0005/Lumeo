using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.AspectRatio;

public class AspectRatioTests : IDisposable
{
    private readonly BunitContext _ctx = new();

    public AspectRatioTests()
    {
        _ctx.AddLumeoServices();
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public void Renders_Outer_Div_Element()
    {
        var cut = _ctx.Render<L.AspectRatio>(p => p.AddChildContent("Content"));

        Assert.NotNull(cut.Find("div"));
    }

    [Fact]
    public void Outer_Div_Has_Relative_Class()
    {
        var cut = _ctx.Render<L.AspectRatio>(p => p.AddChildContent("Content"));

        var outerDiv = cut.Find("div");
        var cls = outerDiv.GetAttribute("class");
        Assert.Contains("relative", cls);
        Assert.Contains("w-full", cls);
    }

    [Fact]
    public void Default_Ratio_Is_1_To_1()
    {
        var cut = _ctx.Render<L.AspectRatio>(p => p.AddChildContent("Content"));

        var outerDiv = cut.Find("div");
        var style = outerDiv.GetAttribute("style");
        // 100 / 1 = 100%
        Assert.Contains("padding-bottom: 100%", style);
    }

    [Fact]
    public void Ratio_16_9_Computes_Correct_Padding()
    {
        var cut = _ctx.Render<L.AspectRatio>(p => p
            .Add(a => a.Ratio, 16.0 / 9.0)
            .AddChildContent("Content"));

        var outerDiv = cut.Find("div");
        var style = outerDiv.GetAttribute("style");
        // 100 / (16/9) = 56.25%
        Assert.Contains("56.25%", style);
    }

    [Fact]
    public void Ratio_4_3_Computes_Correct_Padding()
    {
        var cut = _ctx.Render<L.AspectRatio>(p => p
            .Add(a => a.Ratio, 4.0 / 3.0)
            .AddChildContent("Content"));

        var outerDiv = cut.Find("div");
        var style = outerDiv.GetAttribute("style");
        // 100 / (4/3) = 75%
        Assert.Contains("75%", style);
    }

    [Fact]
    public void Inner_Div_Has_Absolute_Inset_Classes()
    {
        var cut = _ctx.Render<L.AspectRatio>(p => p.AddChildContent("Content"));

        var divs = cut.FindAll("div");
        // The inner div is the second one
        Assert.Equal(2, divs.Count);
        var innerDiv = divs[1];
        var cls = innerDiv.GetAttribute("class");
        Assert.Contains("absolute", cls);
        Assert.Contains("inset-0", cls);
    }

    [Fact]
    public void Renders_Child_Content_In_Inner_Div()
    {
        var cut = _ctx.Render<L.AspectRatio>(p => p.AddChildContent("My Content"));

        Assert.Contains("My Content", cut.Markup);
    }

    [Fact]
    public void Custom_Class_Applied_To_Inner_Div()
    {
        var cut = _ctx.Render<L.AspectRatio>(p => p
            .Add(a => a.Class, "overflow-hidden")
            .AddChildContent("Content"));

        var divs = cut.FindAll("div");
        var innerDiv = divs[1];
        var cls = innerDiv.GetAttribute("class");
        Assert.Contains("overflow-hidden", cls);
        Assert.Contains("absolute", cls);
    }

    [Fact]
    public void Additional_Attributes_Are_Forwarded_To_Outer_Div()
    {
        var cut = _ctx.Render<L.AspectRatio>(p => p
            .Add(a => a.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "my-aspect-ratio"
            })
            .AddChildContent("Content"));

        var outerDiv = cut.Find("div");
        Assert.Equal("my-aspect-ratio", outerDiv.GetAttribute("data-testid"));
    }
}
