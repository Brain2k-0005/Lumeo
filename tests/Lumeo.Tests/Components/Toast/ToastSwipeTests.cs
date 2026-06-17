using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Toast;

/// <summary>
/// Regression tests for #232 — swipe-to-dismiss was fully implemented in the
/// JS + service layer (RegisterToastSwipe / OnToastSwipeDismiss) but no
/// component ever registered it, so sonner's signature gesture was inert.
/// The provider now gives each toast a DOM id and registers the gesture, then
/// unregisters it on dismissal / dispose.
/// </summary>
public class ToastSwipeTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public ToastSwipeTests()
    {
        _ctx.AddLumeoServices();
        // Last registration wins: route component interop through the tracker.
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private ToastService GetToastService() =>
        (ToastService)_ctx.Services.GetRequiredService(typeof(ToastService));

    [Fact]
    public void Provider_Registers_Swipe_For_Shown_Toast()
    {
        var toastService = GetToastService();
        var cut = _ctx.Render<L.ToastProvider>();

        toastService.Show("Swipe me");
        cut.WaitForState(() => cut.FindAll("[role='alert'],[role='status']").Count > 0, TimeSpan.FromSeconds(2));

        cut.WaitForAssertion(() =>
            Assert.Single(_interop.ToastSwipeRegistrations));
        // The element id and toast id must be non-empty and the DOM element
        // must actually carry that id so the JS handler can attach to it.
        var reg = _interop.ToastSwipeRegistrations[0];
        Assert.False(string.IsNullOrEmpty(reg.ElementId));
        Assert.False(string.IsNullOrEmpty(reg.ToastId));
        Assert.NotNull(cut.Find($"#{reg.ElementId}"));
    }

    [Fact]
    public void Swipe_Dismiss_Callback_Removes_Toast()
    {
        var toastService = GetToastService();
        var cut = _ctx.Render<L.ToastProvider>();

        toastService.Show("Swipe me");
        cut.WaitForState(() => cut.FindAll("[role='alert'],[role='status']").Count > 0, TimeSpan.FromSeconds(2));
        cut.WaitForAssertion(() => Assert.Single(_interop.ToastSwipeRegistrations));

        // Simulate the JS layer reporting a completed swipe gesture.
        var reg = _interop.ToastSwipeRegistrations[0];
        cut.InvokeAsync(() => reg.Handler(reg.ToastId));

        cut.WaitForState(() => cut.FindAll("[role='alert'],[role='status']").Count == 0, TimeSpan.FromSeconds(2));
        Assert.Empty(cut.FindAll("[role='alert'],[role='status']"));
    }

    [Fact]
    public void Provider_Unregisters_Swipe_When_Toast_Dismissed()
    {
        var toastService = GetToastService();
        var cut = _ctx.Render<L.ToastProvider>();

        toastService.Show("Dismiss me");
        cut.WaitForState(() => cut.FindAll("[role='alert'],[role='status']").Count > 0, TimeSpan.FromSeconds(2));
        cut.WaitForAssertion(() => Assert.Single(_interop.ToastSwipeRegistrations));

        cut.Find("button").Click();
        cut.WaitForState(() => cut.FindAll("[role='alert'],[role='status']").Count == 0, TimeSpan.FromSeconds(2));

        cut.WaitForAssertion(() =>
            Assert.Single(_interop.ToastSwipeUnregistrations));
    }
}
