using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.ScrollArea;

public class ScrollAreaTests : IDisposable
{
    private readonly BunitContext _ctx = new();

    public ScrollAreaTests()
    {
        _ctx.AddLumeoServices();
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public void Renders_Div_Container()
    {
        var cut = _ctx.Render<L.ScrollArea>(p => p
            .AddChildContent("content"));

        Assert.NotNull(cut.Find("div"));
    }

    [Fact]
    public void Has_Overflow_Auto_Class()
    {
        var cut = _ctx.Render<L.ScrollArea>(p => p
            .AddChildContent("content"));

        var div = cut.Find("div");
        Assert.Contains("overflow-auto", div.GetAttribute("class"));
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
        Assert.Contains("overflow-auto", div.GetAttribute("class"));
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

    [Fact]
    public void Has_Webkit_Scrollbar_Styling()
    {
        var cut = _ctx.Render<L.ScrollArea>(p => p
            .AddChildContent("content"));

        var div = cut.Find("div");
        var cls = div.GetAttribute("class");
        Assert.Contains("[&::-webkit-scrollbar]", cls);
    }
}
