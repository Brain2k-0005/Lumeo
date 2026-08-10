using Bunit;
using Lumeo.Tests.Helpers;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Charts.Native;

// Rigor standard: every assertion below is a hand-worked (or Python-cross-checked,
// see the PR description) linear-interpolation-percentile computation, not a
// guess — matching Lumeo.ChartStatistics.Quartiles' own documented convention
// (same method NumPy's default uses).
public class NativeBoxPlotChartTests : IAsyncLifetime
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

    private static async Task<string> HoverCategoryAsync(Bunit.IRenderedComponent<L.NativeBoxPlotChart> cut, int index)
    {
        var surface = cut.FindComponent<L.ChartInteractionSurface>();
        await surface.InvokeAsync(() => surface.Instance.OnChartPointerIndex(index));
        return cut.Find(".lumeo-chart-tooltip-host").TextContent;
    }

    [Fact]
    public async Task Even_Sized_Sample_Interpolates_The_Median_Between_The_Two_Middle_Values()
    {
        // [1,2,3,4]: median = (2+3)/2 = 2.5 (interpolated, not either middle value
        // alone) — the classic off-by-one convention trap this repo watches for.
        var samples = new List<List<double>> { new() { 1, 2, 3, 4 } };
        var cut = _ctx.Render<L.NativeBoxPlotChart>(p => p
            .Add(b => b.Categories, new List<string> { "Even" })
            .Add(b => b.Samples, samples));

        var tooltip = await HoverCategoryAsync(cut, 0);

        Assert.Contains("Median 2.5", tooltip);
        Assert.Contains("Q1–Q3 1.75 – 3.25", tooltip);
        Assert.Contains("Whisker 1 – 4", tooltip);
    }

    [Fact]
    public async Task Outlier_Exactly_On_The_1_5_IQR_Boundary_Is_Included_In_The_Whisker_Not_Flagged_As_An_Outlier()
    {
        // [0,10,...,90,150]: Q1=25, Q3=75, IQR=50 => upper fence = 75 + 1.5*50 = 150
        // EXACTLY. The boundary is inclusive (v <= upperFence stays in-whisker), so
        // 150 becomes the whisker-high value itself, not an outlier.
        var samples = new List<double> { 0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 150 };
        var cut = _ctx.Render<L.NativeBoxPlotChart>(p => p
            .Add(b => b.Categories, new List<string> { "Boundary" })
            .Add(b => b.Samples, new List<List<double>> { samples }));

        var tooltip = await HoverCategoryAsync(cut, 0);

        Assert.Contains("Whisker 0 – 150", tooltip);
        Assert.DoesNotContain("outlier", tooltip);
    }

    [Fact]
    public async Task A_Value_Just_Past_The_Boundary_Becomes_A_Real_Outlier_And_The_Whisker_Retreats()
    {
        // Same base sample, but the extreme value is 150.5 instead of exactly 150 —
        // now it's a real outlier and the whisker-high value falls back to 90 (the
        // next-highest in-range value). Predicted via the identical percentile
        // formula ChartStatistics.Quartiles implements.
        var samples = new List<double> { 0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 150.5 };
        var cut = _ctx.Render<L.NativeBoxPlotChart>(p => p
            .Add(b => b.Categories, new List<string> { "PastBoundary" })
            .Add(b => b.Samples, new List<List<double>> { samples }));

        var tooltip = await HoverCategoryAsync(cut, 0);

        Assert.Contains("Whisker 0 – 90", tooltip);
        Assert.Contains("1 outlier(s)", tooltip);
    }

    [Fact]
    public async Task Legacy_PreAggregated_Data_Parameter_Is_Used_When_Samples_Is_Not_Set()
    {
        // [min, Q1, median, Q3, max] shape — the ORIGINAL spec's assumed input,
        // kept for parameter-surface parity. No outlier concept in this mode
        // (whiskers ARE min/max, matching the ECharts convention it was designed for).
        var cut = _ctx.Render<L.NativeBoxPlotChart>(p => p
            .Add(b => b.Categories, new List<string> { "Legacy" })
            .Add(b => b.Data, new List<double[]> { new[] { 5.0, 10.0, 15.0, 20.0, 25.0 } }));

        var tooltip = await HoverCategoryAsync(cut, 0);

        Assert.Contains("Median 15", tooltip);
        Assert.Contains("Q1–Q3 10 – 20", tooltip);
        Assert.Contains("Whisker 5 – 25", tooltip);
    }

    [Fact]
    public void Samples_Takes_Precedence_Over_Data_When_Both_Are_Set()
    {
        var cut = _ctx.Render<L.NativeBoxPlotChart>(p => p
            .Add(b => b.Categories, new List<string> { "Both" })
            .Add(b => b.Samples, new List<List<double>> { new() { 1, 2, 3, 4, 5 } })
            .Add(b => b.Data, new List<double[]> { new[] { 900.0, 901.0, 902.0, 903.0, 904.0 } }));

        // If Data (legacy) had won, the box would render around y-values computed
        // from 900-ish inputs, forcing a totally different Y domain than [1..5].
        var yTicks = cut.FindAll("svg g.lumeo-chart-axis text").Select(t => t.TextContent).ToList();
        Assert.DoesNotContain(yTicks, t => t.Contains("900"));
    }

    [Fact]
    public void DataZoom_Renders_A_Zoom_Slider_Only_When_Requested()
    {
        var categories = new List<string> { "A", "B", "C" };
        var samples = categories.Select(_ => new List<double> { 1, 2, 3 }).ToList();

        var without = _ctx.Render<L.NativeBoxPlotChart>(p => p.Add(b => b.Categories, categories).Add(b => b.Samples, samples));
        Assert.Empty(without.FindAll(".lumeo-chart-zoom-slider"));

        var with = _ctx.Render<L.NativeBoxPlotChart>(p => p.Add(b => b.Categories, categories).Add(b => b.Samples, samples).Add(b => b.DataZoom, true));
        Assert.Single(with.FindAll(".lumeo-chart-zoom-slider"));
    }

    [Fact]
    public void Accessibility_Table_Has_A_Column_Per_FiveNumberSummary_Statistic()
    {
        var cut = _ctx.Render<L.NativeBoxPlotChart>(p => p
            .Add(b => b.Categories, new List<string> { "A" })
            .Add(b => b.Samples, new List<List<double>> { new() { 1, 2, 3 } }));

        var headers = cut.FindAll("table.sr-only thead th").Select(h => h.TextContent).ToList();
        Assert.Equal(new[] { "Category", "Min", "Q1", "Median", "Q3", "Max" }, headers);
    }

    /// <summary>
    /// Axis-scaling consistency judgment call: box plots are a distribution/range
    /// type (like Candlestick), not a zero-anchored magnitude type (like
    /// Line/Bar/Area) — the Y-axis should tight-fit the data, NOT force zero into
    /// the domain. Before this fix, <c>NiceTicks.Compute(Math.Min(0, lo), hi, 5)</c>
    /// always included zero, so a distribution sitting well above zero (e.g. deal
    /// sizes in the 18k-51k range) rendered boxes squashed into a thin band at the
    /// top of the axis — the exact failure mode Candlestick's own tight fit exists
    /// to avoid. This asserts the actual rendered tick VALUES (not just their
    /// count/presence): with data entirely in [18200, 51200], "0" must not appear
    /// as a tick, and the lowest tick must be well above zero.
    /// </summary>
    [Fact]
    public void YAxis_Tight_Fits_The_Data_Range_Instead_Of_Forcing_A_Zero_Baseline()
    {
        var cut = _ctx.Render<L.NativeBoxPlotChart>(p => p
            .Add(b => b.Categories, new List<string> { "Q1" })
            .Add(b => b.Data, new List<double[]> { new[] { 18200.0, 22400, 25800, 29100, 34600 } }));

        var yTicks = cut.FindAll("svg g.lumeo-chart-axis text").Select(t => t.TextContent).ToList();
        Assert.DoesNotContain(yTicks, t => t.Trim() == "0");

        var numericTicks = yTicks
            .Select(t => double.TryParse(t.Replace(",", ""), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v) ? (double?)v : null)
            .Where(v => v is not null)
            .Select(v => v!.Value)
            .ToList();
        Assert.NotEmpty(numericTicks);
        Assert.True(numericTicks.Min() > 10000, $"Expected the lowest Y tick to tight-fit well above zero (>10000); got {numericTicks.Min()}.");
    }
}
