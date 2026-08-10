using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Charts.Interaction;

public class ChartLegendStateTests
{
    [Fact]
    public void Toggle_Hides_A_Visible_Series()
    {
        var state = new L.ChartLegendState();
        var changed = state.Toggle("a", totalSeriesCount: 3);

        Assert.True(changed);
        Assert.True(state.IsHidden("a"));
    }

    [Fact]
    public void Toggle_Again_Shows_It_Back()
    {
        var state = new L.ChartLegendState();
        state.Toggle("a", 3);
        var changed = state.Toggle("a", 3);

        Assert.True(changed);
        Assert.False(state.IsHidden("a"));
    }

    [Fact]
    public void Refuses_To_Hide_The_Last_Visible_Series()
    {
        var state = new L.ChartLegendState();
        state.Toggle("a", totalSeriesCount: 2);
        var changed = state.Toggle("b", totalSeriesCount: 2); // would hide the last visible one

        Assert.False(changed);
        Assert.False(state.IsHidden("b"));
    }

    [Fact]
    public void Single_Series_Chart_Cannot_Be_Hidden_At_All()
    {
        var state = new L.ChartLegendState();
        var changed = state.Toggle("only", totalSeriesCount: 1);

        Assert.False(changed);
    }

    [Fact]
    public void Hover_Dims_Every_Other_Series()
    {
        var state = new L.ChartLegendState();
        state.SetHover("a");

        Assert.False(state.IsDimmed("a"));
        Assert.True(state.IsDimmed("b"));
    }

    [Fact]
    public void No_Hover_Dims_Nothing()
    {
        var state = new L.ChartLegendState();
        Assert.False(state.IsDimmed("a"));
        Assert.False(state.IsDimmed("b"));
    }
}
