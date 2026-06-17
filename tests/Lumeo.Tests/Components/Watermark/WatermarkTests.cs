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

    // --- #290: image watermark mode ---

    [Fact]
    public void Image_Source_Renders_As_Overlay_Background()
    {
        var cut = _ctx.Render<Lumeo.Watermark>(p => p
            .Add(w => w.Image, "/logo.png"));

        var overlay = cut.Find("div.pointer-events-none");
        var style = overlay.GetAttribute("style") ?? "";
        Assert.Contains("url(\"/logo.png\")", style);
        Assert.Contains("background-repeat: repeat", style);
    }

    [Fact]
    public void Image_Mode_Uses_Image_Dimensions_For_Background_Size()
    {
        var cut = _ctx.Render<Lumeo.Watermark>(p => p
            .Add(w => w.Image, "/logo.png")
            .Add(w => w.ImageWidth, 200)
            .Add(w => w.ImageHeight, 80));

        var style = cut.Find("div.pointer-events-none").GetAttribute("style") ?? "";
        Assert.Contains("background-size: 200px 80px", style);
    }

    [Fact]
    public void Image_Takes_Precedence_Over_Text()
    {
        var cut = _ctx.Render<Lumeo.Watermark>(p => p
            .Add(w => w.Text, "Confidential")
            .Add(w => w.Image, "/logo.png"));

        var style = cut.Find("div.pointer-events-none").GetAttribute("style") ?? "";
        Assert.Contains("/logo.png", style);
        // The SVG text data-uri must not be emitted when an image is supplied.
        Assert.DoesNotContain("data:image/svg+xml", style);
    }

    [Fact]
    public void Image_Opacity_Applied_To_Overlay()
    {
        var cut = _ctx.Render<Lumeo.Watermark>(p => p
            .Add(w => w.Image, "/logo.png")
            .Add(w => w.Opacity, 0.25));

        var style = cut.Find("div.pointer-events-none").GetAttribute("style") ?? "";
        Assert.Contains("opacity: 0.25", style);
    }

    [Fact]
    public void No_Image_Falls_Back_To_Text_Svg()
    {
        var cut = _ctx.Render<Lumeo.Watermark>(p => p
            .Add(w => w.Text, "Confidential"));

        var style = cut.Find("div.pointer-events-none").GetAttribute("style") ?? "";
        Assert.Contains("data:image/svg+xml", style);
    }
}
