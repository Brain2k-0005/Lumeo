using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Confetti;

public class ConfettiTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ConfettiTests()
    {
        _ctx.AddLumeoServices();
        var motionModule = _ctx.JSInterop.SetupModule("./_content/Lumeo.Motion/js/motion.js");
        motionModule.Mode = JSRuntimeMode.Loose;
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Div_With_Confetti_Class()
    {
        var cut = _ctx.Render<Lumeo.Confetti>();

        Assert.Contains("lumeo-confetti", cut.Find("div").GetAttribute("class"));
    }

    [Fact]
    public void Renders_Canvas_Element()
    {
        var cut = _ctx.Render<Lumeo.Confetti>();

        Assert.NotNull(cut.Find("canvas"));
    }

    [Fact]
    public void Canvas_Has_Confetti_Canvas_Class()
    {
        var cut = _ctx.Render<Lumeo.Confetti>();

        Assert.Contains("lumeo-confetti-canvas", cut.Find("canvas").GetAttribute("class"));
    }

    [Fact]
    public void Renders_ChildContent()
    {
        var cut = _ctx.Render<Lumeo.Confetti>(p => p
            .AddChildContent("<button>Celebrate!</button>"));

        Assert.NotNull(cut.Find("button"));
    }
}
