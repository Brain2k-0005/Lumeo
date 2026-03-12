using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Heading;

public class HeadingTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public HeadingTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Default_Level2_Renders_H2()
    {
        var cut = _ctx.Render<Lumeo.Heading>(p => p
            .AddChildContent("My Heading"));

        Assert.NotNull(cut.Find("h2"));
        Assert.Equal("My Heading", cut.Find("h2").TextContent);
    }

    [Fact]
    public void Level1_Renders_H1()
    {
        var cut = _ctx.Render<Lumeo.Heading>(p => p
            .Add(h => h.Level, 1)
            .AddChildContent("H1 Heading"));

        Assert.NotNull(cut.Find("h1"));
    }

    [Fact]
    public void Level3_Renders_H3()
    {
        var cut = _ctx.Render<Lumeo.Heading>(p => p
            .Add(h => h.Level, 3)
            .AddChildContent("H3 Heading"));

        Assert.NotNull(cut.Find("h3"));
    }

    [Fact]
    public void Default_Includes_Text_Foreground_Class()
    {
        var cut = _ctx.Render<Lumeo.Heading>(p => p
            .AddChildContent("Heading"));

        var cls = cut.Find("h2").GetAttribute("class");
        Assert.Contains("text-foreground", cls);
    }

    [Fact]
    public void Level1_Defaults_To_Bold_And_Tight_Tracking()
    {
        var cut = _ctx.Render<Lumeo.Heading>(p => p
            .Add(h => h.Level, 1)
            .AddChildContent("H1"));

        var cls = cut.Find("h1").GetAttribute("class");
        Assert.Contains("font-bold", cls);
        Assert.Contains("tracking-tight", cls);
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.Heading>(p => p
            .Add(h => h.Class, "my-heading")
            .AddChildContent("Heading"));

        var cls = cut.Find("h2").GetAttribute("class");
        Assert.Contains("my-heading", cls);
    }
}
