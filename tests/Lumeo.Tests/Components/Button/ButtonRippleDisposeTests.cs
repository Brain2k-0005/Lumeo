using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Lumeo.Tests.Components.Button;

/// <summary>
/// Triage #22 (medium, lifecycle) — Button attaches the ripple pointerdown
/// listener via <c>Interop.RippleAttachAsync(_el)</c> but historically declared
/// no <c>@implements IAsyncDisposable</c> and no <c>DisposeAsync</c>, so the
/// listener leaked when the component was torn down.
///
/// The current source mirrors ShimmerButton: it tracks <c>_rippleAttached</c>,
/// detaches in <see cref="System.IAsyncDisposable.DisposeAsync"/> guarded by that
/// flag, and clears the flag afterwards (also swallowing
/// <see cref="Microsoft.JSInterop.JSDisconnectedException"/>). These tests pin the
/// teardown EDGES that the reconcile tests (#145) don't cover: the no-ripple
/// dispose must be a clean no-op, and a double dispose must be idempotent — the
/// <c>_rippleAttached</c> guard must prevent a second detach and any throw. They
/// fail if the dispose path or its flag guard regresses.
/// </summary>
public class ButtonRippleDisposeTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public ButtonRippleDisposeTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public async Task Disposing_A_Non_Ripple_Button_Never_Detaches_And_Does_Not_Throw()
    {
        // A button with no ripple never attached a listener, so teardown must be
        // a clean no-op — no detach call, no exception from the guarded path.
        var cut = _ctx.Render<Lumeo.Button>(p => p
            .Add(b => b.PressEffect, Lumeo.Button.ButtonPressEffect.None)
            .AddChildContent("Btn"));

        Assert.Equal(0, _interop.RippleAttachCallCount);

        var ex = await Record.ExceptionAsync(() => cut.Instance.DisposeAsync().AsTask());

        Assert.Null(ex);
        Assert.Equal(0, _interop.RippleDetachCallCount);
    }

    [Fact]
    public async Task Disposing_A_Ripple_Button_Twice_Detaches_Exactly_Once_And_Does_Not_Throw()
    {
        // Mounted with a ripple — the listener is attached on first render and
        // released on the first dispose.
        var cut = _ctx.Render<Lumeo.Button>(p => p
            .Add(b => b.PressEffect, Lumeo.Button.ButtonPressEffect.Ripple)
            .AddChildContent("Btn"));

        Assert.Equal(1, _interop.RippleAttachCallCount);

        await cut.Instance.DisposeAsync();
        Assert.Equal(1, _interop.RippleDetachCallCount);

        // A second dispose must be a no-op: the _rippleAttached guard prevents a
        // second detach and any double-dispose throw.
        var ex = await Record.ExceptionAsync(() => cut.Instance.DisposeAsync().AsTask());

        Assert.Null(ex);
        Assert.Equal(1, _interop.RippleDetachCallCount);
    }
}
