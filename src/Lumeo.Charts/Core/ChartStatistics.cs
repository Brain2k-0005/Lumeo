using System.Linq;

namespace Lumeo;

/// <summary>
/// Five-number-summary statistics used by distribution charts (BoxPlot). The
/// original spec assumed a wrapper always hands the engine a pre-aggregated
/// <c>double[5]</c> (min/Q1/median/Q3/max) — see spec §4 row 9 ("Data is
/// already raw double[5] per category — pure pass-through"). The owner's
/// second reference demo (chartsextended.html's BoxPlot card) instead computes
/// these FROM raw per-category samples (linear-interpolation percentiles +
/// 1.5×IQR whisker + outlier extraction), which is richer than the spec
/// assumed. This is a deliberate, small ADDITION beyond the original spec
/// text — flagged in the delivery report rather than absorbed silently — kept
/// here because it is a tiny, pure, coordinate-system/JS-independent
/// statistics function with no rendering opinion, squarely in the "core is
/// almost entirely pure functions" spirit, and removes an obvious place for a
/// future BoxPlotChart wrapper to reimplement percentile math inconsistently.
/// </summary>
internal readonly record struct ChartBoxPlotStats(
    double Min,
    double Q1,
    double Median,
    double Q3,
    double Max,
    double WhiskerLow,
    double WhiskerHigh,
    IReadOnlyList<double> Outliers);

internal static class ChartStatistics
{
    /// <summary>
    /// Computes Q1/median/Q3 via linear-interpolation percentiles (the same
    /// method as the reference demo and NumPy's default), a 1.5×IQR whisker
    /// range, and the list of samples falling outside the whiskers.
    /// <paramref name="samples"/> need not be pre-sorted.
    /// </summary>
    public static ChartBoxPlotStats Quartiles(IReadOnlyList<double> samples)
    {
        if (samples.Count == 0)
            throw new ArgumentException("Quartiles requires at least one sample.", nameof(samples));

        var sorted = samples.OrderBy(v => v).ToList();

        double Percentile(double p)
        {
            if (sorted.Count == 1) return sorted[0];
            var idx = (sorted.Count - 1) * p;
            var lo = (int)Math.Floor(idx);
            var hi = Math.Min(lo + 1, sorted.Count - 1);
            var frac = idx - lo;
            return sorted[lo] + (sorted[hi] - sorted[lo]) * frac;
        }

        var q1 = Percentile(0.25);
        var median = Percentile(0.5);
        var q3 = Percentile(0.75);
        var iqr = q3 - q1;
        var lowerFence = q1 - 1.5 * iqr;
        var upperFence = q3 + 1.5 * iqr;

        var whiskerLow = sorted.Where(v => v >= lowerFence).DefaultIfEmpty(sorted[0]).Min();
        var whiskerHigh = sorted.Where(v => v <= upperFence).DefaultIfEmpty(sorted[^1]).Max();
        var outliers = sorted.Where(v => v < whiskerLow || v > whiskerHigh).ToList();

        return new ChartBoxPlotStats(sorted[0], q1, median, q3, sorted[^1], whiskerLow, whiskerHigh, outliers);
    }
}
