using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Dock;

public class DockTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly BunitJSModuleInterop _motionModule;

    public DockTests()
    {
        _ctx.AddLumeoServices();
        _motionModule = _ctx.JSInterop.SetupModule("./_content/Lumeo.Motion/js/motion.js");
        _motionModule.Mode = JSRuntimeMode.Loose;
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

    // ── #328: magnify wiring (keyboard/touch/reduced-motion handled in JS) ────

    [Fact]
    public void Registers_Magnify_With_Dock_Element()
    {
        var cut = _ctx.Render<Lumeo.Dock>();
        var id = cut.Find("div").GetAttribute("id");

        var invoke = _motionModule.VerifyInvoke("motion.dock");
        Assert.Equal(id, invoke.Arguments[0]);
    }

    [Fact]
    public void Magnify_Options_Carry_MaxScale_And_Radius()
    {
        var cut = _ctx.Render<Lumeo.Dock>(p => p
            .Add(d => d.MaxScale, 2.0)
            .Add(d => d.MagnifyRadius, 120));

        var invoke = _motionModule.VerifyInvoke("motion.dock");
        // The options bag is an anonymous object — assert via reflection.
        var options = invoke.Arguments[1]!;
        var type = options.GetType();
        Assert.Equal(2.0, (double)type.GetProperty("maxScale")!.GetValue(options)!);
        Assert.Equal(120, (int)type.GetProperty("magnifyRadius")!.GetValue(options)!);
    }

    [Fact]
    public void Children_Remain_In_Dom_For_Keyboard_Focus()
    {
        // The JS focus-magnify keys off focusin on real focusable children; the
        // component renders them verbatim so a consumer's <button>/<a> stay
        // tabbable.
        var cut = _ctx.Render<Lumeo.Dock>(p => p
            .AddChildContent("<button id='dock-app-1'>One</button><button id='dock-app-2'>Two</button>"));

        Assert.Equal(2, cut.FindAll("button").Count);
    }
}
