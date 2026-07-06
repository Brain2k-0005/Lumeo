using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Slider;

/// <summary>
/// shadcn-parity Wave 2: data-orientation + data-disabled styling hooks on the
/// slider root/track/range/thumb for <see cref="Lumeo.Slider"/>.
/// </summary>
public class SliderDataStateTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SliderDataStateTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Root_DataOrientation_Horizontal_By_Default()
    {
        var cut = _ctx.Render<Lumeo.Slider>();
        Assert.Equal("horizontal", cut.Find("div[data-orientation]").GetAttribute("data-orientation"));
    }

    [Fact]
    public void Root_DataOrientation_Vertical_When_Vertical()
    {
        var cut = _ctx.Render<Lumeo.Slider>(p => p.Add(s => s.Orientation, "Vertical"));
        Assert.Equal("vertical", cut.Find("div[data-orientation]").GetAttribute("data-orientation"));
    }

    [Fact]
    public void Thumb_Input_Carries_DataOrientation()
    {
        var cut = _ctx.Render<Lumeo.Slider>();
        Assert.Equal("horizontal", cut.Find("input[type=range]").GetAttribute("data-orientation"));
    }

    [Fact]
    public void DataDisabled_Present_Only_When_Disabled()
    {
        var enabled = _ctx.Render<Lumeo.Slider>();
        Assert.False(enabled.Find("input[type=range]").HasAttribute("data-disabled"));

        var disabled = _ctx.Render<Lumeo.Slider>(p => p.Add(s => s.Disabled, true));
        Assert.True(disabled.Find("input[type=range]").HasAttribute("data-disabled"));
    }
}
