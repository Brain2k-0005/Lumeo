using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Charts.Interaction;

public class ChartZoomStateTests
{
    [Fact]
    public void Default_Range_Is_The_Full_Domain()
    {
        var state = new L.ChartZoomState();
        Assert.Equal(0, state.Start);
        Assert.Equal(1, state.End);
        Assert.Equal(1, state.Span);
    }

    [Fact]
    public void SetRange_Clamps_To_Zero_One()
    {
        var state = new L.ChartZoomState();
        state.SetRange(-0.5, 1.5);

        Assert.Equal(0, state.Start);
        Assert.Equal(1, state.End);
    }

    [Fact]
    public void SetRange_Normalizes_Reversed_Endpoints()
    {
        var state = new L.ChartZoomState();
        state.SetRange(0.8, 0.2);

        Assert.Equal(0.2, state.Start);
        Assert.Equal(0.8, state.End);
    }

    [Fact]
    public void ZoomAt_Center_Shrinks_The_Window_Symmetrically()
    {
        var state = new L.ChartZoomState();
        state.ZoomAt(pivot: 0.5, factor: 2);

        Assert.Equal(0.5, state.Span, precision: 6);
        Assert.Equal(0.25, state.Start, precision: 6);
        Assert.Equal(0.75, state.End, precision: 6);
    }

    [Fact]
    public void ZoomAt_Respects_Minimum_Span()
    {
        var state = new L.ChartZoomState();
        state.ZoomAt(pivot: 0.5, factor: 10_000, minSpan: 0.05);

        Assert.True(state.Span >= 0.05 - 1e-9);
    }

    [Fact]
    public void ZoomOut_Factor_Below_One_Widens_The_Window_And_Clamps_To_Full_Domain()
    {
        var state = new L.ChartZoomState();
        state.SetRange(0.4, 0.6);
        state.ZoomAt(pivot: 0.5, factor: 0.1);

        Assert.Equal(0, state.Start, precision: 6);
        Assert.Equal(1, state.End, precision: 6);
    }

    [Fact]
    public void Pan_Shifts_The_Window_Without_Changing_Its_Span()
    {
        var state = new L.ChartZoomState();
        state.SetRange(0.2, 0.4);
        var span = state.Span;

        state.Pan(0.3);

        Assert.Equal(span, state.Span, precision: 9);
        Assert.Equal(0.5, state.Start, precision: 9);
    }

    [Fact]
    public void Pan_Clamps_At_The_Domain_Edges()
    {
        var state = new L.ChartZoomState();
        state.SetRange(0.8, 1.0);

        state.Pan(0.5); // would push End past 1.0

        Assert.True(state.End <= 1.0001);
        Assert.Equal(0.2, state.Span, precision: 6);
    }

    [Fact]
    public void ToDomain_Maps_Fractions_Onto_Absolute_Bounds()
    {
        var state = new L.ChartZoomState();
        state.SetRange(0.25, 0.75);

        var (min, max) = state.ToDomain(fullMin: 0, fullMax: 400);

        Assert.Equal(100, min);
        Assert.Equal(300, max);
    }

    [Fact]
    public void Reset_Restores_The_Full_Domain()
    {
        var state = new L.ChartZoomState();
        state.SetRange(0.3, 0.4);
        state.Reset();

        Assert.Equal(0, state.Start);
        Assert.Equal(1, state.End);
    }
}
