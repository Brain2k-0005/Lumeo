namespace Lumeo;

/// <summary>
/// Logarithmic scale mapping a strictly-positive numeric domain to a pixel
/// range (spec §2.1).
/// </summary>
/// <remarks>
/// <b>Zero/negative-value policy (explicit, per spec §2.1):</b> ECharts silently
/// misrenders a log axis whose domain touches zero or goes negative. This engine
/// throws instead — a loud <see cref="ArgumentOutOfRangeException"/> at
/// construction/scale time surfaces the bug during development rather than
/// shipping a blank or garbled chart. Callers that need to plot data crossing
/// zero must choose a different scale (linear) or pre-clamp/filter their data;
/// this type does not silently clamp on their behalf.
/// </remarks>
internal sealed class LogScale
{
    public double DomainMin { get; }
    public double DomainMax { get; }
    public double RangeMin { get; }
    public double RangeMax { get; }
    public double Base { get; }

    public LogScale(double domainMin, double domainMax, double rangeMin, double rangeMax, double @base = 10)
    {
        if (domainMin <= 0 || domainMax <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(domainMin),
                "LogScale requires a strictly positive domain (both bounds must be > 0). " +
                "ECharts silently misrenders zero/negative values on a log axis; this engine " +
                "throws instead so the problem surfaces during development.");
        }
        if (@base <= 1)
            throw new ArgumentOutOfRangeException(nameof(@base), "LogScale base must be > 1.");

        DomainMin = domainMin;
        DomainMax = domainMax;
        RangeMin = rangeMin;
        RangeMax = rangeMax;
        Base = @base;
    }

    private double LogB(double v) => Math.Log(v, Base);

    /// <summary>Maps a strictly-positive domain value to a pixel position.
    /// Throws for zero/negative <paramref name="value"/> per this type's
    /// documented zero-value policy.</summary>
    public double Scale(double value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value), "LogScale cannot map a non-positive value.");
        }

        var logMin = LogB(DomainMin);
        var logMax = LogB(DomainMax);
        var span = logMax - logMin;
        if (span == 0) return (RangeMin + RangeMax) / 2;
        var t = (LogB(value) - logMin) / span;
        return RangeMin + t * (RangeMax - RangeMin);
    }

    /// <summary>Inverse of <see cref="Scale"/>.</summary>
    public double Invert(double pixel)
    {
        var rangeSpan = RangeMax - RangeMin;
        var logMin = LogB(DomainMin);
        var logMax = LogB(DomainMax);
        var t = rangeSpan == 0 ? 0 : (pixel - RangeMin) / rangeSpan;
        var logV = logMin + t * (logMax - logMin);
        return Math.Pow(Base, logV);
    }
}
