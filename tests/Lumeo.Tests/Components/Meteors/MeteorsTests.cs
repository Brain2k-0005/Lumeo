using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Meteors;

public class MeteorsTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public MeteorsTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Root_Div_With_Meteors_Class()
    {
        var cut = _ctx.Render<Lumeo.Meteors>();

        Assert.Contains("lumeo-meteors", cut.Find("div").GetAttribute("class"));
    }

    [Fact]
    public void Renders_Default_20_Meteor_Spans()
    {
        var cut = _ctx.Render<Lumeo.Meteors>();

        Assert.Equal(20, cut.FindAll(".lumeo-meteor").Count);
    }

    [Fact]
    public void Number_Param_Controls_Meteor_Count()
    {
        var cut = _ctx.Render<Lumeo.Meteors>(p => p
            .Add(m => m.Number, 5));

        Assert.Equal(5, cut.FindAll(".lumeo-meteor").Count);
    }

    [Fact]
    public void Custom_Color_Sets_CSS_Variable()
    {
        var cut = _ctx.Render<Lumeo.Meteors>(p => p
            .Add(m => m.Color, "oklch(0.7 0.2 30)"));

        // CSS variable syntax may or may not include space after colon depending on Blazor's renderer
        Assert.Contains("--lumeo-meteor-color:oklch(0.7 0.2 30)", cut.Find("div").GetAttribute("style"));
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.Meteors>(p => p
            .Add(m => m.Class, "overflow-hidden"));

        Assert.Contains("overflow-hidden", cut.Find("div").GetAttribute("class"));
    }
}
