using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Toggle;

/// <summary>
/// shadcn-parity Wave 2: data-state="on|off" + data-disabled on <see cref="Lumeo.Toggle"/>
/// so consumers can target data-[state=on] in CSS (visuals were C#-only before).
/// </summary>
public class ToggleDataStateTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ToggleDataStateTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void DataState_Off_By_Default()
    {
        var cut = _ctx.Render<Lumeo.Toggle>(p => p.AddChildContent("B"));
        Assert.Equal("off", cut.Find("button").GetAttribute("data-state"));
    }

    [Fact]
    public void DataState_On_When_Pressed()
    {
        var cut = _ctx.Render<Lumeo.Toggle>(p => p
            .Add(t => t.Pressed, true)
            .AddChildContent("B"));
        Assert.Equal("on", cut.Find("button").GetAttribute("data-state"));
    }

    [Fact]
    public void DataState_Flips_On_Click()
    {
        var cut = _ctx.Render<Lumeo.Toggle>(p => p
            .Add(t => t.Pressed, false)
            .AddChildContent("B"));
        cut.Find("button").Click();
        Assert.Equal("on", cut.Find("button").GetAttribute("data-state"));
    }

    [Fact]
    public void DataDisabled_Present_Only_When_Disabled()
    {
        var enabled = _ctx.Render<Lumeo.Toggle>(p => p.AddChildContent("B"));
        Assert.False(enabled.Find("button").HasAttribute("data-disabled"));

        var disabled = _ctx.Render<Lumeo.Toggle>(p => p
            .Add(t => t.Disabled, true)
            .AddChildContent("B"));
        Assert.True(disabled.Find("button").HasAttribute("data-disabled"));
    }
}
