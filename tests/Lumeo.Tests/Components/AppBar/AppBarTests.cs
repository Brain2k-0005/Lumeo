using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.AppBar;

public class AppBarTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public AppBarTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_As_Header_Element()
    {
        var cut = _ctx.Render<Lumeo.AppBar>();

        Assert.NotNull(cut.Find("header"));
    }

    [Fact]
    public void Custom_Class_Is_Applied()
    {
        var cut = _ctx.Render<Lumeo.AppBar>(p => p
            .Add(a => a.Class, "my-appbar"));

        var cls = cut.Find("header").GetAttribute("class");
        Assert.Contains("my-appbar", cls);
    }

    [Fact]
    public void Sticky_True_Adds_Sticky_Classes()
    {
        var cut = _ctx.Render<Lumeo.AppBar>(p => p
            .Add(a => a.Sticky, true));

        var cls = cut.Find("header").GetAttribute("class");
        Assert.Contains("sticky", cls);
        Assert.Contains("top-0", cls);
    }

    [Fact]
    public void Bordered_True_Adds_Border_Class()
    {
        var cut = _ctx.Render<Lumeo.AppBar>(p => p
            .Add(a => a.Bordered, true));

        var cls = cut.Find("header").GetAttribute("class");
        Assert.Contains("border-b", cls);
    }

    [Fact]
    public void Elevated_True_Adds_Shadow()
    {
        var cut = _ctx.Render<Lumeo.AppBar>(p => p
            .Add(a => a.Elevated, true));

        var cls = cut.Find("header").GetAttribute("class");
        Assert.Contains("shadow-sm", cls);
    }

    [Fact]
    public void Renders_StartContent()
    {
        var cut = _ctx.Render<Lumeo.AppBar>(p => p
            .Add(a => a.StartContent, (RenderFragment)(b => b.AddContent(0, "Logo"))));

        Assert.Contains("Logo", cut.Markup);
    }

    [Fact]
    public void Renders_EndContent()
    {
        var cut = _ctx.Render<Lumeo.AppBar>(p => p
            .Add(a => a.EndContent, (RenderFragment)(b => b.AddContent(0, "Avatar"))));

        Assert.Contains("Avatar", cut.Markup);
    }

    [Fact]
    public void Height_Param_Is_Applied()
    {
        var cut = _ctx.Render<Lumeo.AppBar>(p => p
            .Add(a => a.Height, "h-16"));

        var cls = cut.Find("header").GetAttribute("class");
        Assert.Contains("h-16", cls);
    }
}
