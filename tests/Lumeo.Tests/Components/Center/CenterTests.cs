using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Center;

public class CenterTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public CenterTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Default_Center()
    {
        var cut = _ctx.Render<Lumeo.Center>(p => p
            .AddChildContent("Content"));

        var div = cut.Find("div");
        Assert.Equal("Content", div.TextContent.Trim());
    }

    [Fact]
    public void Default_Has_Flex_And_Center_Classes()
    {
        var cut = _ctx.Render<Lumeo.Center>(p => p
            .AddChildContent("Content"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("flex", cls);
        Assert.Contains("items-center", cls);
        Assert.Contains("justify-center", cls);
    }

    [Fact]
    public void Inline_True_Uses_InlineFlex()
    {
        var cut = _ctx.Render<Lumeo.Center>(p => p
            .Add(c => c.Inline, true)
            .AddChildContent("Content"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("inline-flex", cls);
    }

    [Fact]
    public void Height_Prop_Sets_Style_Attribute()
    {
        var cut = _ctx.Render<Lumeo.Center>(p => p
            .Add(c => c.Height, "200px")
            .AddChildContent("Content"));

        var style = cut.Find("div").GetAttribute("style");
        Assert.Contains("200px", style);
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.Center>(p => p
            .Add(c => c.Class, "my-custom")
            .AddChildContent("Content"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("my-custom", cls);
        Assert.Contains("flex", cls);
    }
}
