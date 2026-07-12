using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.NumberInput;

/// <summary>
/// Keyboard coverage for NumberInput's spinner key handler (HandleKeyDown on
/// the native &lt;input type="number"&gt;): ArrowUp increments by Step,
/// ArrowDown decrements by Step, both clamp at Min/Max exactly like the
/// stepper buttons, and both are inert while Disabled.
/// </summary>
public class NumberInputKeyboardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public NumberInputKeyboardTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void ArrowUp_Increments_By_Step()
    {
        double? committed = null;
        var cut = _ctx.Render<Lumeo.NumberInput>(p => p
            .Add(n => n.Value, 5.0)
            .Add(n => n.Step, 2.0)
            .Add(n => n.ValueChanged, v => committed = v));

        cut.Find("input").KeyDown("ArrowUp");

        Assert.Equal(7.0, committed);
    }

    [Fact]
    public void ArrowDown_Decrements_By_Step()
    {
        double? committed = null;
        var cut = _ctx.Render<Lumeo.NumberInput>(p => p
            .Add(n => n.Value, 5.0)
            .Add(n => n.Step, 2.0)
            .Add(n => n.ValueChanged, v => committed = v));

        cut.Find("input").KeyDown("ArrowDown");

        Assert.Equal(3.0, committed);
    }

    [Fact]
    public void ArrowUp_Clamps_At_Max()
    {
        double? committed = null;
        var cut = _ctx.Render<Lumeo.NumberInput>(p => p
            .Add(n => n.Value, 9.0)
            .Add(n => n.Step, 5.0)
            .Add(n => n.Max, 10.0)
            .Add(n => n.ValueChanged, v => committed = v));

        cut.Find("input").KeyDown("ArrowUp");

        Assert.Equal(10.0, committed);
    }

    [Fact]
    public void ArrowDown_Clamps_At_Min()
    {
        double? committed = null;
        var cut = _ctx.Render<Lumeo.NumberInput>(p => p
            .Add(n => n.Value, 1.0)
            .Add(n => n.Step, 5.0)
            .Add(n => n.Min, 0.0)
            .Add(n => n.ValueChanged, v => committed = v));

        cut.Find("input").KeyDown("ArrowDown");

        Assert.Equal(0.0, committed);
    }

    [Fact]
    public void Disabled_Input_Ignores_ArrowUp()
    {
        var valueChangedCount = 0;
        var cut = _ctx.Render<Lumeo.NumberInput>(p => p
            .Add(n => n.Value, 5.0)
            .Add(n => n.Disabled, true)
            .Add(n => n.ValueChanged, _ => valueChangedCount++));

        cut.Find("input").KeyDown("ArrowUp");

        Assert.Equal(0, valueChangedCount);
    }

    [Fact]
    public void Disabled_Input_Ignores_ArrowDown()
    {
        var valueChangedCount = 0;
        var cut = _ctx.Render<Lumeo.NumberInput>(p => p
            .Add(n => n.Value, 5.0)
            .Add(n => n.Disabled, true)
            .Add(n => n.ValueChanged, _ => valueChangedCount++));

        cut.Find("input").KeyDown("ArrowDown");

        Assert.Equal(0, valueChangedCount);
    }

    [Fact]
    public void Unhandled_Key_Does_Not_Change_The_Value()
    {
        var valueChangedCount = 0;
        var cut = _ctx.Render<Lumeo.NumberInput>(p => p
            .Add(n => n.Value, 5.0)
            .Add(n => n.ValueChanged, _ => valueChangedCount++));

        cut.Find("input").KeyDown("a");

        Assert.Equal(0, valueChangedCount);
    }
}
