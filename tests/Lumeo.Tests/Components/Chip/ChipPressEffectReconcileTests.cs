using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Chip;

/// <summary>
/// Triage wave-3 #33 (low, state-on-data-change) — the Chip ripple pointerdown
/// listener was attached only inside <c>if (firstRender &amp;&amp; Clickable &amp;&amp;
/// PressEffect == Ripple)</c>, with no per-render reconciliation and no dispose.
/// So a runtime <c>None -&gt; Ripple</c> flip (or a <c>Clickable</c> flip) after the
/// first render never attached the listener, a <c>Ripple -&gt; None</c> flip left a
/// leaked listener bound, and tearing the chip down never detached it.
///
/// The fix copies Button.razor's proven reconcile: attach/detach is reconciled
/// against the live <c>Clickable</c>/<c>PressEffect</c> on EVERY render via an
/// <c>_rippleAttached</c> flag, and torn down in <c>DisposeAsync</c>. bUnit can't
/// observe the real DOM listener, so the testable seam is the .NET wiring —
/// these tests re-render via <c>cut.Render(p =&gt; p.Add(...))</c> and assert on the
/// recorded RippleAttach/RippleDetach calls captured by
/// <see cref="TrackingInteropService"/>.
/// </summary>
public class ChipPressEffectReconcileTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public ChipPressEffectReconcileTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void None_To_Ripple_After_First_Render_Attaches_The_Listener()
    {
        // Mounted clickable but without a ripple — nothing attached yet.
        var cut = _ctx.Render<L.Chip>(p => p
            .Add(c => c.Clickable, true)
            .Add(c => c.PressEffect, L.Button.ButtonPressEffect.None)
            .AddChildContent("Tag"));

        Assert.Equal(0, _interop.RippleAttachCallCount);

        // Consumer switches the effect on at runtime — the listener must now bind.
        cut.Render(p => p
            .Add(c => c.Clickable, true)
            .Add(c => c.PressEffect, L.Button.ButtonPressEffect.Ripple)
            .AddChildContent("Tag"));

        Assert.Equal(1, _interop.RippleAttachCallCount);
    }

    [Fact]
    public void Ripple_To_None_After_First_Render_Detaches_The_Listener()
    {
        // Mounted clickable + ripple — attached once on first render.
        var cut = _ctx.Render<L.Chip>(p => p
            .Add(c => c.Clickable, true)
            .Add(c => c.PressEffect, L.Button.ButtonPressEffect.Ripple)
            .AddChildContent("Tag"));

        Assert.Equal(1, _interop.RippleAttachCallCount);
        Assert.Equal(0, _interop.RippleDetachCallCount);

        // Consumer turns the effect off — the now-orphaned listener must detach.
        cut.Render(p => p
            .Add(c => c.Clickable, true)
            .Add(c => c.PressEffect, L.Button.ButtonPressEffect.None)
            .AddChildContent("Tag"));

        Assert.Equal(1, _interop.RippleDetachCallCount);
    }

    [Fact]
    public void Clickable_Flip_While_Ripple_Set_Attaches_The_Listener()
    {
        // Ripple requested but NOT clickable -> no ripple surface, nothing bound.
        var cut = _ctx.Render<L.Chip>(p => p
            .Add(c => c.Clickable, false)
            .Add(c => c.PressEffect, L.Button.ButtonPressEffect.Ripple)
            .AddChildContent("Tag"));

        Assert.Equal(0, _interop.RippleAttachCallCount);

        // Becoming clickable at runtime must now bind the ripple listener.
        cut.Render(p => p
            .Add(c => c.Clickable, true)
            .Add(c => c.PressEffect, L.Button.ButtonPressEffect.Ripple)
            .AddChildContent("Tag"));

        Assert.Equal(1, _interop.RippleAttachCallCount);
    }

    [Fact]
    public void Unrelated_Re_Render_With_Same_Ripple_Effect_Does_Not_Re_Attach()
    {
        var cut = _ctx.Render<L.Chip>(p => p
            .Add(c => c.Clickable, true)
            .Add(c => c.PressEffect, L.Button.ButtonPressEffect.Ripple)
            .AddChildContent("Tag"));

        Assert.Equal(1, _interop.RippleAttachCallCount);

        // A re-render with the IDENTICAL effect must not churn the listener.
        cut.Render(p => p
            .Add(c => c.Clickable, true)
            .Add(c => c.PressEffect, L.Button.ButtonPressEffect.Ripple)
            .AddChildContent("Tag"));

        Assert.Equal(1, _interop.RippleAttachCallCount);
        Assert.Equal(0, _interop.RippleDetachCallCount);
    }

    [Fact]
    public async Task Disposing_A_Ripple_Chip_Detaches_The_Listener()
    {
        var cut = _ctx.Render<L.Chip>(p => p
            .Add(c => c.Clickable, true)
            .Add(c => c.PressEffect, L.Button.ButtonPressEffect.Ripple)
            .AddChildContent("Tag"));

        Assert.Equal(1, _interop.RippleAttachCallCount);
        Assert.Equal(0, _interop.RippleDetachCallCount);

        // Tearing the component down must release the listener (no leak).
        await cut.Instance.DisposeAsync();

        Assert.Equal(1, _interop.RippleDetachCallCount);
    }
}
