namespace Lumeo;

/// <summary>Strategy for rendering category-axis labels on cartesian charts.</summary>
public enum ChartLabelStrategy
{
    /// <summary>Show every label; auto-rotate as density grows so labels stay readable.</summary>
    Smart,
    /// <summary>Force every label with no automatic rotation (may overlap on dense datasets).</summary>
    ShowAll,
    /// <summary>Let ECharts auto-thin overlapping labels (original default — hides some labels on dense sets).</summary>
    Auto,
}

/// <summary>Shared helper used by every cartesian chart (Bar, Line, Area, Candlestick,
/// BoxPlot, Waterfall, PictorialBar, Mixed) so category-axis label rendering is
/// consistent across the library.</summary>
public static class ChartLabelHelper
{
    /// <summary>Computes the axisLabel interval + rotation for a category axis
    /// given the chosen strategy, category count, and manual rotate override.
    /// Returns (null, null) if no axisLabel customization is needed (strategy = Auto
    /// and no manual rotate).</summary>
    public static (int? interval, int? rotate) Resolve(
        ChartLabelStrategy strategy,
        int categoryCount,
        int? manualRotate)
    {
        int? interval = null;
        int? autoRotate = null;
        switch (strategy)
        {
            case ChartLabelStrategy.Smart:
                interval = 0;
                autoRotate = categoryCount switch
                {
                    <= 10 => null,
                    <= 16 => -30,
                    <= 24 => -60,
                    _     => -75,
                };
                break;
            case ChartLabelStrategy.ShowAll:
                interval = 0;
                break;
            case ChartLabelStrategy.Auto:
                break;
        }
        return (interval, manualRotate ?? autoRotate);
    }

    /// <summary>Applies the strategy to the given axis, creating <see cref="EChartAxisLabel"/>
    /// when needed. Leaves the axis untouched for <see cref="ChartLabelStrategy.Auto"/>
    /// with no manual rotate.</summary>
    public static void ApplyTo(EChartAxis axis, ChartLabelStrategy strategy, int categoryCount, int? manualRotate)
    {
        var (interval, rotate) = Resolve(strategy, categoryCount, manualRotate);
        if (interval.HasValue || rotate.HasValue)
        {
            axis.AxisLabel ??= new EChartAxisLabel();
            if (interval.HasValue) axis.AxisLabel.Interval = interval;
            if (rotate.HasValue) axis.AxisLabel.Rotate = rotate;
        }
    }
}
