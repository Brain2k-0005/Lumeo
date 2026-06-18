using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Confetti;

public class ConfettiTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly BunitJSModuleInterop _motionModule;

    public ConfettiTests()
    {
        _ctx.AddLumeoServices();
        _motionModule = _ctx.JSInterop.SetupModule("./_content/Lumeo.Motion/js/motion.js");
        _motionModule.Mode = JSRuntimeMode.Loose;
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

    // ── #327: reduced-motion gate + scoped canvas ────────────────────────────

    [Fact]
    public async Task Fire_Emits_Burst_When_Motion_Allowed()
    {
        // Loose mode → motion.prefersReducedMotion resolves to false (default).
        var cut = _ctx.Render<Lumeo.Confetti>();

        await cut.Instance.Fire();

        // The burst was requested.
        _motionModule.VerifyInvoke("motion.confettiFire");
    }

    [Fact]
    public async Task Fire_NoOps_Under_Reduced_Motion()
    {
        _motionModule.Setup<bool>("motion.prefersReducedMotion").SetResult(true);
        var cut = _ctx.Render<Lumeo.Confetti>();

        await cut.Instance.Fire();

        // No burst should be requested when the user prefers reduced motion.
        Assert.DoesNotContain(_motionModule.Invocations, i => i.Identifier == "motion.confettiFire");
    }

    [Fact]
    public void Canvas_Is_Not_A_Fixed_Viewport_Overlay_By_Default()
    {
        // The canvas must not pre-declare a viewport-filling fixed overlay in
        // markup (the old hijack). Positioning is applied by confettiFire() at
        // burst time, scoped to the host element.
        var cut = _ctx.Render<Lumeo.Confetti>();

        var style = cut.Find("canvas").GetAttribute("style") ?? string.Empty;
        Assert.DoesNotContain("100vw", style);
        Assert.DoesNotContain("9999", style);
    }
}
