using Bunit;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Lumeo.Tests.Components.PullToRefresh;

/// <summary>
/// Behavior/interop tests for the PullToRefresh gesture wrapper.
///
/// The wrapper IS the scroll container. On first render it installs a
/// non-passive touchmove guard through the interop seam
/// (<c>RegisterPullToRefresh</c>) and tears it down on dispose
/// (<c>UnregisterPullToRefresh</c>) — we spy both via TrackingInteropService and
/// assert the method-name contract by inspecting the recorded element ids.
///
/// The actual refresh is driven by Blazor Pointer Events, NOT a [JSInvokable]
/// JS→.NET callback (the JS side only owns the non-passive touchmove guard).
/// bUnit can't dispatch real touch, so we simulate the gesture by firing
/// pointerdown → pointermove → pointerup on the root element and assert the
/// OnRefresh EventCallback fires only once the visual pull (delta × 0.5) clears
/// ThresholdPx, and that the spinner spins while the async handler runs.
/// </summary>
public class PullToRefreshBehaviorTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public PullToRefreshBehaviorTests()
    {
        _ctx.AddLumeoServices();
        // Last interface registration wins, so PullToRefresh resolves the spy.
        _ctx.Services.AddScoped<IComponentInteropService>(_ => _interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<Lumeo.PullToRefresh> Render(
        EventCallback onRefresh = default,
        double thresholdPx = 80) =>
        _ctx.Render<Lumeo.PullToRefresh>(p => p
            .Add(c => c.OnRefresh, onRefresh)
            .Add(c => c.ThresholdPx, thresholdPx)
            .AddChildContent("<p>content</p>"));

    // --- Interop lifecycle (register on init / unregister on dispose) ---

    [Fact]
    public void Registers_The_TouchmoveGuard_Once_On_FirstRender_For_The_Root_Element()
    {
        var cut = Render();

        // OnAfterRenderAsync wires the guard exactly once.
        cut.WaitForAssertion(() => Assert.Single(_interop.PullToRefreshRegistrations));

        // The registered id is the wrapper's own element id (the scroll container).
        var rootId = cut.Find("div[id]").GetAttribute("id");
        Assert.Equal(rootId, _interop.PullToRefreshRegistrations[0]);
        Assert.StartsWith("ptr", rootId);
    }

    [Fact]
    public async Task Unregisters_The_TouchmoveGuard_On_Dispose_With_The_Same_Element_Id()
    {
        var cut = Render();
        cut.WaitForAssertion(() => Assert.Single(_interop.PullToRefreshRegistrations));
        var rootId = _interop.PullToRefreshRegistrations[0];

        Assert.Empty(_interop.PullToRefreshUnregistrations);

        // Disposing the component must tear the guard down for the same id.
        await cut.Instance.DisposeAsync();

        var unregistered = Assert.Single(_interop.PullToRefreshUnregistrations);
        Assert.Equal(rootId, unregistered);
    }

    // --- Gesture → OnRefresh contract ---

    [Fact]
    public async Task Pull_Past_Threshold_Invokes_OnRefresh_Once()
    {
        var fired = 0;
        var cb = EventCallback.Factory.Create(this, () => fired++);
        var cut = Render(onRefresh: cb, thresholdPx: 80);

        var root = cut.Find("div[id^='ptr']");
        // Visual pull is delta × 0.5; to clear an 80px threshold we need ≥160px.
        root.PointerDown(new PointerEventArgs { PointerId = 1, ClientY = 0 });
        root.PointerMove(new PointerEventArgs { PointerId = 1, ClientY = 200 });
        await root.PointerUpAsync(new PointerEventArgs { PointerId = 1, ClientY = 200 });

        Assert.Equal(1, fired);
    }

    [Fact]
    public async Task Release_Below_Threshold_Does_Not_Invoke_OnRefresh()
    {
        var fired = 0;
        var cb = EventCallback.Factory.Create(this, () => fired++);
        var cut = Render(onRefresh: cb, thresholdPx: 80);

        var root = cut.Find("div[id^='ptr']");
        // 100px finger travel → 50px visual pull → below the 80px threshold.
        root.PointerDown(new PointerEventArgs { PointerId = 1, ClientY = 0 });
        root.PointerMove(new PointerEventArgs { PointerId = 1, ClientY = 100 });
        await root.PointerUpAsync(new PointerEventArgs { PointerId = 1, ClientY = 100 });

        Assert.Equal(0, fired);
    }

    [Fact]
    public async Task Spinner_Spins_While_The_Refresh_Handler_Runs_Then_Settles()
    {
        // A handler we can hold open to observe the in-flight "refreshing" state.
        var gate = new TaskCompletionSource();
        var cb = EventCallback.Factory.Create(this, () => gate.Task);
        var cut = Render(onRefresh: cb, thresholdPx: 80);

        var root = cut.Find("div[id^='ptr']");
        root.PointerDown(new PointerEventArgs { PointerId = 1, ClientY = 0 });
        root.PointerMove(new PointerEventArgs { PointerId = 1, ClientY = 220 });
        // Don't await — the handler is still pending on the gate.
        var pointerUp = root.PointerUpAsync(new PointerEventArgs { PointerId = 1, ClientY = 220 });

        // While the handler is in flight the refresh spinner animates.
        cut.WaitForAssertion(() => Assert.Contains("animate-spin", cut.Markup));

        // Complete the handler; the component settles and the spinner stops.
        gate.SetResult();
        await pointerUp;

        cut.WaitForAssertion(() => Assert.DoesNotContain("animate-spin", cut.Markup));
    }

    [Fact]
    public async Task Upward_Move_Disengages_So_Release_Does_Not_Refresh()
    {
        var fired = 0;
        var cb = EventCallback.Factory.Create(this, () => fired++);
        var cut = Render(onRefresh: cb, thresholdPx: 80);

        var root = cut.Find("div[id^='ptr']");
        root.PointerDown(new PointerEventArgs { PointerId = 1, ClientY = 0 });
        // Pull down well past threshold...
        root.PointerMove(new PointerEventArgs { PointerId = 1, ClientY = 220 });
        // ...then move back above the start: delta ≤ 0 disengages the pull.
        root.PointerMove(new PointerEventArgs { PointerId = 1, ClientY = -10 });
        await root.PointerUpAsync(new PointerEventArgs { PointerId = 1, ClientY = -10 });

        Assert.Equal(0, fired);
    }
}
