using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Globe;

public class GlobeTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public GlobeTests()
    {
        _ctx.AddLumeoServices();
        var motionModule = _ctx.JSInterop.SetupModule("./_content/Lumeo.Motion/js/motion.js");
        motionModule.Mode = JSRuntimeMode.Loose;
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Div_With_Globe_Class()
    {
        var cut = _ctx.Render<Lumeo.Globe>();

        Assert.Contains("lumeo-globe", cut.Find("div").GetAttribute("class"));
    }

    [Fact]
    public void Renders_Canvas_Element()
    {
        var cut = _ctx.Render<Lumeo.Globe>();

        Assert.NotNull(cut.Find("canvas"));
    }

    [Fact]
    public void Size_Param_Sets_Canvas_Dimensions()
    {
        var cut = _ctx.Render<Lumeo.Globe>(p => p
            .Add(g => g.Size, 400));

        var canvas = cut.Find("canvas");
        Assert.Equal("400", canvas.GetAttribute("width"));
        Assert.Equal("400", canvas.GetAttribute("height"));
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.Globe>(p => p
            .Add(g => g.Class, "mx-auto"));

        Assert.Contains("mx-auto", cut.Find("div").GetAttribute("class"));
    }
}
