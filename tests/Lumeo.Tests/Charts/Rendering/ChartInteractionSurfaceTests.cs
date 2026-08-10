using Bunit;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Charts.Rendering;

public class ChartInteractionSurfaceTests : IAsyncLifetime
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
    public void Renders_A_Transparent_Overlay_Rect_Sized_To_The_Plot()
    {
        var cut = _ctx.Render<L.ChartInteractionSurface>(p => p
            .Add(b => b.PlotX, 40).Add(b => b.PlotY, 10)
            .Add(b => b.PlotWidth, 300).Add(b => b.PlotHeight, 200)
            .Add(b => b.PointCount, 50));

        var rect = cut.Find("rect");
        Assert.Equal("40", rect.GetAttribute("x"));
        Assert.Equal("10", rect.GetAttribute("y"));
        Assert.Equal("300", rect.GetAttribute("width"));
        Assert.Equal("200", rect.GetAttribute("height"));
        Assert.Equal("transparent", rect.GetAttribute("fill"));
    }

    [Fact]
    public void Overlay_Is_Focusable_With_An_Aria_Label()
    {
        var cut = _ctx.Render<L.ChartInteractionSurface>(p => p
            .Add(b => b.PointCount, 10)
            .Add(b => b.AriaLabel, "Line chart with 10 points"));

        var rect = cut.Find("rect");
        Assert.Equal("0", rect.GetAttribute("tabindex"));
        Assert.Equal("Line chart with 10 points", rect.GetAttribute("aria-label"));
    }

    [Fact]
    public async Task ArrowRight_From_No_Selection_Reports_Index_Zero()
    {
        int? reported = null;
        var cut = _ctx.Render<L.ChartInteractionSurface>(p => p
            .Add(b => b.PointCount, 10)
            .Add(b => b.OnIndexChanged, EventCallback.Factory.Create<int?>(this, i => reported = i)));

        await cut.InvokeAsync(() => cut.Find("rect").KeyDown("ArrowRight"));

        Assert.Equal(0, reported);
    }

    [Fact]
    public async Task End_Key_Jumps_To_The_Last_Point()
    {
        int? reported = null;
        var cut = _ctx.Render<L.ChartInteractionSurface>(p => p
            .Add(b => b.PointCount, 25)
            .Add(b => b.OnIndexChanged, EventCallback.Factory.Create<int?>(this, i => reported = i)));

        await cut.InvokeAsync(() => cut.Find("rect").KeyDown("End"));

        Assert.Equal(24, reported);
    }

    [Fact]
    public async Task ArrowUp_On_A_MultiSeries_Chart_Switches_The_Active_Series()
    {
        int? reportedSeries = null;
        var cut = _ctx.Render<L.ChartInteractionSurface>(p => p
            .Add(b => b.PointCount, 10)
            .Add(b => b.SeriesCount, 3)
            .Add(b => b.OnSeriesChanged, EventCallback.Factory.Create<int>(this, i => reportedSeries = i)));

        await cut.InvokeAsync(() => cut.Find("rect").KeyDown("ArrowUp"));

        Assert.Equal(2, reportedSeries); // wraps backward from 0 -> 2
    }

    [Fact]
    public async Task ArrowUp_On_A_SingleSeries_Chart_Does_Nothing()
    {
        var seriesChanged = false;
        var cut = _ctx.Render<L.ChartInteractionSurface>(p => p
            .Add(b => b.PointCount, 10)
            .Add(b => b.SeriesCount, 1)
            .Add(b => b.OnSeriesChanged, EventCallback.Factory.Create<int>(this, _ => seriesChanged = true)));

        await cut.InvokeAsync(() => cut.Find("rect").KeyDown("ArrowUp"));

        Assert.False(seriesChanged);
    }

    [Fact]
    public async Task JSInvokable_PointerIndex_Callback_Forwards_The_Index()
    {
        int? reported = null;
        var cut = _ctx.Render<L.ChartInteractionSurface>(p => p
            .Add(b => b.PointCount, 10)
            .Add(b => b.OnIndexChanged, EventCallback.Factory.Create<int?>(this, i => reported = i)));

        await cut.InvokeAsync(() => cut.Instance.OnChartPointerIndex(7));

        Assert.Equal(7, reported);
    }

    [Fact]
    public async Task JSInvokable_PointerLeave_Callback_Reports_Null()
    {
        int? reported = 5;
        var cut = _ctx.Render<L.ChartInteractionSurface>(p => p
            .Add(b => b.PointCount, 10)
            .Add(b => b.OnIndexChanged, EventCallback.Factory.Create<int?>(this, i => reported = i)));

        await cut.InvokeAsync(() => cut.Instance.OnChartPointerLeave());

        Assert.Null(reported);
    }
}
