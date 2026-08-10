namespace Lumeo;

/// <summary>
/// Maps N categories to N equal padded bands within a pixel range — bar-chart
/// x-positioning (spec §2.1). Same formula as d3-scale's band scale:
/// <c>step = span / max(1, count - paddingInner + 2*paddingOuter)</c>,
/// <c>bandwidth = step * (1 - paddingInner)</c>.
/// </summary>
internal sealed class BandScale
{
    public int Count { get; }
    public double RangeMin { get; }
    public double RangeMax { get; }
    public double PaddingInner { get; }
    public double PaddingOuter { get; }

    /// <summary>Width of a single band in pixels.</summary>
    public double Bandwidth { get; }

    private readonly double _step;
    private readonly double _start;

    public BandScale(int count, double rangeMin, double rangeMax, double paddingInner = 0.2, double paddingOuter = 0.1)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));

        Count = count;
        RangeMin = rangeMin;
        RangeMax = rangeMax;
        PaddingInner = Math.Clamp(paddingInner, 0, 1);
        PaddingOuter = Math.Max(paddingOuter, 0);

        if (count == 0)
        {
            Bandwidth = 0;
            _step = 0;
            _start = rangeMin;
            return;
        }

        var span = rangeMax - rangeMin;
        _step = span / Math.Max(1.0, count - PaddingInner + 2 * PaddingOuter);
        Bandwidth = _step * (1 - PaddingInner);
        _start = rangeMin + _step * PaddingOuter;
    }

    /// <summary>Left edge of the band at <paramref name="index"/>.</summary>
    public double Start(int index)
    {
        if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
        return _start + index * _step;
    }

    /// <summary>Horizontal center of the band at <paramref name="index"/>.</summary>
    public double Center(int index) => Start(index) + Bandwidth / 2;
}
