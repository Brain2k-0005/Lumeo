using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Lumeo.Tests.Components.Affix;

/// <summary>
/// Triage #10 (high, state-on-data-change) — the JS <c>registerAffix</c> closure captures
/// offsetTop/offsetBottom/targetSelector by value, and the component originally latched the
/// registration on first render (<c>if (!firstRender) return; if (_registered) return;</c>).
/// As a result, changing <c>OffsetTop</c>/<c>OffsetBottom</c>/<c>Target</c> after the first
/// render was silently ignored: the sticky boundary kept using the stale, first-render values.
///
/// bUnit can't drive the real scroll watcher, so the testable seam is the .NET wiring: a config
/// change must tear the old registration down (UnregisterAffix) and re-register (RegisterAffix)
/// with the NEW values. These tests re-render via <c>cut.Render(p =&gt; p.Add(...))</c> and assert
/// on the recorded register/unregister calls captured by <see cref="TrackingInteropService"/>.
/// </summary>
public class AffixReconfigureTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public AffixReconfigureTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Changing_OffsetTop_After_First_Render_Re_Registers_With_New_Value()
    {
        var cut = _ctx.Render<Lumeo.Affix>(p => p
            .Add(a => a.OffsetTop, 24)
            .AddChildContent("Sticky"));

        var reg = Assert.Single(_interop.AffixRegistrations);
        Assert.Equal(24, reg.OffsetTop);
        var id = reg.ElementId;

        // Consumer raises the offset at runtime (e.g. a header grew taller).
        cut.Render(p => p
            .Add(a => a.OffsetTop, 80)
            .AddChildContent("Sticky"));

        // The first registration must be torn down...
        Assert.Contains(id, _interop.AffixUnregistrations);
        // ...and a fresh one created with the NEW offset (same element id).
        Assert.Equal(2, _interop.AffixRegistrations.Count);
        var latest = _interop.AffixRegistrations[^1];
        Assert.Equal(id, latest.ElementId);
        Assert.Equal(80, latest.OffsetTop);
    }

    [Fact]
    public void Changing_OffsetBottom_After_First_Render_Re_Registers_With_New_Value()
    {
        var cut = _ctx.Render<Lumeo.Affix>(p => p
            .Add(a => a.OffsetBottom, 16)
            .AddChildContent("Sticky"));

        Assert.Equal(16, Assert.Single(_interop.AffixRegistrations).OffsetBottom);

        cut.Render(p => p
            .Add(a => a.OffsetBottom, 40)
            .AddChildContent("Sticky"));

        Assert.Equal(2, _interop.AffixRegistrations.Count);
        Assert.Equal(40, _interop.AffixRegistrations[^1].OffsetBottom);
    }

    [Fact]
    public void Changing_Target_After_First_Render_Re_Registers_With_New_Selector()
    {
        var cut = _ctx.Render<Lumeo.Affix>(p => p
            .Add(a => a.Target, "#scroller-a")
            .AddChildContent("Sticky"));

        Assert.Equal("#scroller-a", Assert.Single(_interop.AffixRegistrations).Target);

        cut.Render(p => p
            .Add(a => a.Target, "#scroller-b")
            .AddChildContent("Sticky"));

        Assert.Equal(2, _interop.AffixRegistrations.Count);
        Assert.Equal("#scroller-b", _interop.AffixRegistrations[^1].Target);
    }

    [Fact]
    public void Unrelated_Re_Render_With_Same_Config_Does_Not_Re_Register()
    {
        var cut = _ctx.Render<Lumeo.Affix>(p => p
            .Add(a => a.OffsetTop, 24)
            .AddChildContent("Sticky"));

        Assert.Single(_interop.AffixRegistrations);

        // Re-render with the IDENTICAL config (e.g. a parent re-render). The config
        // tuple is unchanged, so there must be no churn: no extra register, no unregister.
        cut.Render(p => p
            .Add(a => a.OffsetTop, 24)
            .AddChildContent("Sticky"));

        Assert.Single(_interop.AffixRegistrations);
        Assert.Empty(_interop.AffixUnregistrations);
    }
}
