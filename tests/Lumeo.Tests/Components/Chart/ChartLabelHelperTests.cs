using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Chart;

public class ChartLabelHelperTests
{
    [Fact]
    public void Smart_With_Five_Categories_Sets_Interval_Zero_And_No_Rotate()
    {
        var (interval, rotate) = L.ChartLabelHelper.Resolve(L.ChartLabelStrategy.Smart, 5, null);

        Assert.Equal(0, interval);
        Assert.Null(rotate);
    }

    [Fact]
    public void Smart_With_Fifteen_Categories_Rotates_Minus_Thirty()
    {
        var (interval, rotate) = L.ChartLabelHelper.Resolve(L.ChartLabelStrategy.Smart, 15, null);

        Assert.Equal(0, interval);
        Assert.Equal(-30, rotate);
    }

    [Fact]
    public void Smart_With_Twenty_Categories_Rotates_Minus_Sixty()
    {
        var (interval, rotate) = L.ChartLabelHelper.Resolve(L.ChartLabelStrategy.Smart, 20, null);

        Assert.Equal(0, interval);
        Assert.Equal(-60, rotate);
    }

    [Fact]
    public void Smart_With_Thirty_Categories_Rotates_Minus_SeventyFive()
    {
        var (interval, rotate) = L.ChartLabelHelper.Resolve(L.ChartLabelStrategy.Smart, 30, null);

        Assert.Equal(0, interval);
        Assert.Equal(-75, rotate);
    }

    [Fact]
    public void ShowAll_Sets_Interval_Zero_No_Rotate()
    {
        var (interval, rotate) = L.ChartLabelHelper.Resolve(L.ChartLabelStrategy.ShowAll, 30, null);

        Assert.Equal(0, interval);
        Assert.Null(rotate);
    }

    [Fact]
    public void Auto_Returns_Null_Interval_And_Null_Rotate()
    {
        var (interval, rotate) = L.ChartLabelHelper.Resolve(L.ChartLabelStrategy.Auto, 30, null);

        Assert.Null(interval);
        Assert.Null(rotate);
    }

    [Fact]
    public void Manual_Rotate_Overrides_Smart_Auto_Rotate()
    {
        var (interval, rotate) = L.ChartLabelHelper.Resolve(L.ChartLabelStrategy.Smart, 30, -45);

        Assert.Equal(0, interval);
        Assert.Equal(-45, rotate);
    }

    [Fact]
    public void ApplyTo_Creates_AxisLabel_When_Smart_Needs_Rotation()
    {
        var axis = new L.EChartAxis { Type = "category" };

        L.ChartLabelHelper.ApplyTo(axis, L.ChartLabelStrategy.Smart, 20, null);

        Assert.NotNull(axis.AxisLabel);
        Assert.Equal(0, axis.AxisLabel!.Interval);
        Assert.Equal(-60, axis.AxisLabel.Rotate);
    }

    [Fact]
    public void ApplyTo_Leaves_AxisLabel_Null_For_Auto_Without_Manual_Rotate()
    {
        var axis = new L.EChartAxis { Type = "category" };

        L.ChartLabelHelper.ApplyTo(axis, L.ChartLabelStrategy.Auto, 50, null);

        Assert.Null(axis.AxisLabel);
    }

    [Fact]
    public void ApplyTo_Creates_AxisLabel_For_Auto_With_Manual_Rotate()
    {
        var axis = new L.EChartAxis { Type = "category" };

        L.ChartLabelHelper.ApplyTo(axis, L.ChartLabelStrategy.Auto, 50, -20);

        Assert.NotNull(axis.AxisLabel);
        Assert.Null(axis.AxisLabel!.Interval);
        Assert.Equal(-20, axis.AxisLabel.Rotate);
    }
}
