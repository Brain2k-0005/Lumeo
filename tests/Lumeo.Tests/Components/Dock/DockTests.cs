using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Dock;

public class DockTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DockTests()
    {
        _ctx.AddLumeoServices();
        var motionModule = _ctx.JSInterop.SetupModule("./_content/Lumeo.Motion/js/motion.js");
        motionModule.Mode = JSRuntimeMode.Loose;
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Div_With_Dock_Class()
    {
        var cut = _ctx.Render<Lumeo.Dock>();

        Assert.Contains("lumeo-dock", cut.Find("div").GetAttribute("class"));
    }

    [Fact]
    public void Renders_ChildContent()
    {
        var cut = _ctx.Render<Lumeo.Dock>(p => p
            .AddChildContent("<button>App</button>"));

        Assert.NotNull(cut.Find("button"));
    }

    [Fact]
    public void Has_Toolbar_Role()
    {
        var cut = _ctx.Render<Lumeo.Dock>();

        Assert.Equal("toolbar", cut.Find("div").GetAttribute("role"));
    }

    [Fact]
    public void Custom_AriaLabel_Is_Applied()
    {
        var cut = _ctx.Render<Lumeo.Dock>(p => p
            .Add(d => d.AriaLabel, "Custom Dock"));

        Assert.Equal("Custom Dock", cut.Find("div").GetAttribute("aria-label"));
    }
}
