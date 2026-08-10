using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Charts.Interaction;

public class ChartHoverStateTests
{
    [Fact]
    public void First_Set_Returns_True_And_Activates_State()
    {
        var state = new L.ChartHoverState();
        var changed = state.TrySet(seriesIndex: 0, dataIndex: 5, pointerX: 10, pointerY: 20);

        Assert.True(changed);
        Assert.True(state.IsActive);
        Assert.Equal(5, state.DataIndex);
        Assert.Equal(0, state.SeriesIndex);
    }

    [Fact]
    public void Setting_The_Same_Index_Again_Returns_False_No_Change()
    {
        var state = new L.ChartHoverState();
        state.TrySet(0, 5, 10, 20);

        var changed = state.TrySet(0, 5, 11, 21); // pointer moved slightly, index unchanged

        Assert.False(changed);
    }

    [Fact]
    public void Pointer_Coordinates_Still_Update_Even_When_Index_Is_Unchanged()
    {
        var state = new L.ChartHoverState();
        state.TrySet(0, 5, 10, 20);
        state.TrySet(0, 5, 99, 88);

        Assert.Equal(99, state.PointerX);
        Assert.Equal(88, state.PointerY);
    }

    [Fact]
    public void Changing_DataIndex_Returns_True()
    {
        var state = new L.ChartHoverState();
        state.TrySet(0, 5, 10, 20);

        var changed = state.TrySet(0, 6, 10, 20);

        Assert.True(changed);
        Assert.Equal(6, state.DataIndex);
    }

    [Fact]
    public void Clear_Deactivates_And_Returns_True_When_Something_Was_Active()
    {
        var state = new L.ChartHoverState();
        state.TrySet(0, 5, 10, 20);

        var changed = state.Clear();

        Assert.True(changed);
        Assert.False(state.IsActive);
        Assert.Null(state.DataIndex);
    }

    [Fact]
    public void Clear_On_An_Already_Inactive_State_Returns_False()
    {
        var state = new L.ChartHoverState();
        Assert.False(state.Clear());
    }
}
