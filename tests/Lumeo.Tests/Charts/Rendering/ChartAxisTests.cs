using Bunit;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Charts.Rendering;

public class ChartAxisTests
{
    private readonly BunitContext _ctx = new();

    private static readonly L.ChartAxisTick[] Ticks =
    {
        new(0, "0"), new(100, "50"), new(200, "100"),
    };

    // Assert on rendered/computed SVG attribute VALUES, not class strings —
    // per this repo's own documented testability trap ("a class in the DOM is
    // not a style on the screen"; here the equivalent risk is "a parameter set
    // in C# is not necessarily the coordinate rendered to the DOM").

    [Fact]
    public void Bottom_Axis_Renders_One_Gridline_Per_Tick()
    {
        var cut = _ctx.Render<L.ChartAxis>(p => p
            .Add(b => b.Ticks, Ticks)
            .Add(b => b.Orientation, L.ChartAxisOrientation.Bottom)
            .Add(b => b.GridStart, 0)
            .Add(b => b.GridEnd, 250));

        var lines = cut.FindAll("line");
        // 3 gridlines + 1 axis baseline
        Assert.Equal(4, lines.Count);
    }

    [Fact]
    public void Bottom_Axis_Gridlines_Are_Vertical_At_Tick_X_Positions()
    {
        var cut = _ctx.Render<L.ChartAxis>(p => p
            .Add(b => b.Ticks, Ticks)
            .Add(b => b.Orientation, L.ChartAxisOrientation.Bottom)
            .Add(b => b.GridStart, 0)
            .Add(b => b.GridEnd, 250)
            .Add(b => b.ShowAxisLine, false));

        var lines = cut.FindAll("line");
        Assert.Equal(3, lines.Count);
        Assert.Equal("100", lines[1].GetAttribute("x1"));
        Assert.Equal("100", lines[1].GetAttribute("x2"));
        Assert.Equal("0", lines[1].GetAttribute("y1"));
        Assert.Equal("250", lines[1].GetAttribute("y2"));
    }

    [Fact]
    public void Left_Axis_Gridlines_Are_Horizontal_At_Tick_Y_Positions()
    {
        var cut = _ctx.Render<L.ChartAxis>(p => p
            .Add(b => b.Ticks, Ticks)
            .Add(b => b.Orientation, L.ChartAxisOrientation.Left)
            .Add(b => b.GridStart, 0)
            .Add(b => b.GridEnd, 300)
            .Add(b => b.ShowAxisLine, false));

        var lines = cut.FindAll("line");
        Assert.Equal("0", lines[1].GetAttribute("x1"));
        Assert.Equal("300", lines[1].GetAttribute("x2"));
        Assert.Equal("100", lines[1].GetAttribute("y1"));
        Assert.Equal("100", lines[1].GetAttribute("y2"));
    }

    [Fact]
    public void ShowGridLines_False_Renders_No_Gridlines_But_Keeps_Axis_Line()
    {
        var cut = _ctx.Render<L.ChartAxis>(p => p
            .Add(b => b.Ticks, Ticks)
            .Add(b => b.ShowGridLines, false)
            .Add(b => b.ShowAxisLine, true));

        Assert.Single(cut.FindAll("line"));
    }

    [Fact]
    public void Tick_Labels_Render_As_Text_Elements_With_Correct_Content()
    {
        var cut = _ctx.Render<L.ChartAxis>(p => p
            .Add(b => b.Ticks, Ticks)
            .Add(b => b.Orientation, L.ChartAxisOrientation.Bottom)
            .Add(b => b.AxisLinePosition, 300));

        var texts = cut.FindAll("text");
        Assert.Equal(3, texts.Count);
        Assert.Equal("0", texts[0].TextContent);
        Assert.Equal("50", texts[1].TextContent);
        Assert.Equal("100", texts[2].TextContent);
    }

    [Fact]
    public void Bottom_Axis_Labels_Sit_Below_The_Axis_Line_By_LabelOffset()
    {
        var cut = _ctx.Render<L.ChartAxis>(p => p
            .Add(b => b.Ticks, new[] { new L.ChartAxisTick(50, "x") })
            .Add(b => b.Orientation, L.ChartAxisOrientation.Bottom)
            .Add(b => b.AxisLinePosition, 200)
            .Add(b => b.LabelOffset, 10));

        var text = cut.Find("text");
        Assert.Equal("210", text.GetAttribute("y")); // 200 + 10
        Assert.Equal("50", text.GetAttribute("x"));
        Assert.Equal("middle", text.GetAttribute("text-anchor"));
    }

    [Fact]
    public void Left_Axis_Labels_Right_Align_Toward_The_Axis_Line()
    {
        var cut = _ctx.Render<L.ChartAxis>(p => p
            .Add(b => b.Ticks, new[] { new L.ChartAxisTick(50, "x") })
            .Add(b => b.Orientation, L.ChartAxisOrientation.Left)
            .Add(b => b.AxisLinePosition, 200)
            .Add(b => b.LabelOffset, 8));

        var text = cut.Find("text");
        Assert.Equal("192", text.GetAttribute("x")); // 200 - 8
        Assert.Equal("end", text.GetAttribute("text-anchor"));
    }

    [Fact]
    public void Label_Text_Is_HtmlEncoded()
    {
        var cut = _ctx.Render<L.ChartAxis>(p => p
            .Add(b => b.Ticks, new[] { new L.ChartAxisTick(0, "A & B < C") }));

        Assert.Equal("A & B < C", cut.Find("text").TextContent);
    }
}
