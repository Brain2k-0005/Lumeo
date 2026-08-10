namespace Lumeo;

/// <summary>
/// Multi-axis interval brush (spec §3.4's "Brush selection" — the same drag-
/// rectangle-to-domain-selection idea, generalized to N independent axes).
/// Backs both the single-axis DataZoom-style brush AND Parallel Coordinates'
/// per-axis brushing, where an item survives only when EVERY active brush
/// (AND, not OR) contains its value on that axis. C# owns the whole
/// membership test; the drag itself is a plain SVG <c>&lt;rect&gt;</c> overlay
/// positioned via the existing pointer-capture interop (spec §3.4), no new JS
/// call.
/// </summary>
internal sealed class ChartBrushState
{
    private readonly Dictionary<int, (double Min, double Max)> _brushes = new();

    public IReadOnlyCollection<int> ActiveAxes => _brushes.Keys;

    /// <summary>Sets (or replaces) the brush interval for <paramref name="axisIndex"/>.
    /// Endpoints are order-independent — whichever of <paramref name="a"/>/
    /// <paramref name="b"/> is smaller becomes the min.</summary>
    public void Set(int axisIndex, double a, double b)
    {
        var min = Math.Min(a, b);
        var max = Math.Max(a, b);
        _brushes[axisIndex] = (min, max);
    }

    public void Clear(int axisIndex) => _brushes.Remove(axisIndex);

    public void ClearAll() => _brushes.Clear();

    public bool HasBrush(int axisIndex) => _brushes.ContainsKey(axisIndex);

    public (double Min, double Max)? Get(int axisIndex) =>
        _brushes.TryGetValue(axisIndex, out var range) ? range : null;

    /// <summary>
    /// True when <paramref name="valuesByAxis"/>[i] falls inside the active
    /// brush for every axis that HAS one (axes with no active brush impose no
    /// constraint). With zero active brushes, everything matches — matching
    /// the reference demo's "no filter until you drag one" behavior.
    /// </summary>
    public bool Matches(IReadOnlyList<double> valuesByAxis)
    {
        foreach (var (axisIndex, range) in _brushes)
        {
            if (axisIndex < 0 || axisIndex >= valuesByAxis.Count) continue;
            var v = valuesByAxis[axisIndex];
            if (v < range.Min || v > range.Max) return false;
        }
        return true;
    }
}
