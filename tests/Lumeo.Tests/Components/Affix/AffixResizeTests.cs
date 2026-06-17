using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Lumeo.Tests.Components.Affix;

/// <summary>
/// #248 — the affixed element's width was frozen at first stick and went stale
/// on resize/rotate. The recompute lives in components.js (resync from the
/// in-flow placeholder on the window 'resize' event), which bUnit can't drive,
/// so these tests guard the C# wiring that keeps that path reachable: the
/// component must register the affix with its offsets and tear it down on
/// dispose.
/// </summary>
public class AffixResizeTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public AffixResizeTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Registers_Affix_With_Offsets_On_First_Render()
    {
        _ctx.Render<Lumeo.Affix>(p => p
            .Add(a => a.OffsetTop, 24)
            .AddChildContent("Sticky"));

        var reg = Assert.Single(_interop.AffixRegistrations);
        Assert.Equal(24, reg.OffsetTop);
        Assert.StartsWith("affix-", reg.ElementId);
    }

    [Fact]
    public void Registers_OffsetBottom_When_Provided()
    {
        _ctx.Render<Lumeo.Affix>(p => p
            .Add(a => a.OffsetBottom, 16)
            .AddChildContent("Sticky"));

        var reg = Assert.Single(_interop.AffixRegistrations);
        Assert.Equal(16, reg.OffsetBottom);
    }

    [Fact]
    public void Unregisters_Affix_On_Dispose()
    {
        var cut = _ctx.Render<Lumeo.Affix>(p => p
            .AddChildContent("Sticky"));
        var id = cut.Find("div").GetAttribute("id");

        cut.Instance.DisposeAsync().AsTask().Wait();

        Assert.Contains(id, _interop.AffixUnregistrations);
    }
}
