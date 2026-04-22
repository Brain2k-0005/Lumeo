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
            Color = MutedPalette,
            Grid = new EChartGrid { Left = "3%", Right = "4%", Bottom = "8%", Top = "14%", ContainLabel = true },
            Legend = BuildLegendHint(seriesCount),
            XAxis = new() { new EChartAxis { Type = "category", Data = CategoryLabels(categories), AxisLabel = new() { Color = "transparent" } } },
            YAxis = new() { new EChartAxis { Type = "value", AxisLabel = new() { Color = "transparent" } } },
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
            Color = MutedPalette,
            Grid = new EChartGrid { Left = "3%", Right = "4%", Bottom = "8%", Top = "14%", ContainLabel = true },
            Legend = BuildLegendHint(seriesCount),
            XAxis = new() { new EChartAxis { Type = "category", Data = CategoryLabels(categories), BoundaryGap = false, AxisLabel = new() { Color = "transparent" } } },
            YAxis = new() { new EChartAxis { Type = "value", AxisLabel = new() { Color = "transparent" } } },
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
            Color = MutedPalette,
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
            Color = MutedPalette,
            Grid = new EChartGrid { Left = "3%", Right = "4%", Bottom = "8%", Top = "14%", ContainLabel = true },
            Legend = BuildLegendHint(1),
            XAxis = new() { new EChartAxis { Type = "value", AxisLabel = new() { Color = "transparent" } } },
            YAxis = new() { new EChartAxis { Type = "value", AxisLabel = new() { Color = "transparent" } } },
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
            Color = MutedPalette,
            Grid = new EChartGrid { Left = "3%", Right = "4%", Bottom = "8%", Top = "14%", ContainLabel = true },
            XAxis = new() { new EChartAxis { Type = "category", Data = CategoryLabels(cols), AxisLabel = new() { Color = "transparent" } } },
            YAxis = new() { new EChartAxis { Type = "category", Data = CategoryLabels(rows), AxisLabel = new() { Color = "transparent" } } },
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

    private static EChartLegend BuildLegendHint(int entries)
    {
        var data = new List<string>(entries);
        for (int i = 0; i < entries; i++) data.Add(" ");
        return new EChartLegend
        {
            Data = data,
            Top = "3%",
            TextStyle = new() { Color = "transparent" }
        };
    }
}
