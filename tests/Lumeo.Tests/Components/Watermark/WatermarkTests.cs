using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Watermark;

public class WatermarkTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public WatermarkTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Container_With_Relative_Class()
    {
        var cut = _ctx.Render<Lumeo.Watermark>(p => p
            .Add(w => w.Text, "Confidential")
            .AddChildContent("Content"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("relative", cls);
    }

    [Fact]
    public void Renders_Child_Content()
    {
        var cut = _ctx.Render<Lumeo.Watermark>(p => p
            .Add(w => w.Text, "Draft")
            .AddChildContent("<span id='child'>Inner content</span>"));

        Assert.Equal("Inner content", cut.Find("#child").TextContent);
    }

    [Fact]
    public void Renders_Overlay_Div_With_PointerEvents_None()
    {
        var cut = _ctx.Render<Lumeo.Watermark>(p => p
            .Add(w => w.Text, "Watermark"));

        var overlay = cut.Find("div.pointer-events-none");
        Assert.NotNull(overlay);
        Assert.Contains("absolute", overlay.GetAttribute("class"));
        Assert.Contains("inset-0", overlay.GetAttribute("class"));
    }

    [Fact]
    public void Custom_Class_Is_Appended_To_Container()
    {
        var cut = _ctx.Render<Lumeo.Watermark>(p => p
            .Add(w => w.Text, "Draft")
            .Add(w => w.Class, "my-watermark"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("my-watermark", cls);
    }
}
