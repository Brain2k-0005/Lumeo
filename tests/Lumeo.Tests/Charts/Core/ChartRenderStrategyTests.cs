using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Charts.Core;

public class ChartRenderStrategyTests
{
    [Fact]
    public void Ordered_Series_Defaults_To_Svg()
    {
        Assert.Equal(L.ChartRenderMode.Svg, L.ChartRenderStrategy.ForOrderedSeries(liveHighFrequencyOptIn: false));
    }

    [Fact]
    public void Ordered_Series_Uses_Canvas_Only_When_Explicitly_OptedIn()
    {
        Assert.Equal(L.ChartRenderMode.Canvas, L.ChartRenderStrategy.ForOrderedSeries(liveHighFrequencyOptIn: true));
    }

    [Fact]
    public void Discrete_Shapes_Under_Budget_Render_Svg()
    {
        Assert.Equal(L.ChartRenderMode.Svg,
            L.ChartRenderStrategy.ForDiscreteShapes(shapeCount: L.ChartRenderStrategy.DiscreteShapeBudget, liveHighFrequencyOptIn: false));
    }

    [Fact]
    public void Discrete_Shapes_Over_Budget_Flip_To_Canvas()
    {
        Assert.Equal(L.ChartRenderMode.Canvas,
            L.ChartRenderStrategy.ForDiscreteShapes(shapeCount: L.ChartRenderStrategy.DiscreteShapeBudget + 1, liveHighFrequencyOptIn: false));
    }

    [Fact]
    public void Discrete_Shapes_OptIn_Forces_Canvas_Regardless_Of_Count()
    {
        Assert.Equal(L.ChartRenderMode.Canvas,
            L.ChartRenderStrategy.ForDiscreteShapes(shapeCount: 1, liveHighFrequencyOptIn: true));
    }
}

public class ChartCanvasCommandBuilderTests
{
    [Fact]
    public void Serializes_Op_Args_And_Style_As_CamelCase_Json()
    {
        var commands = new[]
        {
            new L.ChartCanvasCommand("moveTo", new[] { 1.0, 2.0 }),
            new L.ChartCanvasCommand("stroke", Style: new L.ChartCanvasStyle(Color: "#fff", Width: 2)),
        };

        var json = L.ChartCanvasCommandBuilder.ToJson(commands);

        Assert.Contains("\"op\":\"moveTo\"", json);
        Assert.Contains("\"args\":[1,2]", json);
        Assert.Contains("\"color\":\"#fff\"", json);
        Assert.Contains("\"width\":2", json);
    }
}
