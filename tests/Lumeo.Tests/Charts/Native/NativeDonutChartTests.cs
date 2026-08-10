using Bunit;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Charts.Native;

public class NativeDonutChartTests
{
    private readonly BunitContext _ctx = new();

    private static List<L.NativeDonutChart.DonutChartData> OneSlice() => new()
    {
        new() { Name = "Only", Value = 42 },
    };

    [Fact]
    public void Default_Inner_Outer_Radius_Percentages_Produce_A_Ring_Not_A_Solid_Wedge()
    {
        // MaxRadius = 92 (matches the type's private constant); default InnerRadius
        // "50%" / OuterRadius "70%" => inner = 46, outer = 64.4.
        var cut = _ctx.Render<L.NativeDonutChart>(p => p.Add(b => b.Data, OneSlice()));

        const double cx = 210, cy = 140;
        var expected = L.ChartArcPath.Build(cx, cy, 46, 64.4, -Math.PI / 2, -Math.PI / 2 + Math.PI * 2 - 0.0001);

        var path = cut.Find("svg path");
        Assert.Equal(expected, path.GetAttribute("d"));
    }

    [Fact]
    public void Custom_Radius_Percentages_Are_Parsed_And_Scaled_From_MaxRadius()
    {
        var cut = _ctx.Render<L.NativeDonutChart>(p => p
            .Add(b => b.Data, OneSlice())
            .Add(b => b.InnerRadius, "25%")
            .Add(b => b.OuterRadius, "100%"));

        const double cx = 210, cy = 140;
        // 25% of 92 = 23, 100% of 92 = 92.
        var expected = L.ChartArcPath.Build(cx, cy, 23, 92, -Math.PI / 2, -Math.PI / 2 + Math.PI * 2 - 0.0001);

        var path = cut.Find("svg path");
        Assert.Equal(expected, path.GetAttribute("d"));
    }

    [Fact]
    public void CenterLabel_Renders_As_Centered_Text_When_Set()
    {
        var cut = _ctx.Render<L.NativeDonutChart>(p => p
            .Add(b => b.Data, OneSlice())
            .Add(b => b.CenterLabel, "1,234"));

        var texts = cut.FindAll("svg text");
        var centerText = texts.Single(t => t.TextContent == "1,234");
        Assert.Equal("middle", centerText.GetAttribute("text-anchor"));
    }

    [Fact]
    public void No_CenterLabel_By_Default()
    {
        var cut = _ctx.Render<L.NativeDonutChart>(p => p.Add(b => b.Data, OneSlice()));

        var texts = cut.FindAll("svg text").Select(t => t.TextContent);
        Assert.DoesNotContain(texts, t => t.Trim().Length > 0 && !t.Contains("Only") && !t.Contains('%'));
    }

    [Fact]
    public void Unparseable_Radius_String_Falls_Back_To_The_Documented_Default_Percentage()
    {
        var cut = _ctx.Render<L.NativeDonutChart>(p => p
            .Add(b => b.Data, OneSlice())
            .Add(b => b.InnerRadius, "not-a-percent"));

        const double cx = 210, cy = 140;
        // Falls back to the documented default of 50% => inner = 46.
        var expected = L.ChartArcPath.Build(cx, cy, 46, 64.4, -Math.PI / 2, -Math.PI / 2 + Math.PI * 2 - 0.0001);

        var path = cut.Find("svg path");
        Assert.Equal(expected, path.GetAttribute("d"));
    }
}
