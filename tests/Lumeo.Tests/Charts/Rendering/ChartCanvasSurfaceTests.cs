using Bunit;
using Lumeo.Tests.Helpers;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Charts.Rendering;

public class ChartCanvasSurfaceTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public Task InitializeAsync()
    {
        _ctx.AddLumeoServices();
        var module = _ctx.JSInterop.SetupModule("./_content/Lumeo.Charts/js/chart-interop.js");
        module.Mode = Bunit.JSRuntimeMode.Loose;
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_A_Canvas_Element_With_The_Given_Id()
    {
        var cut = _ctx.Render<L.ChartCanvasSurface>(p => p.Add(b => b.ElementId, "my-canvas"));

        var canvas = cut.Find("canvas");
        Assert.Equal("my-canvas", canvas.GetAttribute("id"));
    }

    [Fact]
    public void Backing_Store_Size_Scales_By_DevicePixelRatio()
    {
        var cut = _ctx.Render<L.ChartCanvasSurface>(p => p
            .Add(b => b.ElementId, "c1")
            .Add(b => b.Width, 200)
            .Add(b => b.Height, 100)
            .Add(b => b.DevicePixelRatio, 2));

        var canvas = cut.Find("canvas");
        Assert.Equal("400", canvas.GetAttribute("width"));
        Assert.Equal("200", canvas.GetAttribute("height"));
        Assert.Contains("width:200px", canvas.GetAttribute("style"));
        Assert.Contains("height:100px", canvas.GetAttribute("style"));
    }

    [Fact]
    public void DevicePixelRatio_Below_One_Is_Clamped_To_One_For_The_Backing_Store()
    {
        var cut = _ctx.Render<L.ChartCanvasSurface>(p => p
            .Add(b => b.ElementId, "c1")
            .Add(b => b.Width, 100)
            .Add(b => b.Height, 50)
            .Add(b => b.DevicePixelRatio, 0));

        var canvas = cut.Find("canvas");
        Assert.Equal("100", canvas.GetAttribute("width"));
    }
}
