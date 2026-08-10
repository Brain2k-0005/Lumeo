using Bunit;
using Lumeo.Tests.Helpers;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Charts.Rendering;

public class ChartZoomSliderTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public Task InitializeAsync()
    {
        _ctx.AddLumeoServices();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Handles_Are_Positioned_From_Start_And_End_Fractions()
    {
        var cut = _ctx.Render<L.ChartZoomSlider>(p => p
            .Add(b => b.Width, 200)
            .Add(b => b.Start, 0.25)
            .Add(b => b.End, 0.75));

        var handles = cut.FindAll(".lumeo-chart-zoom-handle");
        Assert.Equal(2, handles.Count);
        Assert.Contains("left:50px", handles[0].GetAttribute("style"));  // 0.25 * 200
        Assert.Contains("left:150px", handles[1].GetAttribute("style")); // 0.75 * 200
    }

    [Fact]
    public void Window_Rect_Spans_Between_The_Two_Handles()
    {
        var cut = _ctx.Render<L.ChartZoomSlider>(p => p
            .Add(b => b.Width, 200)
            .Add(b => b.Start, 0.25)
            .Add(b => b.End, 0.75));

        var window = cut.Find(".lumeo-chart-zoom-window");
        Assert.Contains("left:50px", window.GetAttribute("style"));
        Assert.Contains("width:100px", window.GetAttribute("style"));
    }

    [Fact]
    public void Full_Range_Window_Spans_The_Whole_Track()
    {
        var cut = _ctx.Render<L.ChartZoomSlider>(p => p.Add(b => b.Width, 300));

        var window = cut.Find(".lumeo-chart-zoom-window");
        Assert.Contains("left:0px", window.GetAttribute("style"));
        Assert.Contains("width:300px", window.GetAttribute("style"));
    }
}
