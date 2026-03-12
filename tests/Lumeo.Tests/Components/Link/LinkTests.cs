using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Link;

public class LinkTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public LinkTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Anchor_Element()
    {
        var cut = _ctx.Render<Lumeo.Link>(p => p
            .Add(l => l.Href, "/home")
            .AddChildContent("Home"));

        var a = cut.Find("a");
        Assert.NotNull(a);
        Assert.Equal("Home", a.TextContent);
    }

    [Fact]
    public void Href_Is_Set_On_Anchor()
    {
        var cut = _ctx.Render<Lumeo.Link>(p => p
            .Add(l => l.Href, "/about")
            .AddChildContent("About"));

        Assert.Equal("/about", cut.Find("a").GetAttribute("href"));
    }

    [Fact]
    public void Default_Variant_Has_Primary_Text_Class()
    {
        var cut = _ctx.Render<Lumeo.Link>(p => p
            .AddChildContent("Link"));

        var cls = cut.Find("a").GetAttribute("class");
        Assert.Contains("text-primary", cls);
    }

    [Fact]
    public void External_True_Adds_Target_Blank()
    {
        var cut = _ctx.Render<Lumeo.Link>(p => p
            .Add(l => l.External, true)
            .Add(l => l.Href, "https://example.com")
            .AddChildContent("External"));

        var a = cut.Find("a");
        Assert.Equal("_blank", a.GetAttribute("target"));
        Assert.Equal("noopener noreferrer", a.GetAttribute("rel"));
    }

    [Fact]
    public void Muted_Variant_Has_Muted_Foreground_Class()
    {
        var cut = _ctx.Render<Lumeo.Link>(p => p
            .Add(l => l.Variant, "muted")
            .AddChildContent("Muted link"));

        var cls = cut.Find("a").GetAttribute("class");
        Assert.Contains("text-muted-foreground", cls);
    }
}
