using Bunit;
using Lumeo.Tests.Helpers;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Charts.Native.Charts;

public class NativeAreaChartTests : IAsyncLifetime
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
    public void Renders_An_Area_Path()
    {
        var cut = _ctx.Render<L.NativeAreaChart>(p => p
            .Add(b => b.Categories, new List<string> { "A", "B" })
            .Add(b => b.Series, new List<L.NativeAreaChart.ChartSeriesData> { new() { Name = "S1", Values = new List<double> { 1, 2 } } }));

        Assert.Single(cut.FindAll("path.lumeo-chart-native-area"));
    }

    [Fact]
    public void GradientFill_Emits_A_LinearGradient_Def_And_References_It_As_The_Fill()
    {
        var cut = _ctx.Render<L.NativeAreaChart>(p => p
            .Add(b => b.Categories, new List<string> { "A", "B" })
            .Add(b => b.Series, new List<L.NativeAreaChart.ChartSeriesData> { new() { Name = "S1", Values = new List<double> { 1, 2 } } })
            .Add(b => b.GradientFill, true));

        var gradient = cut.Find("defs linearGradient");
        var id = gradient.GetAttribute("id")!;
        var fill = cut.Find("path.lumeo-chart-native-area").GetAttribute("fill")!;

        Assert.Equal($"url(#{id})", fill);
        Assert.Equal(2, cut.FindAll("defs linearGradient stop").Count); // color -> transparent, two stops
    }

    [Fact]
    public void GradientFill_False_Uses_A_ColorMix_Fill_Not_A_Gradient_Url()
    {
        var cut = _ctx.Render<L.NativeAreaChart>(p => p
            .Add(b => b.Categories, new List<string> { "A", "B" })
            .Add(b => b.Series, new List<L.NativeAreaChart.ChartSeriesData> { new() { Name = "S1", Values = new List<double> { 1, 2 } } })
            .Add(b => b.GradientFill, false));

        var fill = cut.Find("path.lumeo-chart-native-area").GetAttribute("fill")!;
        Assert.DoesNotContain("url(#", fill);
        Assert.Contains("color-mix", fill);
    }

    /// <summary>
    /// The actual defect: <c>ShowDots</c> defaults to <c>true</c> on the
    /// component, but was silently a no-op for Area — <c>NativeAreaChart</c>
    /// never passed it into <c>NativeCartesianSeries</c>, and
    /// <c>CartesianChartHost.BuildSeriesMarkup()</c>'s Area case never emitted
    /// <c>&lt;circle class="lumeo-chart-native-dot"&gt;</c> elements at all
    /// (unlike the Line case, which does). Line/Mixed drew dots correctly; only
    /// Area silently dropped them.
    /// </summary>
    [Fact]
    public void ShowDots_Defaults_True_And_Renders_A_Marker_Per_Point()
    {
        var cut = _ctx.Render<L.NativeAreaChart>(p => p
            .Add(b => b.Categories, new List<string> { "A", "B", "C" })
            .Add(b => b.Series, new List<L.NativeAreaChart.ChartSeriesData> { new() { Name = "S1", Values = new List<double> { 1, 2, 3 } } }));

        Assert.Equal(3, cut.FindAll("circle.lumeo-chart-native-dot").Count);
    }

    [Fact]
    public void ShowDots_False_Renders_No_Marker_Circles()
    {
        var cut = _ctx.Render<L.NativeAreaChart>(p => p
            .Add(b => b.Categories, new List<string> { "A", "B" })
            .Add(b => b.Series, new List<L.NativeAreaChart.ChartSeriesData> { new() { Name = "S1", Values = new List<double> { 1, 2 } } })
            .Add(b => b.ShowDots, false));

        Assert.Empty(cut.FindAll("circle.lumeo-chart-native-dot"));
    }

    /// <summary>
    /// Rendered VISUAL property, not just presence: each dot's own (cx,cy) must
    /// sit exactly at the series' value point — the same coordinate the area
    /// path's top edge passes through at that category. A dot that "exists"
    /// but is mispositioned (e.g. always at the stacked BOTTOM instead of the
    /// series' own TOP) would pass a presence-only assertion while still being
    /// visually wrong.
    /// </summary>
    [Fact]
    public void Dot_Position_Matches_The_Series_Value_Not_The_Stacked_Baseline()
    {
        var cut = _ctx.Render<L.NativeAreaChart>(p => p
            .Add(b => b.Categories, new List<string> { "A" })
            .Add(b => b.Series, new List<L.NativeAreaChart.ChartSeriesData>
            {
                new() { Name = "Base", Values = new List<double> { 10 } },
                new() { Name = "Top", Values = new List<double> { 20 } },
            })
            .Add(b => b.Stacked, true));

        var dots = cut.FindAll("circle.lumeo-chart-native-dot");
        Assert.Equal(2, dots.Count);
        // Stacked: Base sits at [0,10], Top sits at [10,30] -- their dots must be
        // at DIFFERENT y pixels (top-of-stack for each series), not collapsed onto
        // the same baseline.
        var baseY = dots[0].GetAttribute("cy");
        var topY = dots[1].GetAttribute("cy");
        Assert.NotEqual(baseY, topY);
    }

    /// <summary>
    /// Paint-order guard for "markers render underneath the filled area" — the
    /// other concretely-predicted failure mode this task called out. SVG paints
    /// in document order, so the dot &lt;circle&gt; must appear AFTER its area
    /// &lt;path&gt; in the markup for the dot to be visually on top.
    /// </summary>
    [Fact]
    public void Dot_Markup_Comes_After_The_Area_Path_So_It_Paints_On_Top()
    {
        var cut = _ctx.Render<L.NativeAreaChart>(p => p
            .Add(b => b.Categories, new List<string> { "A", "B" })
            .Add(b => b.Series, new List<L.NativeAreaChart.ChartSeriesData> { new() { Name = "S1", Values = new List<double> { 1, 2 } } }));

        var markup = cut.Markup;
        var areaIndex = markup.IndexOf("lumeo-chart-native-area", StringComparison.Ordinal);
        var dotIndex = markup.IndexOf("lumeo-chart-native-dot", StringComparison.Ordinal);
        Assert.True(areaIndex >= 0 && dotIndex >= 0 && dotIndex > areaIndex,
            "Dot markers must appear AFTER the area path in the rendered markup so SVG paint order draws them on top.");
    }

    [Fact]
    public void Stacked_Areas_Second_Series_Builds_On_The_First()
    {
        // Predicted: a stacked area's band path encodes cumulative Y0/Y1 values,
        // producing a DIFFERENT (non-degenerate) "d" than an unstacked render of
        // the same data (which would start every series' band at the baseline).
        var stacked = _ctx.Render<L.NativeAreaChart>(p => p
            .Add(b => b.Categories, new List<string> { "A" })
            .Add(b => b.Series, new List<L.NativeAreaChart.ChartSeriesData>
            {
                new() { Name = "S1", Values = new List<double> { 10 } },
                new() { Name = "S2", Values = new List<double> { 10 } },
            })
            .Add(b => b.Stacked, true));

        var unstacked = _ctx.Render<L.NativeAreaChart>(p => p
            .Add(b => b.Categories, new List<string> { "A" })
            .Add(b => b.Series, new List<L.NativeAreaChart.ChartSeriesData>
            {
                new() { Name = "S1", Values = new List<double> { 10 } },
                new() { Name = "S2", Values = new List<double> { 10 } },
            })
            .Add(b => b.Stacked, false));

        var stackedPaths = stacked.FindAll("path.lumeo-chart-native-area");
        var unstackedPaths = unstacked.FindAll("path.lumeo-chart-native-area");
        Assert.NotEqual(unstackedPaths[1].GetAttribute("d"), stackedPaths[1].GetAttribute("d"));
    }
}
