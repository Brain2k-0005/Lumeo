namespace Lumeo;

/// <summary>
/// Continuous linear scale mapping a numeric domain to a pixel range. The
/// foundational scale used wherever a value axis exists (spec §2.1). Pure,
/// stateless math — no ECharts dependency, no JS.
/// </summary>
internal sealed class LinearScale
{
    /// <summary>Lower bound of the input domain.</summary>
    public double DomainMin { get; }

    /// <summary>Upper bound of the input domain.</summary>
    public double DomainMax { get; }

    /// <summary>Lower bound of the output pixel range.</summary>
    public double RangeMin { get; }

    /// <summary>Upper bound of the output pixel range.</summary>
    public double RangeMax { get; }

    public LinearScale(double domainMin, double domainMax, double rangeMin, double rangeMax)
    {
        DomainMin = domainMin;
        DomainMax = domainMax;
        RangeMin = rangeMin;
        RangeMax = rangeMax;
    }

    /// <summary>Maps a domain value to a pixel position. A zero-span domain
    /// (degenerate single-value domain) maps every value to the range midpoint
    /// rather than dividing by zero.</summary>
    public double Scale(double value)
    {
        var domainSpan = DomainMax - DomainMin;
        if (domainSpan == 0) return (RangeMin + RangeMax) / 2;
        var t = (value - DomainMin) / domainSpan;
        return RangeMin + t * (RangeMax - RangeMin);
    }

    /// <summary>Inverse of <see cref="Scale"/>: maps a pixel position back to a
    /// domain value (used by hit-testing, zoom, and keyboard nav).</summary>
    public double Invert(double pixel)
    {
        var rangeSpan = RangeMax - RangeMin;
        if (rangeSpan == 0) return DomainMin;
        var t = (pixel - RangeMin) / rangeSpan;
        return DomainMin + t * (DomainMax - DomainMin);
    }
}
