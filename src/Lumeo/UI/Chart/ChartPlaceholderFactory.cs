namespace Lumeo;

/// <summary>Describes the layout the phantom should mimic — the real chart's category count,
/// number of series, stacking, etc. When absent, defaults per chart kind are used.</summary>
public sealed record PhantomShape
{
    public int Categories { get; init; } = 8;
    public int SeriesCount { get; init; } = 1;
    public bool Stacked { get; init; }
    public bool FilledArea { get; init; }
    /// <summary>For pie/donut/funnel: slice count overrides <see cref="Categories"/>.</summary>
    public int? SliceCount { get; init; }
}

/// <summary>
/// Builds "phantom" ECharts options — a real chart rendered with synthesized placeholder data
/// in a muted palette. Each <c>Create(kind, tick, shape)</c> produces a fresh random variation
/// seeded on <c>tick</c> so Chart.razor can cycle through frames every ~1.4s while loading.
/// When a <see cref="PhantomShape"/> is supplied, the placeholder mirrors the real chart's
/// structure (same category count, same number of series, matching stack mode) — so the
/// phantom looks like the real thing with randomized values.
/// </summary>
internal static class ChartPlaceholderFactory
{
    private static readonly List<string> MutedPalette = new()
    {
        "rgba(148, 163, 184, 0.55)",
        "rgba(148, 163, 184, 0.40)",
        "rgba(148, 163, 184, 0.28)",
        "rgba(148, 163, 184, 0.20)",
        "rgba(148, 163, 184, 0.14)",
    };

    /// <summary>Derive a <see cref="PhantomShape"/> from the real chart option so the phantom
    /// mirrors the actual layout (category count, series count, stacking, area fill).</summary>
    public static PhantomShape? InferShape(EChartOption? option)
    {
        if (option is null) return null;

        int categories = option.XAxis?.FirstOrDefault()?.Data?.Count
                         ?? option.YAxis?.FirstOrDefault()?.Data?.Count
                         ?? 0;
        int seriesCount = option.Series?.Count ?? 0;
        bool stacked = option.Series?.Any(s => !string.IsNullOrEmpty(s.Stack)) ?? false;
        bool filled = option.Series?.Any(s => s.AreaStyle is not null) ?? false;

        int? sliceCount = null;
        var firstSeries = option.Series?.FirstOrDefault();
        if (firstSeries?.Type is "pie" or "funnel" or "rosetype")
        {
            if (firstSeries.Data is System.Collections.ICollection coll) sliceCount = coll.Count;
        }

        if (categories == 0 && seriesCount == 0 && sliceCount is null) return null;

        return new PhantomShape
        {
            Categories = categories > 0 ? categories : 8,
            SeriesCount = Math.Max(1, seriesCount),
            Stacked = stacked,
            FilledArea = filled,
            SliceCount = sliceCount
        };
    }

    public static EChartOption Create(ChartSkeletonKind kind, int tick = 0, PhantomShape? shape = null)
    {
        shape ??= new PhantomShape();
        var rng = new Random(unchecked(kind.GetHashCode() * 397 ^ tick));

        return kind switch
        {
            ChartSkeletonKind.Bars => BuildBars(rng, shape),
            ChartSkeletonKind.Line => BuildLine(rng, shape, filled: shape.FilledArea),
            ChartSkeletonKind.Area => BuildLine(rng, shape, filled: true),
            ChartSkeletonKind.Pie => BuildPie(rng, shape),
            ChartSkeletonKind.Scatter => BuildScatter(rng, shape),
            ChartSkeletonKind.Grid => BuildHeatmap(rng, shape),
            ChartSkeletonKind.Radar => BuildRadar(rng, shape),
            ChartSkeletonKind.Sankey => BuildSankey(rng),
            ChartSkeletonKind.Graph => BuildGraph(rng),
            ChartSkeletonKind.Tree => BuildTree(rng),
            ChartSkeletonKind.Parallel => BuildParallel(rng, shape),
            ChartSkeletonKind.Funnel => BuildFunnel(rng, shape),
            ChartSkeletonKind.Gauge => BuildGauge(rng),
            ChartSkeletonKind.Polar => BuildPolar(rng, shape),
            ChartSkeletonKind.Candlestick => BuildCandlestick(rng, shape),
            _ => BuildBars(rng, shape)
        };
    }

    public static string CreateJson(ChartSkeletonKind kind, int tick = 0, PhantomShape? shape = null) =>
        Create(kind, tick, shape).ToJson();

    // ---- builders ----

    private static List<string> CategoryLabels(int count)
    {
        var list = new List<string>(count);
        for (int i = 0; i < count; i++) list.Add(" ");
        return list;
    }

    private static EChartOption BuildBars(Random rng, PhantomShape shape)
    {
        int categories = Math.Max(2, shape.Categories);
        int seriesCount = Math.Max(1, shape.SeriesCount);

        var series = new List<EChartSeries>(seriesCount);
        for (int s = 0; s < seriesCount; s++)
        {
            var values = new List<double>(categories);
            for (int i = 0; i < categories; i++) values.Add(20 + rng.Next(80));
            series.Add(new EChartSeries
            {
                Name = "·",
                Type = "bar",
                Data = values,
                Stack = shape.Stacked ? "_stack_" : null,
                BarMaxWidth = 28,
                ItemStyle = new() { BorderRadius = 4 }
            });
        }

        return new EChartOption
        {
            Grid = new EChartGrid { Left = "3%", Right = "4%", Bottom = "8%", Top = "14%", ContainLabel = true },
            Legend = BuildLegendHint(seriesCount),
            XAxis = new() { new EChartAxis { Type = "category", Data = CategoryLabels(categories), AxisLabel = SkeletonBoxAxisLabel() } },
            YAxis = new() { new EChartAxis { Type = "value", AxisLabel = SkeletonBoxAxisLabel() } },
            Series = series
        };
    }

    private static EChartOption BuildLine(Random rng, PhantomShape shape, bool filled)
    {
        int categories = Math.Max(3, shape.Categories);
        int seriesCount = Math.Max(1, shape.SeriesCount);

        List<double> Walk(double start, double drift, double noise)
        {
            var result = new List<double>(categories);
            double value = start;
            for (int i = 0; i < categories; i++)
            {
                value += drift + (rng.NextDouble() - 0.5) * noise;
                result.Add(Math.Round(Math.Clamp(value, 5, 95), 1));
            }
            return result;
        }

        var series = new List<EChartSeries>(seriesCount);
        for (int s = 0; s < seriesCount; s++)
        {
            double start = 15 + s * 10;
            series.Add(new EChartSeries
            {
                Name = "·",
                Type = "line",
                Data = Walk(start, 3 + s, 10 + s * 2),
                Smooth = true,
                ShowSymbol = false,
                Stack = shape.Stacked ? "_stack_" : null,
                LineStyle = new() { Width = 2 },
                AreaStyle = filled ? new EChartAreaStyle { Opacity = 0.18 } : null
            });
        }

        return new EChartOption
        {
            Grid = new EChartGrid { Left = "3%", Right = "4%", Bottom = "8%", Top = "14%", ContainLabel = true },
            Legend = BuildLegendHint(seriesCount),
            XAxis = new() { new EChartAxis { Type = "category", Data = CategoryLabels(categories), BoundaryGap = false, AxisLabel = SkeletonBoxAxisLabel() } },
            YAxis = new() { new EChartAxis { Type = "value", AxisLabel = SkeletonBoxAxisLabel() } },
            Series = series
        };
    }

    private static EChartOption BuildPie(Random rng, PhantomShape shape)
    {
        int slices = shape.SliceCount ?? Math.Max(3, Math.Min(8, shape.Categories));
        var weights = new double[slices];
        double total = 0;
        for (int i = 0; i < slices; i++) { weights[i] = 10 + rng.NextDouble() * 40; total += weights[i]; }

        var data = new List<EChartPieData>(slices);
        for (int i = 0; i < slices; i++)
        {
            data.Add(new EChartPieData { Name = " ", Value = Math.Round(weights[i] / total * 100, 1) });
        }

        return new EChartOption
        {
            Legend = BuildLegendHint(slices),
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

    private static EChartOption BuildScatter(Random rng, PhantomShape shape)
    {
        int points = Math.Max(8, Math.Min(60, shape.Categories > 8 ? shape.Categories * 2 : 20));
        var pts = new List<double[]>(points);
        for (int i = 0; i < points; i++)
        {
            pts.Add(new[] { Math.Round(5 + rng.NextDouble() * 90, 1), Math.Round(10 + rng.NextDouble() * 85, 1) });
        }

        return new EChartOption
        {
            Grid = new EChartGrid { Left = "3%", Right = "4%", Bottom = "8%", Top = "14%", ContainLabel = true },
            Legend = BuildLegendHint(1),
            XAxis = new() { new EChartAxis { Type = "value", AxisLabel = SkeletonBoxAxisLabel() } },
            YAxis = new() { new EChartAxis { Type = "value", AxisLabel = SkeletonBoxAxisLabel() } },
            Series = new()
            {
                new EChartSeries { Name = "·", Type = "scatter", Data = pts, SymbolSize = 12 }
            }
        };
    }

    private static EChartOption BuildHeatmap(Random rng, PhantomShape shape)
    {
        int cols = Math.Max(3, Math.Min(24, shape.Categories));
        int rows = Math.Max(3, Math.Min(12, shape.SeriesCount > 1 ? shape.SeriesCount : 5));

        var points = new List<int[]>(cols * rows);
        for (int x = 0; x < cols; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                points.Add(new[] { x, y, 15 + rng.Next(75) });
            }
        }

        return new EChartOption
        {
            Grid = new EChartGrid { Left = "3%", Right = "4%", Bottom = "8%", Top = "14%", ContainLabel = true },
            XAxis = new() { new EChartAxis { Type = "category", Data = CategoryLabels(cols), AxisLabel = SkeletonBoxAxisLabel() } },
            YAxis = new() { new EChartAxis { Type = "category", Data = CategoryLabels(rows), AxisLabel = SkeletonBoxAxisLabel() } },
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

    // Muted box color for axis/legend skeleton rectangles — a shade lighter than series bars
    // so the "text boxes" don't compete with the data but still read as placeholders.
    private const string SkeletonBoxColor = "rgba(148, 163, 184, 0.35)";

    /// <summary>Axis label styled as a skeleton box — label text is transparent but the
    /// label's backgroundColor / padding / borderRadius draw a muted rectangle. Width
    /// tuned for typical tick labels; consumers can override if needed.</summary>
    private static EChartAxisLabel SkeletonBoxAxisLabel(int width = 22, int height = 8) => new()
    {
        Color = "transparent",
        BackgroundColor = SkeletonBoxColor,
        Padding = new[] { 0, 0, 0, 0 },
        BorderRadius = 2,
        Width = width,
        Height = height
    };

    private static EChartLegend BuildLegendHint(int entries)
    {
        var data = new List<string>(entries);
        for (int i = 0; i < entries; i++) data.Add(" ");
        return new EChartLegend
        {
            Data = data,
            Top = "3%",
            // Transparent text + muted pill per legend item reads as "loading legend entries".
            TextStyle = new()
            {
                Color = "transparent",
                BackgroundColor = SkeletonBoxColor,
                BorderRadius = 3,
                Width = 28,
                Height = 8
            }
        };
    }

    // ---- dedicated phantom builders for complex chart types ----

    private static EChartOption BuildRadar(Random rng, PhantomShape shape)
    {
        int dims = 5;
        var indicators = new List<EChartRadarIndicator>(dims);
        for (int i = 0; i < dims; i++) indicators.Add(new EChartRadarIndicator { Name = " ", Max = 100 });

        int seriesCount = Math.Max(1, Math.Min(3, shape.SeriesCount));
        var radarData = new List<EChartRadarData>(seriesCount);
        for (int s = 0; s < seriesCount; s++)
        {
            var values = new List<double>(dims);
            for (int i = 0; i < dims; i++) values.Add(Math.Round(30 + rng.NextDouble() * 60, 1));
            radarData.Add(new EChartRadarData { Name = " ", Value = values, AreaStyle = new() { Opacity = 0.18 } });
        }

        return new EChartOption
        {
            Legend = BuildLegendHint(seriesCount),
            Radar = new EChartRadar { Indicator = indicators, Shape = "polygon" },
            Series = new()
            {
                new EChartSeries
                {
                    Name = "·",
                    Type = "radar",
                    Data = radarData
                }
            }
        };
    }

    private static EChartOption BuildSankey(Random rng)
    {
        var nodes = new List<EChartSankeyNode>();
        for (int i = 0; i < 7; i++) nodes.Add(new EChartSankeyNode { Name = $"n{i}" });

        var links = new List<EChartSankeyLink>
        {
            new() { Source = "n0", Target = "n3", Value = Math.Round(20 + rng.NextDouble() * 30, 1) },
            new() { Source = "n0", Target = "n4", Value = Math.Round(15 + rng.NextDouble() * 25, 1) },
            new() { Source = "n1", Target = "n3", Value = Math.Round(10 + rng.NextDouble() * 20, 1) },
            new() { Source = "n1", Target = "n5", Value = Math.Round(15 + rng.NextDouble() * 25, 1) },
            new() { Source = "n2", Target = "n4", Value = Math.Round(20 + rng.NextDouble() * 30, 1) },
            new() { Source = "n3", Target = "n6", Value = Math.Round(18 + rng.NextDouble() * 22, 1) },
            new() { Source = "n4", Target = "n6", Value = Math.Round(15 + rng.NextDouble() * 25, 1) },
            new() { Source = "n5", Target = "n6", Value = Math.Round(12 + rng.NextDouble() * 18, 1) },
        };

        return new EChartOption
        {
            Series = new()
            {
                new EChartSeries
                {
                    Name = "·",
                    Type = "sankey",
                    Left = "5%",
                    Right = "10%",
                    Top = "5%",
                    Bottom = "5%",
                    Nodes = nodes,
                    Links = links,
                    Label = new() { Show = false },
                    ItemStyle = new() { BorderColor = "rgba(0,0,0,0)" }
                }
            }
        };
    }

    private static EChartOption BuildGraph(Random rng)
    {
        int nodeCount = 10;
        var nodes = new List<EChartGraphNode>();
        for (int i = 0; i < nodeCount; i++)
        {
            nodes.Add(new EChartGraphNode
            {
                Name = $"g{i}",
                X = Math.Round(rng.NextDouble() * 100, 1),
                Y = Math.Round(rng.NextDouble() * 100, 1),
                SymbolSize = 10 + rng.Next(18)
            });
        }

        var links = new List<EChartGraphLink>();
        for (int i = 0; i < nodeCount - 1; i++)
        {
            links.Add(new EChartGraphLink { Source = $"g{i}", Target = $"g{i + 1}" });
        }
        for (int extra = 0; extra < 4; extra++)
        {
            int a = rng.Next(nodeCount), b = rng.Next(nodeCount);
            if (a != b) links.Add(new EChartGraphLink { Source = $"g{a}", Target = $"g{b}" });
        }

        return new EChartOption
        {
            Series = new()
            {
                new EChartSeries
                {
                    Name = "·",
                    Type = "graph",
                    Layout = "none",
                    Nodes = nodes,
                    Links = links,
                    Roam = false,
                    Label = new() { Show = false }
                }
            }
        };
    }

    private static EChartOption BuildTree(Random rng)
    {
        // 3-level tree with random branching factor.
        var root = new EChartTreeData
        {
            Name = " ",
            Children = new()
            {
                new() { Name = " ", Children = new()
                {
                    new() { Name = " " },
                    new() { Name = " " },
                    new() { Name = " " }
                }},
                new() { Name = " ", Children = new()
                {
                    new() { Name = " " },
                    new() { Name = " " }
                }},
                new() { Name = " ", Children = new()
                {
                    new() { Name = " " },
                    new() { Name = " " },
                    new() { Name = " " }
                }}
            }
        };

        return new EChartOption
        {
            Series = new()
            {
                new EChartSeries
                {
                    Name = "·",
                    Type = "tree",
                    Data = new List<EChartTreeData> { root },
                    Orient = "LR",
                    Left = "8%",
                    Right = "18%",
                    Top = "5%",
                    Bottom = "5%",
                    Label = new() { Show = false }
                }
            }
        };
    }

    private static EChartOption BuildParallel(Random rng, PhantomShape shape)
    {
        int axes = Math.Max(3, Math.Min(8, shape.Categories > 3 ? shape.Categories : 5));
        var parallelAxis = new List<EChartParallelAxis>(axes);
        for (int i = 0; i < axes; i++) parallelAxis.Add(new EChartParallelAxis { Dim = i, Name = " ", Min = 0, Max = 100 });

        int lines = 8;
        var data = new List<double[]>(lines);
        for (int i = 0; i < lines; i++)
        {
            var row = new double[axes];
            for (int j = 0; j < axes; j++) row[j] = Math.Round(10 + rng.NextDouble() * 80, 1);
            data.Add(row);
        }

        return new EChartOption
        {
            Parallel = new EChartParallel { Left = "6%", Right = "14%", Top = "10%", Bottom = "10%" },
            ParallelAxis = parallelAxis,
            Series = new()
            {
                new EChartSeries
                {
                    Name = "·",
                    Type = "parallel",
                    Data = data
                }
            }
        };
    }

    private static EChartOption BuildFunnel(Random rng, PhantomShape shape)
    {
        int steps = Math.Max(3, Math.Min(6, shape.SliceCount ?? 5));
        var data = new List<EChartPieData>(steps);
        double value = 100;
        for (int i = 0; i < steps; i++)
        {
            value = Math.Max(8, value - (10 + rng.NextDouble() * 18));
            data.Add(new EChartPieData { Name = " ", Value = Math.Round(value, 1) });
        }

        return new EChartOption
        {
            Series = new()
            {
                new EChartSeries
                {
                    Name = "·",
                    Type = "funnel",
                    Left = "10%",
                    Right = "10%",
                    Top = "8%",
                    Bottom = "8%",
                    Data = data,
                    Label = new() { Show = false },
                    ItemStyle = new() { BorderColor = "rgba(0,0,0,0)", BorderWidth = 2 }
                }
            }
        };
    }

    private static EChartOption BuildGauge(Random rng)
    {
        double value = Math.Round(10 + rng.NextDouble() * 80, 0);

        return new EChartOption
        {
            Series = new()
            {
                new EChartSeries
                {
                    Name = "·",
                    Type = "gauge",
                    StartAngle = 210,
                    EndAngle = -30,
                    Min = 0,
                    Max = 100,
                    Progress = new EChartSeriesProgress { Show = true, Width = 10 },
                    Pointer = new EChartPointer { Show = true },
                    Detail = new EChartSeriesDetail { FontSize = 0, Formatter = " " },
                    Data = new List<EChartPieData> { new() { Name = " ", Value = value } }
                }
            }
        };
    }

    private static EChartOption BuildPolar(Random rng, PhantomShape shape)
    {
        int bars = Math.Max(6, Math.Min(12, shape.Categories > 0 ? shape.Categories : 8));
        var values = new List<double>(bars);
        for (int i = 0; i < bars; i++) values.Add(20 + rng.Next(70));

        return new EChartOption
        {
            Polar = new { radius = new[] { "20%", "75%" } },
            AngleAxis = new() { new { type = "category", data = CategoryLabels(bars), axisLabel = new { color = "transparent", backgroundColor = SkeletonBoxColor, width = 18, height = 8, borderRadius = 2 } } },
            RadiusAxis = new() { new { axisLabel = new { color = "transparent", backgroundColor = SkeletonBoxColor, width = 18, height = 8, borderRadius = 2 } } },
            Series = new()
            {
                new EChartSeries
                {
                    Name = "·",
                    Type = "bar",
                    CoordinateSystem = "polar",
                    Data = values,
                    ItemStyle = new() { BorderRadius = 4 }
                }
            }
        };
    }

    private static EChartOption BuildCandlestick(Random rng, PhantomShape shape)
    {
        int candles = Math.Max(8, Math.Min(40, shape.Categories > 0 ? shape.Categories : 16));
        var data = new List<double[]>(candles);
        double prev = 50;
        for (int i = 0; i < candles; i++)
        {
            double open = prev;
            double close = Math.Round(Math.Clamp(prev + (rng.NextDouble() - 0.5) * 18, 10, 90), 1);
            double low = Math.Round(Math.Min(open, close) - rng.NextDouble() * 6, 1);
            double high = Math.Round(Math.Max(open, close) + rng.NextDouble() * 6, 1);
            data.Add(new[] { open, close, low, high });
            prev = close;
        }

        return new EChartOption
        {
            Grid = new EChartGrid { Left = "3%", Right = "4%", Bottom = "8%", Top = "10%", ContainLabel = true },
            XAxis = new() { new EChartAxis { Type = "category", Data = CategoryLabels(candles), AxisLabel = SkeletonBoxAxisLabel() } },
            YAxis = new() { new EChartAxis { Type = "value", AxisLabel = SkeletonBoxAxisLabel() } },
            Series = new()
            {
                new EChartSeries
                {
                    Name = "·",
                    Type = "candlestick",
                    Data = data
                }
            }
        };
    }
}
