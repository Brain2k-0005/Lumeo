using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Charts.Interaction;

public class ChartBrushStateTests
{
    [Fact]
    public void No_Active_Brushes_Matches_Everything()
    {
        var state = new L.ChartBrushState();
        Assert.True(state.Matches(new[] { 1.0, 2.0, 3.0 }));
    }

    [Fact]
    public void Single_Axis_Brush_Filters_Out_Values_Outside_Its_Range()
    {
        var state = new L.ChartBrushState();
        state.Set(axisIndex: 0, a: 10, b: 20);

        Assert.True(state.Matches(new[] { 15.0, 999.0 }));
        Assert.False(state.Matches(new[] { 5.0, 999.0 }));
    }

    [Fact]
    public void Multiple_Axis_Brushes_Are_AndCombined()
    {
        var state = new L.ChartBrushState();
        state.Set(0, 10, 20);
        state.Set(2, 100, 200);

        Assert.True(state.Matches(new[] { 15.0, 0.0, 150.0 }));
        Assert.False(state.Matches(new[] { 15.0, 0.0, 999.0 })); // axis 2 out of range
        Assert.False(state.Matches(new[] { 999.0, 0.0, 150.0 })); // axis 0 out of range
    }

    [Fact]
    public void Set_Normalizes_Reversed_Endpoints()
    {
        var state = new L.ChartBrushState();
        state.Set(0, a: 20, b: 10); // reversed on purpose

        var range = state.Get(0);
        Assert.Equal(10, range!.Value.Min);
        Assert.Equal(20, range.Value.Max);
    }

    [Fact]
    public void Clear_Removes_That_Axis_Constraint_Only()
    {
        var state = new L.ChartBrushState();
        state.Set(0, 10, 20);
        state.Set(1, 100, 200);

        state.Clear(0);

        Assert.False(state.HasBrush(0));
        Assert.True(state.HasBrush(1));
        Assert.True(state.Matches(new[] { 0.0, 150.0 })); // axis 0 unconstrained again
    }

    [Fact]
    public void ClearAll_Removes_Every_Brush()
    {
        var state = new L.ChartBrushState();
        state.Set(0, 10, 20);
        state.Set(1, 100, 200);

        state.ClearAll();

        Assert.Empty(state.ActiveAxes);
        Assert.True(state.Matches(new[] { 0.0, 0.0 }));
    }

    [Fact]
    public void Brush_On_An_Axis_Beyond_The_Values_Length_Is_Ignored()
    {
        var state = new L.ChartBrushState();
        state.Set(axisIndex: 5, a: 10, b: 20); // no axis 5 in the 2-value array below

        Assert.True(state.Matches(new[] { 0.0, 0.0 }));
    }
}
