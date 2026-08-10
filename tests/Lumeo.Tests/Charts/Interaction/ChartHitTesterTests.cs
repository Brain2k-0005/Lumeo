using System.Diagnostics;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Charts.Interaction;

public class ChartHitTesterTests
{
    [Fact]
    public void Pointer_At_Plot_Origin_Resolves_To_First_Index()
    {
        var idx = L.ChartHitTester.IndexForPointerX(pointerX: 0, plotOriginX: 0, plotWidth: 400, pointCount: 100);
        Assert.Equal(0, idx);
    }

    [Fact]
    public void Pointer_At_Plot_End_Resolves_To_Last_Index()
    {
        var idx = L.ChartHitTester.IndexForPointerX(pointerX: 400, plotOriginX: 0, plotWidth: 400, pointCount: 100);
        Assert.Equal(99, idx);
    }

    [Fact]
    public void Pointer_At_Midpoint_Resolves_Near_The_Middle_Index()
    {
        var idx = L.ChartHitTester.IndexForPointerX(pointerX: 200, plotOriginX: 0, plotWidth: 400, pointCount: 101);
        Assert.Equal(50, idx);
    }

    [Fact]
    public void Pointer_Before_Origin_Clamps_To_Zero()
    {
        var idx = L.ChartHitTester.IndexForPointerX(pointerX: -50, plotOriginX: 0, plotWidth: 400, pointCount: 100);
        Assert.Equal(0, idx);
    }

    [Fact]
    public void Pointer_Past_End_Clamps_To_Last_Index()
    {
        var idx = L.ChartHitTester.IndexForPointerX(pointerX: 999, plotOriginX: 0, plotWidth: 400, pointCount: 100);
        Assert.Equal(99, idx);
    }

    [Fact]
    public void Zero_Points_Returns_Negative_One()
    {
        Assert.Equal(-1, L.ChartHitTester.IndexForPointerX(100, 0, 400, 0));
    }

    [Fact]
    public void Single_Point_Always_Resolves_To_Index_Zero()
    {
        Assert.Equal(0, L.ChartHitTester.IndexForPointerX(9999, 0, 400, 1));
    }

    [Fact]
    public void Zero_Plot_Width_Does_Not_Throw_And_Returns_First_Index()
    {
        Assert.Equal(0, L.ChartHitTester.IndexForPointerX(50, 0, 0, 100));
    }

    // Cost must be O(1) regardless of N — the whole point of spec §3.3's
    // "no geometric hit-testing" answer. Assert lookup at N=500,000 costs
    // roughly the same as at N=500 (allow generous headroom for JIT/measurement
    // noise; this is a shape assertion, not a tight micro-benchmark).
    [Fact]
    [Trait("Category", "Perf")]
    public void Perf_Index_Lookup_Cost_Is_Independent_Of_N()
    {
        const int iterations = 200_000;

        var smallSw = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
            L.ChartHitTester.IndexForPointerX(i % 500, 0, 500, pointCount: 500);
        smallSw.Stop();

        var bigSw = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
            L.ChartHitTester.IndexForPointerX(i % 500, 0, 500, pointCount: 500_000);
        bigSw.Stop();

        var ratio = (bigSw.Elapsed.TotalMilliseconds + 0.01) / (smallSw.Elapsed.TotalMilliseconds + 0.01);
        Assert.True(ratio < 3.0, $"N=500,000 lookup took {ratio:F2}x the N=500 cost — expected O(1), not O(N)");
    }
}

public class ChartSpatialGridTests
{
    [Fact]
    public void Finds_The_Nearest_Point_Within_Range()
    {
        var points = new (double X, double Y)[] { (0, 0), (10, 10), (50, 50) };
        var grid = new L.ChartSpatialGrid(points, cellSize: 20);

        var nearest = grid.Nearest(9, 9, maxDistance: 5);
        Assert.Equal(1, nearest);
    }

    [Fact]
    public void Returns_Negative_One_When_Nothing_Is_Within_Range()
    {
        var points = new (double X, double Y)[] { (0, 0) };
        var grid = new L.ChartSpatialGrid(points, cellSize: 20);

        Assert.Equal(-1, grid.Nearest(500, 500, maxDistance: 5));
    }

    [Fact]
    public void Picks_The_Closer_Of_Two_Nearby_Candidates()
    {
        var points = new (double X, double Y)[] { (0, 0), (100, 100), (1.5, 1.5) };
        var grid = new L.ChartSpatialGrid(points, cellSize: 10);

        var nearest = grid.Nearest(1, 1, maxDistance: 50);
        Assert.Equal(2, nearest); // index 2 = (1.5,1.5), unambiguously closer than index 0 = (0,0)
    }
}
