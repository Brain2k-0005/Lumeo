using Bunit;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using L = Lumeo;

namespace Lumeo.Tests.Components.PullToRefresh;

/// <summary>
/// #308 — touch-action: pan-y let the browser consume a downward pull at the
/// top as native overscroll. The fix registers a non-passive touchmove guard
/// (registerPullToRefresh) that preventDefaults the top-pull case. bUnit can't
/// drive real touch, so these assert the interop registration contract via the
/// tracking fake, plus the rendered structure.
/// </summary>
public class PullToRefreshTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public PullToRefreshTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddScoped<IComponentInteropService>(_ => _interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.PullToRefresh> Render() =>
        _ctx.Render<L.PullToRefresh>(p => p.AddChildContent("<p>content</p>"));

    [Fact]
    public void Registers_Gesture_Guard_On_First_Render()
    {
        var cut = Render();
        Assert.Single(_interop.PullToRefreshRegistrations);
    }

    [Fact]
    public async Task Unregisters_Gesture_Guard_On_Dispose()
    {
        var cut = Render();
        var id = _interop.PullToRefreshRegistrations[0];
        await cut.Instance.DisposeAsync();
        Assert.Contains(id, _interop.PullToRefreshUnregistrations);
    }

    [Fact]
    public void Container_Renders_Child_Content()
    {
        var cut = Render();
        Assert.Contains("content", cut.Markup);
    }

    [Fact]
    public void Container_Keeps_PanY_TouchAction()
    {
        // pan-y is retained so normal scroll stays native; the JS guard handles
        // only the downward-at-top case.
        var cut = Render();
        Assert.Contains("touch-action: pan-y", cut.Markup);
    }
}
