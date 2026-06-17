using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.ScrollArea;

public class ScrollAreaTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ScrollAreaTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Div_Container()
    {
        var cut = _ctx.Render<L.ScrollArea>(p => p
            .AddChildContent("content"));

        Assert.NotNull(cut.Find("div"));
    }

    [Fact]
    public void Has_Cross_Browser_Scrollbar_Class()
    {
        // #256 — webkit-only inline classes were replaced by the cross-browser
        // lumeo-scrollarea rule (Firefox scrollbar-* + WebKit pseudo-elements).
        var cut = _ctx.Render<L.ScrollArea>(p => p
            .AddChildContent("content"));

        var div = cut.Find("div");
        Assert.Contains("lumeo-scrollarea", div.GetAttribute("class"));
    }

    [Fact]
    public void Default_Type_Is_Auto()
    {
        var cut = _ctx.Render<L.ScrollArea>(p => p
            .AddChildContent("content"));

        Assert.Equal("auto", cut.Find("div").GetAttribute("data-scroll-type"));
    }

    [Fact]
    public void Has_Relative_Class()
    {
        var cut = _ctx.Render<L.ScrollArea>(p => p
            .AddChildContent("content"));

        var div = cut.Find("div");
        Assert.Contains("relative", div.GetAttribute("class"));
    }

    [Fact]
    public void Renders_ChildContent()
    {
        var cut = _ctx.Render<L.ScrollArea>(p => p
            .AddChildContent("<p>Hello scrollable</p>"));

        Assert.NotNull(cut.Find("p"));
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<L.ScrollArea>(p => p
            .Add(b => b.Class, "h-64")
            .AddChildContent("content"));

        var div = cut.Find("div");
        Assert.Contains("h-64", div.GetAttribute("class"));
        Assert.Contains("lumeo-scrollarea", div.GetAttribute("class"));
    }

    [Fact]
    public void Additional_Attributes_Are_Forwarded()
    {
        var cut = _ctx.Render<L.ScrollArea>(p => p
            .Add(b => b.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "scroll-area",
                ["aria-label"] = "Scrollable region"
            })
            .AddChildContent("content"));

        var div = cut.Find("div");
        Assert.Equal("scroll-area", div.GetAttribute("data-testid"));
        Assert.Equal("Scrollable region", div.GetAttribute("aria-label"));
    }

    [Fact]
    public void Null_Class_Does_Not_Add_Extra_Space()
    {
        var cut = _ctx.Render<L.ScrollArea>(p => p
            .AddChildContent("content"));

        var div = cut.Find("div");
        var cls = div.GetAttribute("class");
        Assert.DoesNotContain("  ", cls);
        Assert.False(cls!.EndsWith(" "));
    }

    [Theory]
    [InlineData(L.ScrollArea.ScrollAreaType.Auto, "auto")]
    [InlineData(L.ScrollArea.ScrollAreaType.Always, "always")]
    [InlineData(L.ScrollArea.ScrollAreaType.Scroll, "scroll")]
    [InlineData(L.ScrollArea.ScrollAreaType.Hover, "hover")]
    public void Type_Sets_Data_Scroll_Type(L.ScrollArea.ScrollAreaType type, string expected)
    {
        var cut = _ctx.Render<L.ScrollArea>(p => p
            .Add(b => b.Type, type)
            .AddChildContent("content"));

        Assert.Equal(expected, cut.Find("div").GetAttribute("data-scroll-type"));
    }

    [Fact]
    public void FocusRingGutter_Adds_Inline_Gutter()
    {
        var cut = _ctx.Render<L.ScrollArea>(p => p
            .Add(b => b.FocusRingGutter, true)
            .AddChildContent("content"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("-mx-1", cls);
        Assert.Contains("px-1", cls);
    }
}
