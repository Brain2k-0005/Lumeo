using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.MagneticButton;

public class MagneticButtonTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public MagneticButtonTests()
    {
        _ctx.AddLumeoServices();
        var motionModule = _ctx.JSInterop.SetupModule("./_content/Lumeo.Motion/js/motion.js");
        motionModule.Mode = JSRuntimeMode.Loose;
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Div_With_MagneticButton_Class()
    {
        var cut = _ctx.Render<Lumeo.MagneticButton>();

        Assert.Contains("lumeo-magnetic-button", cut.Find("div").GetAttribute("class"));
    }

    [Fact]
    public void Renders_ChildContent()
    {
        var cut = _ctx.Render<Lumeo.MagneticButton>(p => p
            .AddChildContent("<button>Click me</button>"));

        Assert.NotNull(cut.Find("button"));
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.MagneticButton>(p => p
            .Add(m => m.Class, "my-mag"));

        Assert.Contains("my-mag", cut.Find("div").GetAttribute("class"));
    }
}
