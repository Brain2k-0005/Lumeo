using System.Globalization;
using System.Linq;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.AnimatedBeam;

/// <summary>
/// Battle-test wave 3 regression coverage for AnimatedBeam.
///
/// • #wave3-1 (lifecycle): registration was latched on <c>firstRender</c>, so a
///   beam whose FromId/ToId arrive on a later render never started. The fix drops
///   the firstRender gate and relies on the <c>_registered</c> latch alone.
/// • #wave3-27 (state-on-data-change): the JS-affecting params (Curvature, Reverse,
///   Duration/Delay, From/To, Container) were read once into a JS closure and never
///   re-applied. The fix disposes then re-registers when any of them change.
/// • #wave3-28 (edge-data): PathWidth/BeamWidth were interpolated as raw doubles,
///   producing invalid SVG stroke-width="1,5" under comma-decimal cultures. The fix
///   formats them with InvariantCulture like PathOpacity already did.
///
/// The lifecycle/state mechanisms are asserted via the recorded motion.js module
/// invocations (arg order for motion.animatedBeam: [0]=svgId, [1]=fromId,
/// [2]=toId, [3]=options).
/// </summary>
public class AnimatedBeamRegressionTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly BunitJSModuleInterop _motionModule;

    public AnimatedBeamRegressionTests()
    {
        _ctx.AddLumeoServices();
        _motionModule = _ctx.JSInterop.SetupModule("./_content/Lumeo.Motion/js/motion.js");
        _motionModule.Mode = JSRuntimeMode.Loose;
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // ── #wave3-1 ──────────────────────────────────────────────────────────────
    [Fact]
    public void Registers_When_FromId_ToId_Arrive_After_First_Render()
    {
        // First render with no ids — nothing to anchor to, so nothing registers.
        var cut = _ctx.Render<Lumeo.AnimatedBeam>();
        Assert.DoesNotContain(
            _motionModule.Invocations,
            i => i.Identifier == "motion.animatedBeam");

        // The ids arrive on a later render (e.g. the parent measured its nodes).
        cut.Render(p => p
            .Add(c => c.FromId, "from-el")
            .Add(c => c.ToId, "to-el"));

        // Without the fix the firstRender gate has already passed on render #1, so
        // the beam never registers. With the fix the _registered latch alone gates
        // it, so OnAfterRenderAsync picks the ids up as soon as they are present.
        Assert.Contains(
            _motionModule.Invocations,
            i => i.Identifier == "motion.animatedBeam");
    }

    // ── #wave3-27 ─────────────────────────────────────────────────────────────
    [Fact]
    public void Changing_Curvature_ReApplies_The_Beam_With_New_Options()
    {
        var cut = _ctx.Render<Lumeo.AnimatedBeam>(p => p
            .Add(c => c.FromId, "from-el")
            .Add(c => c.ToId, "to-el")
            .Add(c => c.Curvature, 0.0));

        Assert.Equal(
            1,
            _motionModule.Invocations.Count(i => i.Identifier == "motion.animatedBeam"));

        // A runtime change to a JS-affecting parameter.
        cut.Render(p => p.Add(c => c.Curvature, 50.0));

        // Without the fix the one-shot latch never re-applies, so the JS closure
        // keeps the original curvature forever. The fix tears down the prior
        // observer then re-registers with the new options.
        Assert.Equal(
            2,
            _motionModule.Invocations.Count(i => i.Identifier == "motion.animatedBeam"));
        Assert.Contains(
            _motionModule.Invocations,
            i => i.Identifier == "motion.disposeAnimatedBeam");

        // The re-applied options carry the new curvature (anonymous bag → reflect).
        var last = _motionModule.Invocations.Last(i => i.Identifier == "motion.animatedBeam");
        var options = last.Arguments[3]!;
        var curvature = (double)options.GetType().GetProperty("curvature")!.GetValue(options)!;
        Assert.Equal(50.0, curvature);
    }

    [Fact]
    public void Re_Render_With_Unchanged_Params_Does_Not_Re_Apply()
    {
        var cut = _ctx.Render<Lumeo.AnimatedBeam>(p => p
            .Add(c => c.FromId, "from-el")
            .Add(c => c.ToId, "to-el")
            .Add(c => c.Curvature, 10.0));

        var afterFirst = _motionModule.Invocations.Count(i => i.Identifier == "motion.animatedBeam");

        // An unrelated parent re-render that does not change a JS-affecting value.
        cut.Render(p => p.Add(c => c.Curvature, 10.0));

        var afterReRender = _motionModule.Invocations.Count(i => i.Identifier == "motion.animatedBeam");

        // Same-value re-renders must not churn the JS observer.
        Assert.Equal(afterFirst, afterReRender);
    }

    // ── #wave3-28 ─────────────────────────────────────────────────────────────
    [Fact]
    public void StrokeWidths_Use_Invariant_Decimal_Separator_On_Comma_Cultures()
    {
        // de-DE CurrentCulture rendered stroke-width="1,5" / "2,5" — invalid SVG
        // the renderer ignores, leaving the track/beam at the default 1px hairline.
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            var cut = _ctx.Render<Lumeo.AnimatedBeam>(p => p
                .Add(c => c.PathWidth, 1.5)
                .Add(c => c.BeamWidth, 2.5));

            var paths = cut.FindAll("path");
            Assert.Equal("1.5", paths[0].GetAttribute("stroke-width")); // track
            Assert.Equal("2.5", paths[1].GetAttribute("stroke-width")); // beam
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
