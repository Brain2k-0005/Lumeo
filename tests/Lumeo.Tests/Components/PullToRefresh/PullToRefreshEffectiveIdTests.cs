using Bunit;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Lumeo.Tests.Components.PullToRefresh;

/// <summary>
/// Battle-test #14 (high, lifecycle) — when a consumer splats an
/// <c>id="..."</c> via AdditionalAttributes it WINS in the rendered DOM (the
/// splat is emitted after the explicit <c>id="@EffectiveId"</c>). Every JS
/// interop call (the non-passive touchmove guard, the scrollTop gate, and
/// pointer capture/release) must therefore target that same consumer id —
/// otherwise <c>document.getElementById(internalId)</c> finds nothing and the
/// whole gesture silently no-ops.
///
/// Before the fix the component passed the internal <c>_elementId</c> ("ptr…")
/// to every interop call, so these assertions — that the interop ids equal the
/// consumer-supplied id and the rendered DOM id — FAIL. After the fix
/// (EffectiveId everywhere) they PASS.
/// </summary>
public class PullToRefreshEffectiveIdTests : IAsyncLifetime
{
    private const string CustomId = "my-pull-host";

    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public PullToRefreshEffectiveIdTests()
    {
        _ctx.AddLumeoServices();
        // Last interface registration wins, so PullToRefresh resolves the spy.
        _ctx.Services.AddScoped<IComponentInteropService>(_ => _interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<Lumeo.PullToRefresh> RenderWithConsumerId(EventCallback onRefresh = default) =>
        _ctx.Render<Lumeo.PullToRefresh>(p => p
            .Add(c => c.OnRefresh, onRefresh)
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object> { ["id"] = CustomId })
            .AddChildContent("<p>content</p>"));

    [Fact]
    public void ConsumerSuppliedId_Wins_In_The_Rendered_Dom()
    {
        var cut = RenderWithConsumerId();

        // The splat is emitted after the explicit id, so the consumer id wins.
        var rootId = cut.Find("div[id]").GetAttribute("id");
        Assert.Equal(CustomId, rootId);
    }

    [Fact]
    public void GestureGuard_Registers_Against_The_Consumer_Id_Not_The_Internal_Id()
    {
        var cut = RenderWithConsumerId();

        // The non-passive touchmove guard must be wired to the DOM id that
        // actually exists — the consumer's, not the internal "ptr…" fallback.
        cut.WaitForAssertion(() => Assert.Single(_interop.PullToRefreshRegistrations));
        Assert.Equal(CustomId, _interop.PullToRefreshRegistrations[0]);
        // Guard against regressing to the internal id.
        Assert.DoesNotContain(_interop.PullToRefreshRegistrations, id => id.StartsWith("ptr"));
    }

    [Fact]
    public async Task GestureGuard_Unregisters_Against_The_Consumer_Id_On_Dispose()
    {
        var cut = RenderWithConsumerId();
        cut.WaitForAssertion(() => Assert.Single(_interop.PullToRefreshRegistrations));

        await cut.Instance.DisposeAsync();

        var unregistered = Assert.Single(_interop.PullToRefreshUnregistrations);
        Assert.Equal(CustomId, unregistered);
    }

    [Fact]
    public async Task PointerCapture_And_Release_Use_The_Consumer_Id()
    {
        var cut = RenderWithConsumerId();

        var root = cut.Find($"div[id='{CustomId}']");

        // pointerdown captures the pointer; the gate reads scrollTop (== 0 in the
        // fake) and engages, then pointerup releases the capture. All three must
        // target the consumer id so the JS lookups resolve the real element.
        root.PointerDown(new PointerEventArgs { PointerId = 1, ClientY = 0 });
        root.PointerMove(new PointerEventArgs { PointerId = 1, ClientY = 40 });
        await root.PointerUpAsync(new PointerEventArgs { PointerId = 1, ClientY = 40 });

        var captured = Assert.Single(_interop.PointerCaptureCalls);
        Assert.Equal(CustomId, captured.ElementId);

        var released = Assert.Single(_interop.PointerReleaseCalls);
        Assert.Equal(CustomId, released.ElementId);
    }
}
