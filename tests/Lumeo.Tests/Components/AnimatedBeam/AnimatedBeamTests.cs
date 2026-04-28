using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.AnimatedBeam;

public class AnimatedBeamTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public AnimatedBeamTests()
    {
        _ctx.AddLumeoServices();
        // Motion module is loaded lazily; loose mode handles the import call
        var motionModule = _ctx.JSInterop.SetupModule("./_content/Lumeo.Motion/js/motion.js");
        motionModule.Mode = JSRuntimeMode.Loose;
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Svg_With_AnimatedBeam_Class()
    {
        var cut = _ctx.Render<Lumeo.AnimatedBeam>(p => p
            .Add(c => c.FromId, "from-el")
            .Add(c => c.ToId, "to-el"));

        Assert.Contains("lumeo-animated-beam", cut.Find("svg").GetAttribute("class"));
    }

    [Fact]
    public void Renders_Path_And_Beam_Elements()
    {
        var cut = _ctx.Render<Lumeo.AnimatedBeam>(p => p
            .Add(c => c.FromId, "from-el")
            .Add(c => c.ToId, "to-el"));

        var paths = cut.FindAll("path");
        Assert.Equal(2, paths.Count);
    }

    [Fact]
    public void Custom_Color_Appears_In_LinearGradient_Stop()
    {
        var cut = _ctx.Render<Lumeo.AnimatedBeam>(p => p
            .Add(c => c.FromId, "a")
            .Add(c => c.ToId, "b")
            .Add(c => c.Color, "oklch(0.7 0.2 200)"));

        Assert.Contains("oklch(0.7 0.2 200)", cut.Markup);
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.AnimatedBeam>(p => p
            .Add(c => c.FromId, "a")
            .Add(c => c.ToId, "b")
            .Add(c => c.Class, "my-beam"));

        Assert.Contains("my-beam", cut.Find("svg").GetAttribute("class"));
    }
}
