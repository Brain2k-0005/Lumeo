namespace Lumeo;

/// <summary>
/// Largest-Triangle-Three-Buckets downsampling (spec §2.4 point 2) — a direct
/// port of the owner's reference performance demo (chartsperformance.html,
/// ~lines 257-285). Deterministic and pure: selects <c>threshold</c>
/// INDICES into the original array whose triangle-area heuristic best preserves
/// the series' visual shape (including outlier spikes), always keeping the
/// first and last point. Downstream hit-testing indexes into the ORIGINAL
/// array, not this reduced one, so tooltips still show exact values even when
/// the drawn path is simplified (spec §3.3).
/// </summary>
internal static class Lttb
{
    /// <summary>
    /// Returns the selected indices. When <paramref name="threshold"/> is
    /// &gt;= the input length, or &lt; 3 (the algorithm is undefined below 3
    /// output buckets — first + last + at least one interior), returns every
    /// index unchanged (no downsampling).
    /// </summary>
    public static int[] SelectIndices(IReadOnlyList<double> data, int threshold)
    {
        var n = data.Count;
        if (threshold >= n || threshold < 3)
        {
            var all = new int[n];
            for (var i = 0; i < n; i++) all[i] = i;
            return all;
        }

        var outIdx = new int[threshold];
        var bucketSize = (double)(n - 2) / (threshold - 2);
        var a = 0;
        outIdx[0] = 0;

        for (var i = 0; i < threshold - 2; i++)
        {
            var rangeStart = (int)Math.Floor((i + 1) * bucketSize) + 1;
            var rangeEnd = Math.Min((int)Math.Floor((i + 2) * bucketSize) + 1, n);

            double avgX = 0, avgY = 0;
            for (var j = rangeStart; j < rangeEnd; j++)
            {
                avgX += j;
                avgY += data[j];
            }
            var avgCount = Math.Max(1, rangeEnd - rangeStart);
            avgX /= avgCount;
            avgY /= avgCount;

            var bucketStart = (int)Math.Floor(i * bucketSize) + 1;
            var bucketEnd = Math.Min((int)Math.Floor((i + 1) * bucketSize) + 1, n);

            var maxArea = -1.0;
            var maxIdx = bucketStart;
            var ay = data[a];
            for (var j = bucketStart; j < bucketEnd; j++)
            {
                var area = Math.Abs((a - avgX) * (data[j] - ay) - (a - j) * (avgY - ay));
                if (area > maxArea)
                {
                    maxArea = area;
                    maxIdx = j;
                }
            }

            outIdx[i + 1] = maxIdx;
            a = maxIdx;
        }

        outIdx[threshold - 1] = n - 1;
        return outIdx;
    }
}

/// <summary>
/// Downsampling policy: the target output point count before SVG rendering
/// (spec §2.4 point 2) — roughly 2 points per horizontal device pixel, clamped
/// to <c>[300, 4000]</c>.
/// </summary>
internal static class ChartDownsampling
{
    public const int MinTargetPoints = 300;
    public const int MaxTargetPoints = 4000;

    public static int TargetPointCount(double viewportPxWidth)
    {
        var raw = (int)Math.Round(viewportPxWidth * 2);
        return Math.Clamp(raw, MinTargetPoints, MaxTargetPoints);
    }
}
