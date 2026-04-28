using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Ripple;

public class RippleTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public RippleTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Div_With_Ripple_Class()
    {
        var cut = _ctx.Render<Lumeo.Ripple>();

        Assert.Contains("lumeo-ripple", cut.Find("div").GetAttribute("class"));
    }

    [Fact]
    public void Renders_Default_5_Rings()
    {
        var cut = _ctx.Render<Lumeo.Ripple>();

        Assert.Equal(5, cut.FindAll(".lumeo-ripple-ring").Count);
    }

    [Fact]
    public void RingCount_Controls_Number_Of_Rings()
    {
        var cut = _ctx.Render<Lumeo.Ripple>(p => p
            .Add(r => r.RingCount, 3));

        Assert.Equal(3, cut.FindAll(".lumeo-ripple-ring").Count);
    }

    [Fact]
    public void Duration_Sets_CSS_Variable()
    {
        var cut = _ctx.Render<Lumeo.Ripple>(p => p
            .Add(r => r.Duration, "3s"));

        Assert.Contains("--lumeo-ripple-duration: 3s", cut.Find("div").GetAttribute("style"));
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.Ripple>(p => p
            .Add(r => r.Class, "my-ripple"));

        Assert.Contains("my-ripple", cut.Find("div").GetAttribute("class"));
    }
}
