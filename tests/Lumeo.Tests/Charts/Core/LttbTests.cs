using System.Linq;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Charts.Core;

public class LttbTests
{
    [Fact]
    public void Always_Keeps_First_And_Last_Index()
    {
        var data = MakeFlatSeriesWithNoise(500);
        var idx = L.Lttb.SelectIndices(data, threshold: 50);

        Assert.Equal(0, idx[0]);
        Assert.Equal(data.Length - 1, idx[^1]);
    }

    [Fact]
    public void Output_Length_Matches_Requested_Threshold()
    {
        var data = MakeFlatSeriesWithNoise(1000);
        var idx = L.Lttb.SelectIndices(data, threshold: 100);

        Assert.Equal(100, idx.Length);
    }

    [Fact]
    public void Threshold_At_Or_Above_Length_Returns_Every_Index_Unchanged()
    {
        var data = MakeFlatSeriesWithNoise(50);
        var idx = L.Lttb.SelectIndices(data, threshold: 50);

        Assert.Equal(Enumerable.Range(0, 50), idx);
    }

    [Fact]
    public void Threshold_Below_Three_Returns_Every_Index_Unchanged()
    {
        var data = MakeFlatSeriesWithNoise(50);
        var idx = L.Lttb.SelectIndices(data, threshold: 2);

        Assert.Equal(50, idx.Length);
    }

    // The load-bearing test the spec explicitly calls for: a deliberately
    // injected spike must survive aggressive downsampling. A test that only
    // checks output LENGTH proves nothing about whether the algorithm
    // actually preserves shape — this asserts the spike's INDEX is present in
    // the selected set.
    [Fact]
    public void Preserves_A_Deliberately_Injected_Outlier_Spike()
    {
        const int n = 2000;
        const int spikeIndex = 733;
        var data = new double[n];
        for (var i = 0; i < n; i++) data[i] = 50 + Math.Sin(i * 0.01) * 2; // gentle baseline wave
        data[spikeIndex] = 500; // sharp, isolated spike far outside the baseline's range

        var idx = L.Lttb.SelectIndices(data, threshold: 100);

        Assert.Contains(spikeIndex, idx);
    }

    [Fact]
    public void Preserves_Multiple_Spikes_Simultaneously()
    {
        const int n = 5000;
        var spikeIndices = new[] { 400, 1800, 3300, 4600 };
        var data = new double[n];
        for (var i = 0; i < n; i++) data[i] = 20; // perfectly flat baseline
        foreach (var i in spikeIndices) data[i] = 900; // sharp spikes, easy to lose

        var idx = L.Lttb.SelectIndices(data, threshold: 200).ToHashSet();

        foreach (var spike in spikeIndices)
            Assert.Contains(spike, idx);
    }

    [Fact]
    public void Deterministic_For_The_Same_Input_And_Threshold()
    {
        var data = MakeFlatSeriesWithNoise(3000);
        var a = L.Lttb.SelectIndices(data, 150);
        var b = L.Lttb.SelectIndices(data, 150);

        Assert.Equal(a, b);
    }

    // --- Disable-check ---
    // A naive "keep every Nth point" downsampler (the thing LTTB exists to
    // beat) has NO area-heuristic step at all — it would deterministically
    // MISS an outlier spike whose index isn't a multiple of the stride.
    // Predicted: with n=1000, threshold=100 (stride 10), a spike planted at
    // index 733 (not a multiple of 10) is dropped by naive striding but kept
    // by real LTTB.
    [Fact]
    public void DisableCheck_Naive_Stride_Sampling_Would_Miss_An_OffStride_Spike()
    {
        const int n = 1000;
        const int threshold = 100;
        const int spikeIndex = 733; // 733 % (1000/100=10) == 3, i.e. off-stride

        var data = new double[n];
        for (var i = 0; i < n; i++) data[i] = 10;
        data[spikeIndex] = 999;

        var stride = n / threshold;
        var naiveIdx = Enumerable.Range(0, threshold).Select(i => i * stride).ToArray();
        Assert.DoesNotContain(spikeIndex, naiveIdx); // predicted broken behavior confirmed

        var realIdx = L.Lttb.SelectIndices(data, threshold);
        Assert.Contains(spikeIndex, realIdx); // real LTTB must not reproduce that bug
    }

    [Fact]
    [Trait("Category", "Perf")]
    public void Perf_500K_Points_Completes_Well_Under_Budget()
    {
        var data = MakeFlatSeriesWithNoise(500_000);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var idx = L.Lttb.SelectIndices(data, threshold: 2000);
        sw.Stop();

        Assert.Equal(2000, idx.Length);
        Assert.True(sw.ElapsedMilliseconds < 200,
            $"LTTB over 500K points took {sw.ElapsedMilliseconds}ms, budget is 200ms");
    }

    private static double[] MakeFlatSeriesWithNoise(int n)
    {
        var rnd = new Random(1234);
        var data = new double[n];
        for (var i = 0; i < n; i++) data[i] = 50 + rnd.NextDouble() * 4;
        return data;
    }
}
