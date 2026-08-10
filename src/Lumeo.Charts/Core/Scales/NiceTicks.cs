namespace Lumeo;

/// <summary>
/// Generalized "nice number" tick generation for a linear value axis (spec
/// §2.1) — the classic round-to-1/2/5×10ⁿ algorithm (a generalization of the
/// owner's reference demo's <c>niceStep</c>/<c>niceTicks</c>, which assumed
/// <c>min == 0</c>; this version handles arbitrary, including negative-spanning,
/// domains). Pure function, zero ECharts/JS dependency.
/// </summary>
internal static class NiceTicks
{
    /// <summary>
    /// Rounds <paramref name="range"/> to the nearest "nice" number: for
    /// <paramref name="round"/> = false (sizing the overall nice range), picks
    /// the smallest of {1,2,5,10}×10ⁿ that is ≥ range; for <paramref name="round"/>
    /// = true (sizing the per-tick step), picks whichever of {1,2,5,10}×10ⁿ is
    /// closest.
    /// </summary>
    public static double NiceNumber(double range, bool round)
    {
        if (range <= 0) return 0;
        var exponent = Math.Floor(Math.Log10(range));
        var fraction = range / Math.Pow(10, exponent);
        double niceFraction;
        if (round)
        {
            niceFraction = fraction switch
            {
                < 1.5 => 1,
                < 3 => 2,
                < 7 => 5,
                _ => 10,
            };
        }
        else
        {
            niceFraction = fraction switch
            {
                <= 1 => 1,
                <= 2 => 2,
                <= 5 => 5,
                _ => 10,
            };
        }
        return niceFraction * Math.Pow(10, exponent);
    }

    /// <summary>
    /// Computes a "nice" tick list covering <c>[min,max]</c>, targeting roughly
    /// <paramref name="targetCount"/> ticks. Handles the degenerate <c>min ==
    /// max</c> case (single-point / zero-span domain) by returning a small
    /// symmetric 3-tick set around the value instead of dividing by zero.
    /// </summary>
    public static IReadOnlyList<double> Compute(double min, double max, int targetCount = 5)
    {
        if (targetCount < 2) targetCount = 2;
        if (min > max) (min, max) = (max, min);

        if (min == max)
        {
            if (min == 0) return new[] { -1.0, 0.0, 1.0 };
            var mag = Math.Pow(10, Math.Floor(Math.Log10(Math.Abs(min))));
            return new[] { min - mag, min, min + mag };
        }

        var niceRange = NiceNumber(max - min, false);
        var step = NiceNumber(niceRange / (targetCount - 1), true);
        var niceMin = Math.Floor(min / step) * step;
        var niceMax = Math.Ceiling(max / step) * step;

        // Count-based loop (not a floating-point accumulation loop) so rounding
        // error can't drop or duplicate the final tick.
        var n = (int)Math.Round((niceMax - niceMin) / step) + 1;
        var ticks = new List<double>(n);
        for (var i = 0; i < n; i++)
        {
            // Round away FP noise (e.g. 0.30000000000000004) to a sane number of
            // decimals derived from the step's own magnitude.
            var raw = niceMin + i * step;
            ticks.Add(RoundToStepPrecision(raw, step));
        }
        return ticks;
    }

    private static double RoundToStepPrecision(double value, double step)
    {
        var stepExponent = (int)Math.Floor(Math.Log10(Math.Abs(step) is 0 ? 1 : Math.Abs(step)));
        var decimals = Math.Max(0, -stepExponent + 6);
        return Math.Round(value, Math.Min(decimals, 15), MidpointRounding.AwayFromZero);
    }
}
