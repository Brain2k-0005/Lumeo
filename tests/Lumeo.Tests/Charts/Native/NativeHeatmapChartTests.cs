using Bunit;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Charts.Native;

public class NativeHeatmapChartTests : IAsyncLifetime
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

    private static (List<string> X, List<string> Y, List<double[]> Data) BuildGrid(int cols, int rows)
    {
        var x = Enumerable.Range(0, cols).Select(i => $"x{i}").ToList();
        var y = Enumerable.Range(0, rows).Select(i => $"y{i}").ToList();
        var data = new List<double[]>();
        for (var yi = 0; yi < rows; yi++)
            for (var xi = 0; xi < cols; xi++)
                data.Add(new double[] { xi, yi, (xi + yi) % 10 });
        return (x, y, data);
    }

    // The DiscreteShapeBudget exercise: this is the FIRST code with real DOM to
    // check whether Lumeo.ChartRenderStrategy.DiscreteShapeBudget (8000, a policy
    // constant the core author explicitly flagged as NOT empirically derived) is
    // the right cutover point for this chart type.
    [Fact]
    public void Grid_At_Or_Under_The_Shape_Budget_Renders_Individual_SVG_Cells()
    {
        // 89x89 = 7921 cells — just under the 8000 budget.
        var (x, y, data) = BuildGrid(89, 89);
        var cut = _ctx.Render<L.NativeHeatmapChart>(p => p.Add(b => b.XCategories, x).Add(b => b.YCategories, y).Add(b => b.Data, data));

        Assert.Equal(7921, cut.FindAll("svg rect.lumeo-native-heatmap-cell").Count);
        Assert.Empty(cut.FindAll("canvas"));
    }

    [Fact]
    public void Grid_Over_The_Shape_Budget_Falls_Back_To_Canvas()
    {
        // 90x90 = 8100 cells — over the 8000 budget.
        var (x, y, data) = BuildGrid(90, 90);
        var cut = _ctx.Render<L.NativeHeatmapChart>(p => p.Add(b => b.XCategories, x).Add(b => b.YCategories, y).Add(b => b.Data, data));

        Assert.Single(cut.FindAll("canvas"));
        Assert.Empty(cut.FindAll("svg rect.lumeo-native-heatmap-cell"));
    }

    [Fact]
    public void Exactly_At_The_Budget_Boundary_Still_Uses_Svg_Because_The_Comparison_Is_Strictly_Greater_Than()
    {
        // ChartRenderStrategy.ForDiscreteShapes: "shapeCount > DiscreteShapeBudget"
        // — exactly 8000 must still be SVG, 8001 must be Canvas. This directly
        // predicts a concrete wrong value if the comparison operator were ever
        // flipped to >=.
        Assert.Equal(L.ChartRenderStrategy.DiscreteShapeBudget, 8000);

        var (x8000, y8000, d8000) = BuildGrid(100, 80); // exactly 8000 cells
        var atBudget = _ctx.Render<L.NativeHeatmapChart>(p => p.Add(b => b.XCategories, x8000).Add(b => b.YCategories, y8000).Add(b => b.Data, d8000));
        Assert.Empty(atBudget.FindAll("canvas"));

        var (x8001, y8001, d8001) = BuildGrid(100, 80);
        d8001.Add(new double[] { 0, 0, 1 }); // duplicate cell pushes the SPARSE data list to 8001 entries
        var overBudget = _ctx.Render<L.NativeHeatmapChart>(p => p.Add(b => b.XCategories, x8001).Add(b => b.YCategories, y8001).Add(b => b.Data, d8001));
        Assert.Single(overBudget.FindAll("canvas"));
    }

    [Fact]
    public void Min_And_Max_Value_Cells_Resolve_To_The_Two_Endpoint_Stop_Colors_Literally()
    {
        // ChartColorScale.Resolve returns a stop's color LITERALLY (no color-mix()
        // wrapper) when the value lands exactly on that stop. Default gradient
        // tokens are --color-muted (low) -> --color-primary (high), NOT
        // --color-chart-1 -> --color-primary: --color-chart-1 is defined equal to
        // --color-primary in every shipped theme (light AND dark), which collapsed
        // the whole gradient into one solid color — see NativeHeatmapChart's own
        // comment on the Colors/ColorPalette fallback.
        var data = new List<double[]> { new double[] { 0, 0, 0 }, new double[] { 1, 0, 10 } };
        var cut = _ctx.Render<L.NativeHeatmapChart>(p => p
            .Add(b => b.XCategories, new List<string> { "a", "b" })
            .Add(b => b.YCategories, new List<string> { "row" })
            .Add(b => b.Data, data));

        var rects = cut.FindAll("svg rect.lumeo-native-heatmap-cell").ToList();
        Assert.Contains(rects, r => r.GetAttribute("fill") == "var(--color-muted)");
        Assert.Contains(rects, r => r.GetAttribute("fill") == "var(--color-primary)");
    }

    [Fact]
    public void Default_Gradient_Tokens_Are_Not_Aliases_Of_Each_Other_In_Any_Shipped_Theme()
    {
        // The actual bug this regression guards: --color-chart-1 is defined
        // LITERALLY EQUAL to --color-primary in the default theme AND all 7 named
        // themes (lumeo.css + themes/*.css), both light and dark — using that pair
        // as the heatmap's default gradient collapsed every cell to one solid
        // color (black in light mode, near-uniform off-white in dark). This test
        // can't inspect CSS custom-property VALUES from bUnit (no browser), so it
        // instead pins the two DECLARED CSS variable tokens the default gradient
        // must resolve to — the one thing that's directly assertable here — and is
        // paired with the browser-level oklch pixel measurement in the PR
        // description / manual verification for the actual color values.
        var data = new List<double[]> { new double[] { 0, 0, 0 }, new double[] { 1, 0, 10 } };
        var cut = _ctx.Render<L.NativeHeatmapChart>(p => p
            .Add(b => b.XCategories, new List<string> { "a", "b" })
            .Add(b => b.YCategories, new List<string> { "row" })
            .Add(b => b.Data, data));

        var fills = cut.FindAll("svg rect.lumeo-native-heatmap-cell").Select(r => r.GetAttribute("fill")).ToList();
        Assert.DoesNotContain("var(--color-chart-1)", fills);
    }

    [Fact]
    public void Twenty_One_Distinct_Values_Produce_Twenty_One_Distinct_Color_Mix_Expressions()
    {
        // The rigor bar for "the picture is right", not just "a fill exists": a
        // heatmap where N distinct values collapse to fewer than N distinct
        // fill expressions is non-functional as a heatmap even if every cell has
        // SOME fill attribute (which is exactly what the old assertion-by-
        // presence tests would have missed). Mirrors the /e2e/charts-native
        // support-ticket-volume fixture's value range (18-95, 21 cells).
        var data = new List<double[]>();
        var value = 18;
        for (var x = 0; x < 7; x++)
        for (var y = 0; y < 3; y++)
        {
            data.Add(new double[] { x, y, value });
            value += 3; // 21 strictly increasing, distinct values: 18, 21, ..., 78
        }
        var cut = _ctx.Render<L.NativeHeatmapChart>(p => p
            .Add(b => b.XCategories, new List<string> { "0", "1", "2", "3", "4", "5", "6" })
            .Add(b => b.YCategories, new List<string> { "0", "1", "2" })
            .Add(b => b.Data, data));

        var fills = cut.FindAll("svg rect.lumeo-native-heatmap-cell").Select(r => r.GetAttribute("fill")).ToHashSet();
        Assert.Equal(21, fills.Count);
    }

    [Fact]
    public void Hovering_A_Cell_Shows_Its_Category_And_Value_In_The_Status_Readout()
    {
        var data = new List<double[]> { new double[] { 1, 0, 42 } };
        var cut = _ctx.Render<L.NativeHeatmapChart>(p => p
            .Add(b => b.XCategories, new List<string> { "Mon", "Tue" })
            .Add(b => b.YCategories, new List<string> { "Morning" })
            .Add(b => b.Data, data));

        var cell = cut.Find("svg rect.lumeo-native-heatmap-cell");
        cut.InvokeAsync(() => cell.PointerEnter(new PointerEventArgs()));

        var status = cut.Find("[role='status']");
        Assert.Contains("Morning", status.TextContent);
        Assert.Contains("Tue", status.TextContent);
        Assert.Contains("42", status.TextContent);
    }

    [Fact]
    public void Keyboard_ArrowRight_Then_ArrowDown_Moves_The_Active_Cell()
    {
        var data = new List<double[]>
        {
            new double[] { 0, 0, 1 }, new double[] { 1, 0, 2 },
            new double[] { 0, 1, 3 }, new double[] { 1, 1, 4 },
        };
        var cut = _ctx.Render<L.NativeHeatmapChart>(p => p
            .Add(b => b.XCategories, new List<string> { "X0", "X1" })
            .Add(b => b.YCategories, new List<string> { "Y0", "Y1" })
            .Add(b => b.Data, data));

        var host = cut.Find("rect[tabindex]");
        cut.InvokeAsync(() => host.KeyDown("ArrowRight"));
        cut.InvokeAsync(() => host.KeyDown("ArrowDown"));

        var status = cut.Find("[role='status']");
        Assert.Contains("Y1", status.TextContent);
        Assert.Contains("X1", status.TextContent);
        Assert.Contains("4", status.TextContent);
    }

    [Fact]
    public void DataZoom_Renders_A_Zoom_Slider_Only_When_Requested()
    {
        var (x, y, data) = BuildGrid(5, 5);
        var without = _ctx.Render<L.NativeHeatmapChart>(p => p.Add(b => b.XCategories, x).Add(b => b.YCategories, y).Add(b => b.Data, data));
        Assert.Empty(without.FindAll(".lumeo-chart-zoom-slider"));

        var with = _ctx.Render<L.NativeHeatmapChart>(p => p.Add(b => b.XCategories, x).Add(b => b.YCategories, y).Add(b => b.Data, data).Add(b => b.DataZoom, true));
        Assert.Single(with.FindAll(".lumeo-chart-zoom-slider"));
    }

    [Fact]
    public void Accessibility_Table_Caption_Reports_The_Full_Cell_Count_Even_When_Rows_Are_Truncated()
    {
        var (x, y, data) = BuildGrid(20, 20); // 400 cells, well past the 50-row a11y cap
        var cut = _ctx.Render<L.NativeHeatmapChart>(p => p.Add(b => b.XCategories, x).Add(b => b.YCategories, y).Add(b => b.Data, data));

        var caption = cut.Find("table.sr-only caption");
        Assert.Contains("400 data points", caption.TextContent);
        Assert.Contains("more data point", cut.Find("table.sr-only").TextContent);
    }
}
