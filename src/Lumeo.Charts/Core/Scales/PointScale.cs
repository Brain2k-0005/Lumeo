namespace Lumeo;

/// <summary>
/// Maps N categories to N centered points with no inter-point padding
/// (<c>boundaryGap:false</c> equivalent) — line/area x-positioning (spec §2.1).
/// Equivalent to a <see cref="BandScale"/> with bandwidth 0 and
/// <c>paddingInner = 1</c>.
/// </summary>
internal sealed class PointScale
{
    public int Count { get; }
    public double RangeMin { get; }
    public double RangeMax { get; }
    public double PaddingOuter { get; }

    private readonly double _step;
    private readonly double _start;

    public PointScale(int count, double rangeMin, double rangeMax, double paddingOuter = 0)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));

        Count = count;
        RangeMin = rangeMin;
        RangeMax = rangeMax;
        PaddingOuter = Math.Max(paddingOuter, 0);

        var span = rangeMax - rangeMin;
        if (count <= 1)
        {
            _step = 0;
            _start = count == 1 ? (rangeMin + rangeMax) / 2 : rangeMin;
            return;
        }

        _step = span / Math.Max(1e-9, count - 1 + 2 * PaddingOuter);
        _start = rangeMin + _step * PaddingOuter;
    }

    /// <summary>Pixel position of the point at <paramref name="index"/>.</summary>
    public double Position(int index)
    {
        if (Count == 0) throw new InvalidOperationException("PointScale has no points (Count == 0).");
        if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
        return Count == 1 ? _start : _start + index * _step;
    }
}
