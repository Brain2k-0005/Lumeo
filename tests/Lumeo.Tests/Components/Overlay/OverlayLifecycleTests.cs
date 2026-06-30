using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Overlay;

/// <summary>
/// Battle wave-2 LIFECYCLE regressions for the Overlay provider.
///
/// #82  — a service-opened Sheet must play its slide-out: the provider keeps the
///        instance mounted with Open=false (true→false drives SheetContent._exiting)
///        for the exit window instead of unmounting it instantly, while still
///        resolving the awaiting caller immediately.
/// #180 — a destructive AlertDialog confirm must not double-dispatch the close.
///        HandleConfirm closes the overlay, then AlertDialogAction auto-dismisses
///        (Open=false → OnOverlayClosed); the provider must suppress the redundant
///        Cancel so the same id isn't close-dispatched as both Ok and Cancel.
/// </summary>
public class OverlayLifecycleTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly OverlayService _overlay = new();

    public OverlayLifecycleTests()
    {
        _ctx.AddLumeoServices();
        // Hold our own OverlayService so we can observe the OnClose stream and
        // resolve TaskCompletionSources by hand (mirrors ConfirmButtonTests).
        _ctx.Services.AddScoped<OverlayService>(_ => _overlay);
        _ctx.Services.AddScoped<IOverlayService>(_ => _overlay);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private sealed class Body : ComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, "BODY");
    }

    // --- #82: Sheet slide-out plays on service-driven close -----------------

    [Fact]
    public async Task Closing_A_Service_Sheet_Plays_The_Slide_Out_Instead_Of_Vanishing()
    {
        OverlayInstance? shown = null;
        _overlay.OnShow += i => shown = i;

        var cut = _ctx.Render<Lumeo.OverlayProvider>();

        _ = _overlay.ShowSheetAsync<Body>(title: "Slide sheet", side: Lumeo.Side.Right);
        cut.WaitForState(() => cut.Markup.Contains("BODY"));

        // Drive the close through the service using the id the provider is tracking.
        var id = shown!.Id;
        await cut.InvokeAsync(() => _overlay.Cancel(id));

        // Without the fix the provider removed the overlay from its list on close,
        // unmounting the Sheet immediately → no exit class ever rendered. With the
        // fix the provider flips Open=false, SheetContent latches _exiting, and the
        // panel slides out while still mounted.
        cut.WaitForAssertion(() =>
            Assert.Contains("animate-slide-out-to-right", cut.Markup));

        // The Sheet body is still in the DOM during the exit window (not vanished).
        Assert.Contains("BODY", cut.Markup);
    }

    [Fact]
    public async Task Closing_A_Service_Sheet_Resolves_The_Awaiting_Task_Immediately()
    {
        OverlayInstance? shown = null;
        _overlay.OnShow += i => shown = i;

        var cut = _ctx.Render<Lumeo.OverlayProvider>();

        var task = _overlay.ShowSheetAsync<Body>(title: "Slide sheet");
        cut.WaitForState(() => cut.Markup.Contains("BODY"));

        await cut.InvokeAsync(() => _overlay.Cancel(shown!.Id));

        // The result must NOT wait on the exit animation: the deferred unmount only
        // governs the visual teardown. The caller's task completes right away.
        cut.WaitForAssertion(() => Assert.True(task.IsCompleted));
        var sheetResult = await task;
        Assert.True(sheetResult.Cancelled);
    }

    // --- #180: destructive AlertDialog confirm closes exactly once ----------

    [Fact]
    public void Destructive_AlertDialog_Confirm_Dispatches_Close_Exactly_Once()
    {
        var closeCalls = new List<(string id, bool cancelled)>();
        _overlay.OnClose += (id, _, cancelled) => closeCalls.Add((id, cancelled));

        var cut = _ctx.Render<Lumeo.OverlayProvider>();

        _ = _overlay.ShowAlertDialogAsync(new AlertDialogOptions
        {
            Title = "Delete?",
            ConfirmText = "Delete",
            IsDestructive = true
        });
        cut.WaitForState(() => cut.Markup.Contains("Delete?"));

        // The destructive confirm is the AlertDialogAction button (bg-destructive).
        var action = cut.FindAll("button")
            .First(b => (b.GetAttribute("class") ?? "").Contains("bg-destructive"));
        action.Click();

        // Without the fix the confirm fires OverlayService.Close, then the
        // AlertDialogAction auto-dismiss flips Open=false → OnOverlayClosed → Cancel,
        // so OnClose fires TWICE for the same id (once Ok, once Cancel). The
        // _terminated guard suppresses the redundant Cancel: exactly one dispatch.
        Assert.Single(closeCalls);
        // And the single dispatch is the confirm (not cancelled).
        Assert.False(closeCalls[0].cancelled);
    }

    [Fact]
    public async Task Destructive_AlertDialog_Confirm_Resolves_Task_As_Confirmed_Not_Cancelled()
    {
        var cut = _ctx.Render<Lumeo.OverlayProvider>();

        var task = _overlay.ShowAlertDialogAsync(new AlertDialogOptions
        {
            Title = "Delete?",
            ConfirmText = "Delete",
            IsDestructive = true
        });
        cut.WaitForState(() => cut.Markup.Contains("Delete?"));

        var action = cut.FindAll("button")
            .First(b => (b.GetAttribute("class") ?? "").Contains("bg-destructive"));
        action.Click();

        cut.WaitForAssertion(() => Assert.True(task.IsCompleted));
        // The destructive action confirmed — the awaiting caller must see a
        // non-cancelled result, never the stray Cancel from the double-dispatch.
        var dialogResult = await task;
        Assert.False(dialogResult.Cancelled);
    }
}
