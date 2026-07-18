namespace Lumeo;

/// <summary>
/// Pure numeric helpers shared by every stepper-style numeric control (<see cref="NumberInput"/>
/// and the generic <see cref="Input"/>'s vertical steppers for <c>type="number"</c>). Extracted
/// from <see cref="NumberInput"/> so the two controls apply the exact same clamp / floating-point
/// cleanup rules — no typed state, no side effects.
/// </summary>
internal static class NumberStepper
{
    /// <summary>Clamps <paramref name="value"/> into [<paramref name="min"/>, <paramref name="max"/>]. Either bound may be <c>null</c> to leave that side unbounded.</summary>
    public static double Clamp(double value, double? min, double? max)
    {
        if (min.HasValue && value < min.Value) value = min.Value;
        if (max.HasValue && value > max.Value) value = max.Value;
        return value;
    }

    // Number of fractional digits in a step's invariant round-trip representation, clamped to
    // the double-safe 0..15 range Math.Round accepts.
    public static int StepDecimalDigits(double step)
    {
        var s = step.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        var dot = s.IndexOf('.');
        if (dot < 0 || s.IndexOf('E') >= 0 || s.IndexOf('e') >= 0) return 0;
        return Math.Min(s.Length - dot - 1, 15);
    }

    /// <summary>
    /// Stepping a binary double by a fractional step (e.g. 0.1) accumulates FP error
    /// (0.1 + 0.2 -&gt; 0.30000000000000004). When <paramref name="precision"/> is null, snap the
    /// stepped result to the step's own decimal scale so successive +/- stay clean; when
    /// <paramref name="precision"/> is set, the caller already rounds so this is a no-op passthrough.
    /// </summary>
    public static double RoundStepped(double value, double step, int? precision)
    {
        if (precision.HasValue) return value;
        var digits = StepDecimalDigits(step);
        return digits == 0 ? value : Math.Round(value, digits, MidpointRounding.AwayFromZero);
    }
}
