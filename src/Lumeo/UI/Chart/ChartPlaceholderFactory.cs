using System.Text.Json;

namespace Lumeo;

/// <summary>
/// Builds "phantom" ECharts options — a real chart rendered with synthesized placeholder data
/// in a muted palette. When the consumer flips <c>IsLoading</c> off, ECharts animates the option
/// swap from phantom → real, giving a continuous morph instead of a skeleton → chart snap.
///
/// The placeholder palette is intentionally flat grayscale so the handoff reads as
/// "data just arrived" rather than "new chart type appeared". All geometry (axes, legend slots,
/// grid padding) matches the real chart's layout so the swap doesn't shift pixels.
/// </summary>
internal static class ChartPlaceholderFactory
{
    // Muted palette — flat slate with decreasing opacity per series. Picked to look
    // intentionally "loading" on light AND dark themes without theme-token gymnastics.
    private static readonly List<string> MutedPalette = new()
    {
        "rgba(148, 163, 184, 0.55)",
        "rgba(148, 163, 184, 0.40)",
        "rgba(148, 163, 184, 0.28)",
        "rgba(148, 163, 184, 0.20)",
        "rgba(148, 163, 184, 0.14)",
    };

    private static readonly List<string> BarsCategories =
        new() { "A", "B", "C", "D", "E", "F", "G", "H" };

    private static readonly List<string> LineCategories =
        new() { "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12" };

    private static readonly List<string> PieSliceNames =
        new() { "·", "·", "·", "·" };

    public static EChartOption Create(ChartSkeletonKind kind) => kind switch
    {
        ChartSkeletonKind.Bars => BuildBars(),
        ChartSkeletonKind.Line => BuildLine(false),
        ChartSkeletonKind.Area => BuildLine(true),
        ChartSkeletonKind.Pie => BuildPie(),
        ChartSkeletonKind.Scatter => BuildScatter(),
        ChartSkeletonKind.Grid => BuildHeatmap(),
        _ => BuildBars()
    };

    private static EChartOption BuildBars()
    {
        // 8 staggered heights — same silhouette as the SVG skeleton so the morph from
        // phantom to real feels like the bars just "settled" onto actual values.
        var values = new List<double> { 38, 62, 28, 75, 45, 88, 52, 68 };

        return new EChartOption
        {
            Color = MutedPalette,
            Grid = new EChartGrid { Left = "3%", Right = "4%", Bottom = "8%", Top = "14%", ContainLabel = true },
            Legend = BuildLegendHint(new() { "·", "·", "·" }),
            XAxis = new() { new EChartAxis { Type = "category", Data = BarsCategories, AxisLabel = new() { Color = "transparent" } } },
            YAxis = new() { new EChartAxis { Type = "value", AxisLabel = new() { Color = "transparent" } } },
            Series = new()
            {
                new EChartSeries
                {
                    Name = "·",
                    Type = "bar",
                    Data = values,
                    BarWidth = 18,
                    ItemStyle = new() { BorderRadius = 4 }
                }
            }
        };
    }

    private static EChartOption BuildLine(bool filled)
    {
        // 3 gently curved series — matches the "multi-line skeleton" silhouette.
        var s1 = new List<double> { 30, 38, 34, 48, 55, 62, 58, 72, 78, 85, 80, 92 };
        var s2 = new List<double> { 22, 28, 32, 38, 42, 48, 52, 58, 62, 68, 72, 78 };
        var s3 = new List<double> { 15, 18, 22, 28, 32, 36, 40, 44, 48, 52, 56, 60 };

        EChartSeries Line(string name, List<double> data) => new()
        {
            Name = name,
            Type = "line",
            Data = data,
            Smooth = true,
            ShowSymbol = false,
            LineStyle = new() { Width = 2 },
            AreaStyle = filled ? new EChartAreaStyle { Opacity = 0.18 } : null
        };

        return new EChartOption
        {
            Color = MutedPalette,
            Grid = new EChartGrid { Left = "3%", Right = "4%", Bottom = "8%", Top = "14%", ContainLabel = true },
            Legend = BuildLegendHint(new() { "·", "·", "·" }),
            XAxis = new() { new EChartAxis { Type = "category", Data = LineCategories, BoundaryGap = false, AxisLabel = new() { Color = "transparent" } } },
            YAxis = new() { new EChartAxis { Type = "value", AxisLabel = new() { Color = "transparent" } } },
            Series = new() { Line("·1", s1), Line("·2", s2), Line("·3", s3) }
        };
    }

    private static EChartOption BuildPie()
    {
        var data = new List<EChartPieData>
        {
            new() { Name = PieSliceNames[0], Value = 38 },
            new() { Name = PieSliceNames[1], Value = 28 },
            new() { Name = PieSliceNames[2], Value = 20 },
            new() { Name = PieSliceNames[3], Value = 14 },
        };

        return new EChartOption
        {
            Color = MutedPalette,
            Legend = BuildLegendHint(PieSliceNames),
            Series = new()
            {
                new EChartSeries
                {
                    Name = "·",
                    Type = "pie",
                    Radius = "58%",
                    Center = "50%,58%",
                    Data = data,
                    Label = new() { Show = false },
                    ItemStyle = new() { BorderColor = "rgba(0,0,0,0)", BorderWidth = 2 }
                }
            }
        };
    }

    private static EChartOption BuildScatter()
    {
        // 20 seeded positions — stable across renders so the morph to real data is smooth.
        var pts = new List<double[]>
        {
            new[] { 12.0, 44 }, new[] { 18.0, 38 }, new[] { 22.0, 52 }, new[] { 28.0, 48 },
            new[] { 33.0, 60 }, new[] { 38.0, 55 }, new[] { 44.0, 68 }, new[] { 50.0, 62 },
            new[] { 55.0, 74 }, new[] { 60.0, 70 }, new[] { 66.0, 82 }, new[] { 72.0, 78 },
            new[] { 77.0, 85 }, new[] { 82.0, 80 }, new[] { 88.0, 92 }, new[] { 15.0, 62 },
            new[] { 30.0, 40 }, new[] { 48.0, 50 }, new[] { 65.0, 58 }, new[] { 80.0, 68 },
        };

        return new EChartOption
        {
            Color = MutedPalette,
            Grid = new EChartGrid { Left = "3%", Right = "4%", Bottom = "8%", Top = "14%", ContainLabel = true },
            Legend = BuildLegendHint(new() { "·" }),
            XAxis = new() { new EChartAxis { Type = "value", AxisLabel = new() { Color = "transparent" } } },
            YAxis = new() { new EChartAxis { Type = "value", AxisLabel = new() { Color = "transparent" } } },
            Series = new()
            {
                new EChartSeries { Name = "·", Type = "scatter", Data = pts, SymbolSize = 12 }
            }
        };
    }

    private static EChartOption BuildHeatmap()
    {
        // 8×5 grid of values — heatmap placeholder works for CalendarHeatmap + Heatmap kinds.
        var points = new List<int[]>();
        int seed = 0;
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 5; y++)
            {
                seed = (seed * 1103515245 + 12345) & 0x7fffffff;
                points.Add(new[] { x, y, 20 + seed % 60 });
            }
        }

        return new EChartOption
        {
            Color = MutedPalette,
            Grid = new EChartGrid { Left = "3%", Right = "4%", Bottom = "8%", Top = "14%", ContainLabel = true },
            XAxis = new() { new EChartAxis { Type = "category", Data = new() { "1", "2", "3", "4", "5", "6", "7", "8" }, AxisLabel = new() { Color = "transparent" } } },
            YAxis = new() { new EChartAxis { Type = "category", Data = new() { "a", "b", "c", "d", "e" }, AxisLabel = new() { Color = "transparent" } } },
            VisualMap = new EChartVisualMap
            {
                Min = 0,
                Max = 100,
                Show = false,
                InRange = new EChartVisualMapInRange { Color = new() { "rgba(148,163,184,0.15)", "rgba(148,163,184,0.55)" } }
            },
            Series = new()
            {
                new EChartSeries { Name = "·", Type = "heatmap", Data = points, Label = new() { Show = false } }
            }
        };
    }

    private static EChartLegend BuildLegendHint(List<string> data) => new()
    {
        Data = data,
        Top = "3%",
        TextStyle = new() { Color = "transparent" }
    };

    /// <summary>Serializes a placeholder option to JSON — callers pass this directly to
    /// ECharts via <c>updateChart</c> so the option-swap animation fires.</summary>
    public static string CreateJson(ChartSkeletonKind kind) => Create(kind).ToJson();
}
