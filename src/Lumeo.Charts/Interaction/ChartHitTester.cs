namespace Lumeo;

/// <summary>
/// O(1) index arithmetic for ordered (monotonic index→x) cartesian series — the
/// hit-testing approach spec §3.3 mandates for line/area/bar at any N,
/// including 500K points. No geometric search of any kind: the index is
/// derived directly from the pointer's fractional position across the plot
/// rect, so cost is independent of series length.
/// </summary>
internal static class ChartHitTester
{
    /// <summary>
    /// Resolves the data index nearest <paramref name="pointerX"/>, into the
    /// ORIGINAL (non-downsampled) array of <paramref name="pointCount"/>
    /// points — so a tooltip/crosshair shows the real value even when the drawn
    /// path was LTTB-simplified (spec §3.3: index into the original array, not
    /// the reduced one).
    /// </summary>
    public static int IndexForPointerX(double pointerX, double plotOriginX, double plotWidth, int pointCount)
    {
        if (pointCount <= 0) return -1;
        if (pointCount == 1) return 0;
        if (plotWidth <= 0) return 0;

        var t = (pointerX - plotOriginX) / plotWidth;
        var raw = (int)Math.Round(t * (pointCount - 1));
        return Math.Clamp(raw, 0, pointCount - 1);
    }
}

/// <summary>
/// Uniform spatial hash grid for hit-testing discrete (non-ordered) point sets
/// once they exceed the shape-count budget and rendering has fallen back to
/// Canvas (spec §3.3's answer for the dense-scatter-on-Canvas case: "a C#-built
/// spatial grid, queried the same rAF-throttled way"). Below the budget, SVG's
/// own native per-shape pointer events make this unnecessary entirely — this
/// type exists only for the Canvas-fallback path.
/// </summary>
internal sealed class ChartSpatialGrid
{
    private readonly Dictionary<(int, int), List<int>> _cells = new();
    private readonly IReadOnlyList<(double X, double Y)> _points;
    private readonly double _cellSize;

    public ChartSpatialGrid(IReadOnlyList<(double X, double Y)> points, double cellSize)
    {
        if (cellSize <= 0) throw new ArgumentOutOfRangeException(nameof(cellSize));
        _points = points;
        _cellSize = cellSize;

        for (var i = 0; i < points.Count; i++)
        {
            var key = CellOf(points[i].X, points[i].Y);
            if (!_cells.TryGetValue(key, out var list))
                _cells[key] = list = new List<int>();
            list.Add(i);
        }
    }

    private (int, int) CellOf(double x, double y) =>
        ((int)Math.Floor(x / _cellSize), (int)Math.Floor(y / _cellSize));

    /// <summary>Index of the nearest point within <paramref name="maxDistance"/>
    /// px of <paramref name="x"/>/<paramref name="y"/>, or -1 when none qualifies.</summary>
    public int Nearest(double x, double y, double maxDistance)
    {
        var (cx, cy) = CellOf(x, y);
        var radiusCells = (int)Math.Ceiling(maxDistance / _cellSize);
        var best = -1;
        var bestDistSq = maxDistance * maxDistance;

        for (var dx = -radiusCells; dx <= radiusCells; dx++)
        {
            for (var dy = -radiusCells; dy <= radiusCells; dy++)
            {
                if (!_cells.TryGetValue((cx + dx, cy + dy), out var list)) continue;
                foreach (var idx in list)
                {
                    var (px, py) = _points[idx];
                    var distSq = (px - x) * (px - x) + (py - y) * (py - y);
                    if (distSq <= bestDistSq)
                    {
                        bestDistSq = distSq;
                        best = idx;
                    }
                }
            }
        }
        return best;
    }
}
