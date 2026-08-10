using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Charts.Core;

public class ChartStatisticsTests
{
    [Fact]
    public void Quartiles_Of_A_Simple_Ascending_Set_Match_Linear_Interpolation()
    {
        // 0..10 inclusive (11 samples): median = 5, Q1 = 2.5, Q3 = 7.5
        // (linear-interpolation percentile — the same method NumPy's default uses).
        var samples = Enumerable.Range(0, 11).Select(i => (double)i).ToArray();

        var stats = L.ChartStatistics.Quartiles(samples);

        Assert.Equal(0, stats.Min);
        Assert.Equal(10, stats.Max);
        Assert.Equal(5, stats.Median, precision: 9);
        Assert.Equal(2.5, stats.Q1, precision: 9);
        Assert.Equal(7.5, stats.Q3, precision: 9);
    }

    [Fact]
    public void Outliers_Beyond_OnePointFive_IQR_Are_Excluded_From_Whiskers()
    {
        var samples = new List<double> { 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 };
        samples.Add(500); // a single extreme outlier

        var stats = L.ChartStatistics.Quartiles(samples);

        Assert.Contains(500.0, stats.Outliers);
        Assert.True(stats.WhiskerHigh < 500);
    }

    [Fact]
    public void No_Outliers_When_Data_Is_Tightly_Clustered()
    {
        var samples = new double[] { 10, 10.5, 11, 11.5, 12, 12.5, 13 };
        var stats = L.ChartStatistics.Quartiles(samples);

        Assert.Empty(stats.Outliers);
    }

    [Fact]
    public void Single_Sample_Does_Not_Throw()
    {
        var stats = L.ChartStatistics.Quartiles(new[] { 42.0 });
        Assert.Equal(42, stats.Median);
        Assert.Equal(42, stats.Min);
        Assert.Equal(42, stats.Max);
    }

    [Fact]
    public void Empty_Samples_Throws()
    {
        Assert.Throws<ArgumentException>(() => L.ChartStatistics.Quartiles(Array.Empty<double>()));
    }

    [Fact]
    public void Unsorted_Input_Is_Handled_Same_As_Sorted()
    {
        var sorted = new double[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        var shuffled = new double[] { 5, 1, 9, 3, 7, 2, 8, 4, 6 };

        var a = L.ChartStatistics.Quartiles(sorted);
        var b = L.ChartStatistics.Quartiles(shuffled);

        Assert.Equal(a.Median, b.Median);
        Assert.Equal(a.Q1, b.Q1);
        Assert.Equal(a.Q3, b.Q3);
    }
}
