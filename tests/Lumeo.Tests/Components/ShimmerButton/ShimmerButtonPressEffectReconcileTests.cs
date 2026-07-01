using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Lumeo.Tests.Components.ShimmerButton;

/// <summary>
/// Triage #18 (medium, state-on-data-change) — the ripple pointerdown listener was
/// attached only inside <c>if (firstRender &amp;&amp; PressEffect == Ripple)</c>, with no
/// per-render reconciliation. So a runtime <see cref="Lumeo.Button.ButtonPressEffect"/>
/// <c>None -&gt; Ripple</c> flip never attached the listener and a <c>Ripple -&gt; None</c>
/// flip left the leaked listener bound. The fix mirrors Button.razor: reconcile the
/// attach/detach against the live <c>PressEffect</c> on every render.
///
/// bUnit can't observe the real DOM listener, so the testable seam is the .NET wiring.
/// These tests re-render via <c>cut.Render(p =&gt; p.Add(...))</c> and assert on the
/// recorded RippleAttach/RippleDetach calls captured by
/// <see cref="TrackingInteropService"/>.
/// </summary>
public class ShimmerButtonPressEffectReconcileTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public ShimmerButtonPressEffectReconcileTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void None_To_Ripple_After_First_Render_Attaches_The_Listener()
    {
        // Mounted without a ripple — nothing attached yet.
        var cut = _ctx.Render<Lumeo.ShimmerButton>(p => p
            .Add(b => b.PressEffect, Lumeo.Button.ButtonPressEffect.None)
            .AddChildContent("Go"));

        Assert.Equal(0, _interop.RippleAttachCallCount);

        // Consumer switches the effect on at runtime — the listener must now bind.
        cut.Render(p => p
            .Add(b => b.PressEffect, Lumeo.Button.ButtonPressEffect.Ripple)
            .AddChildContent("Go"));

        Assert.Equal(1, _interop.RippleAttachCallCount);
    }

    [Fact]
    public void Ripple_To_None_After_First_Render_Detaches_The_Listener()
    {
        // Mounted with a ripple — attached once on first render.
        var cut = _ctx.Render<Lumeo.ShimmerButton>(p => p
            .Add(b => b.PressEffect, Lumeo.Button.ButtonPressEffect.Ripple)
            .AddChildContent("Go"));

        Assert.Equal(1, _interop.RippleAttachCallCount);
        Assert.Equal(0, _interop.RippleDetachCallCount);

        // Consumer turns the effect off — the now-orphaned listener must be detached.
        cut.Render(p => p
            .Add(b => b.PressEffect, Lumeo.Button.ButtonPressEffect.None)
            .AddChildContent("Go"));

        Assert.Equal(1, _interop.RippleDetachCallCount);
    }

    [Fact]
    public void Unrelated_Re_Render_With_Same_Ripple_Effect_Does_Not_Re_Attach()
    {
        var cut = _ctx.Render<Lumeo.ShimmerButton>(p => p
            .Add(b => b.PressEffect, Lumeo.Button.ButtonPressEffect.Ripple)
            .AddChildContent("Go"));

        Assert.Equal(1, _interop.RippleAttachCallCount);

        // A parent re-render with the IDENTICAL effect must not churn the listener.
        cut.Render(p => p
            .Add(b => b.PressEffect, Lumeo.Button.ButtonPressEffect.Ripple)
            .AddChildContent("Go"));

        Assert.Equal(1, _interop.RippleAttachCallCount);
        Assert.Equal(0, _interop.RippleDetachCallCount);
    }

    [Fact]
    public async Task Disposing_A_Ripple_ShimmerButton_Detaches_The_Listener()
    {
        var cut = _ctx.Render<Lumeo.ShimmerButton>(p => p
            .Add(b => b.PressEffect, Lumeo.Button.ButtonPressEffect.Ripple)
            .AddChildContent("Go"));

        Assert.Equal(1, _interop.RippleAttachCallCount);
        Assert.Equal(0, _interop.RippleDetachCallCount);

        // Tearing the component down must release the listener.
        await cut.Instance.DisposeAsync();

        Assert.Equal(1, _interop.RippleDetachCallCount);
    }
}
