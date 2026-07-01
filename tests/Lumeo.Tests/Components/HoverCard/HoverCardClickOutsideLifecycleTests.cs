using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.HoverCard;

/// <summary>
/// battle-wave2 #179 (lifecycle) — the touch pin/click-outside path leaked the
/// global click-outside listener. <c>_clickOutsideRegistered</c> latched true on
/// the first pin and was only ever reset to false in <c>DisposeAsync</c>, so after
/// a single auto-unpin (or a manual unpin) the listener stayed registered for the
/// component's entire life.
///
/// The fix unregisters on every unpin (the <c>TogglePin</c> else-branch and
/// <c>OnClickOutside</c>) and resets <c>_clickOutsideRegistered</c> so the listener
/// lives only while the card is actually pinned; the next pin re-registers lazily
/// via the existing <c>if (!_clickOutsideRegistered)</c> guard.
///
/// These assert the MECHANISM via the recorded
/// <see cref="TrackingInteropService.ClickOutsideRegistrations"/> /
/// <see cref="TrackingInteropService.ClickOutsideUnregistrations"/> — no real JS.
/// </summary>
public class HoverCardClickOutsideLifecycleTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public HoverCardClickOutsideLifecycleTests()
    {
        _ctx.AddLumeoServices();
        // Override the interop with the tracking double so click-outside
        // register/unregister calls are recorded (HoverCard injects the interface).
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static RenderFragment Body => b =>
    {
        b.OpenComponent<L.HoverCardTrigger>(0);
        b.AddAttribute(1, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Profile")));
        b.CloseComponent();

        b.OpenComponent<L.HoverCardContent>(2);
        b.AddAttribute(3, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Card body")));
        b.CloseComponent();
    };

    private IRenderedComponent<L.HoverCard> Render()
        => _ctx.Render<L.HoverCard>(p => p
            .Add(c => c.OpenDelay, 0)
            .Add(c => c.CloseDelay, 0)
            .Add(c => c.ChildContent, Body));

    // The trigger is the inline-flex wrapper div; @onclick toggles the pin.
    private static IElement Trigger(IRenderedComponent<L.HoverCard> cut) =>
        cut.FindAll("div").First(d => (d.GetAttribute("class") ?? "").Contains("inline-flex"));

    [Fact]
    public void Unpin_Unregisters_The_Click_Outside_Listener()
    {
        var cut = Render();

        // First tap pins: the global click-outside listener is registered once.
        Trigger(cut).Click();
        cut.WaitForAssertion(() => Assert.Single(_interop.ClickOutsideRegistrations));
        Assert.Empty(_interop.ClickOutsideUnregistrations);

        // Second tap unpins. Without the fix the else-branch only flipped _pinned
        // and never unregistered, so the listener leaked; with the fix unpin
        // tears it down.
        Trigger(cut).Click();
        cut.WaitForAssertion(() => Assert.Single(_interop.ClickOutsideUnregistrations));

        // The unregistration targets the same wrapper that was registered.
        Assert.Equal(_interop.ClickOutsideRegistrations[0].ElementId,
                     _interop.ClickOutsideUnregistrations[0]);
    }

    [Fact]
    public async Task Auto_Unpin_Via_Click_Outside_Unregisters_The_Listener()
    {
        var cut = Render();

        // Pin, then fire the captured click-outside handler to simulate the JS
        // global mousedown landing outside the card (the OnClickOutside path).
        Trigger(cut).Click();
        cut.WaitForAssertion(() => Assert.Single(_interop.ClickOutsideRegistrations));

        var registration = _interop.ClickOutsideRegistrations[0];
        await cut.InvokeAsync(() => registration.Handler());

        // The auto-unpin must unregister the listener, not leave it wired for life.
        cut.WaitForAssertion(() => Assert.Single(_interop.ClickOutsideUnregistrations));
    }

    [Fact]
    public void Re_Pin_After_Unpin_Re_Registers_The_Listener()
    {
        var cut = Render();

        Trigger(cut).Click(); // pin
        cut.WaitForAssertion(() => Assert.Single(_interop.ClickOutsideRegistrations));
        Trigger(cut).Click(); // unpin
        cut.WaitForAssertion(() => Assert.Single(_interop.ClickOutsideUnregistrations));

        // Re-pin: because unpin reset _clickOutsideRegistered, the lazy
        // `if (!_clickOutsideRegistered)` guard must re-register a SECOND time.
        // (With the buggy latch the flag stayed true and this never re-registered.)
        Trigger(cut).Click();
        cut.WaitForAssertion(() => Assert.Equal(2, _interop.ClickOutsideRegistrations.Count));
    }
}
