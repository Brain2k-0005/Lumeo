namespace Lumeo;

/// <summary>
/// Builds "phantom" ECharts options — a real chart rendered with synthesized placeholder data
/// in a muted palette. Each <c>Create(kind, tick)</c> call produces a fresh random variation
/// seeded on <c>tick</c>, so Chart.razor can cycle through phantom frames every ~1.5s while
/// loading — the effect reads as "incoming data" rather than a static placeholder.
/// When <c>IsLoading</c> flips off, Chart.razor swaps to the real option with
/// <c>notMerge=true</c> so the muted palette doesn't bleed into real colors.
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

    public static EChartOption Create(ChartSkeletonKind kind, int tick = 0)
    {
        var rng = new Random(unchecked(kind.GetHashCode() * 397 ^ tick));
        return kind switch
        {
            ChartSkeletonKind.Bars => BuildBars(rng),
            ChartSkeletonKind.Line => BuildLine(rng, filled: false),
            ChartSkeletonKind.Area => BuildLine(rng, filled: true),
            ChartSkeletonKind.Pie => BuildPie(rng),
            ChartSkeletonKind.Scatter => BuildScatter(rng),
            ChartSkeletonKind.Grid => BuildHeatmap(rng),
            _ => BuildBars(rng)
        };
    }

    private static EChartOption BuildBars(Random rng)
    {
        var values = new List<double>(8);
        for (int i = 0; i < 8; i++) values.Add(20 + rng.Next(80));

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

    private static EChartOption BuildLine(Random rng, bool filled)
    {
        // Each series is a gentle random-walk so consecutive phantom frames morph smoothly.
        List<double> Walk(double start, double drift, double noise)
        {
            var result = new List<double>(12);
            double value = start;
            for (int i = 0; i < 12; i++)
            {
                value += drift + (rng.NextDouble() - 0.5) * noise;
                result.Add(Math.Round(Math.Clamp(value, 5, 95), 1));
            }
            return result;
        }

        var s1 = Walk(30, 5, 12);
        var s2 = Walk(22, 4, 10);
        var s3 = Walk(15, 3, 8);

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

    private static EChartOption BuildPie(Random rng)
    {
        // Fresh random weights each tick — slices visibly redistribute between frames.
        var weights = new double[4];
        double total = 0;
        for (int i = 0; i < 4; i++) { weights[i] = 10 + rng.NextDouble() * 40; total += weights[i]; }

        var data = new List<EChartPieData>();
        for (int i = 0; i < 4; i++)
        {
            data.Add(new EChartPieData { Name = PieSliceNames[i], Value = Math.Round(weights[i] / total * 100, 1) });
        }

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

    private static EChartOption BuildScatter(Random rng)
    {
        var pts = new List<double[]>(20);
        for (int i = 0; i < 20; i++)
        {
            pts.Add(new[] { Math.Round(5 + rng.NextDouble() * 90, 1), Math.Round(10 + rng.NextDouble() * 85, 1) });
        }

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

    private static EChartOption BuildHeatmap(Random rng)
    {
        var points = new List<int[]>();
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 5; y++)
            {
                points.Add(new[] { x, y, 15 + rng.Next(75) });
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

    public static string CreateJson(ChartSkeletonKind kind, int tick = 0) => Create(kind, tick).ToJson();
}
