using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Overlay;

/// <summary>
/// Overlay-disposal-race regression (5.12.2) — see
/// <see cref="Lumeo.Tests.Components.Tooltip.TooltipDisposalRaceTests"/> for the full
/// writeup. Dialog/Sheet/Drawer/AlertDialog predate the shared
/// <see cref="Lumeo.Services.OverlayExitAnimator"/> and wire their own inline exit
/// callback (the same shape: <c>_selfRef ??= DotNetObjectReference.Create(this);
/// await Interop.AttachOverlayExitEnd(...)</c> from OnAfterRenderAsync), so they hit
/// the exact same disposal-during-await race and needed the same <c>_disposed</c>
/// guard + broadened exception catch (ObjectDisposedException / OperationCanceledException,
/// alongside the pre-existing JSDisconnectedException) inlined at their own call site.
///
/// These render through <see cref="OverlayProvider"/>/<see cref="OverlayService"/>
/// (service-opened, like <c>OverlayExitAnimationRaceTests</c>) rather than as
/// declarative Blazor markup, so the exit path under test is the one every consumer
/// actually uses via <c>IOverlayService</c>.
/// </summary>
public class FixedOverlayDisposalRaceTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public FixedOverlayDisposalRaceTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private sealed class Body : ComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, "BODY");
    }

    private string OpenAndGetId(IOverlayService overlay, Func<Task<OverlayResult>> open)
    {
        OverlayInstance? shown = null;
        overlay.OnShow += i => shown = i;
        _ = open();
        return shown!.Id;
    }

    /// <summary>
    /// Cancels the overlay and asserts the close+exit-wiring render never surfaces an
    /// unhandled exception. The exit-wiring continuation (WireExitAsync's
    /// attachOverlayExitEnd call, from OnAfterRenderAsync) is scheduled on the
    /// renderer's own dispatcher and does not necessarily complete inside
    /// cut.InvokeAsync itself — a plain delay lets it run, and a follow-up render
    /// through the SAME renderer (a neutral probe) is what actually surfaces a
    /// renderer-captured unhandled exception (mirrors TooltipDisposalRaceTests'
    /// Dispose_While_Cleanups_UnpositionFixed_Is_Pending_Does_Not_Throw).
    /// </summary>
    private async Task<Exception?> CancelAndAssertNoUnhandledException(IRenderedComponent<Lumeo.OverlayProvider> cut, IOverlayService overlay, string id)
    {
        var cancelEx = await Record.ExceptionAsync(() => cut.InvokeAsync(() => overlay.Cancel(id)));
        if (cancelEx is not null) return cancelEx;

        await Task.Delay(100);
        return Record.Exception(() => cut.Render());
    }

    [Fact]
    public async Task Dialog_AttachOverlayExitEnd_ObjectDisposedException_Does_Not_Escape_The_Renderer()
    {
        var module = _ctx.SetupComponentsModule();
        module.SetupVoid("attachOverlayExitEnd", _ => true)
            .SetException(new ObjectDisposedException("DotNetObjectReference`1[[Lumeo.DialogContent]]"));

        var overlay = _ctx.Services.GetRequiredService<Lumeo.Services.IOverlayService>();
        var cut = _ctx.Render<Lumeo.OverlayProvider>();
        var id = OpenAndGetId(overlay, () => overlay.ShowDialogAsync<Body>(title: "D"));
        cut.WaitForState(() => cut.Markup.Contains("BODY"));

        var ex = await CancelAndAssertNoUnhandledException(cut, overlay, id);
        Assert.Null(ex);
    }

    [Fact]
    public async Task Sheet_AttachOverlayExitEnd_ObjectDisposedException_Does_Not_Escape_The_Renderer()
    {
        var module = _ctx.SetupComponentsModule();
        module.SetupVoid("attachOverlayExitEnd", _ => true)
            .SetException(new ObjectDisposedException("DotNetObjectReference`1[[Lumeo.SheetContent]]"));

        var overlay = _ctx.Services.GetRequiredService<Lumeo.Services.IOverlayService>();
        var cut = _ctx.Render<Lumeo.OverlayProvider>();
        var id = OpenAndGetId(overlay, () => overlay.ShowSheetAsync<Body>(title: "S", side: Lumeo.Side.Right));
        cut.WaitForState(() => cut.Markup.Contains("BODY"));

        var ex = await CancelAndAssertNoUnhandledException(cut, overlay, id);
        Assert.Null(ex);
    }

    [Fact]
    public async Task Drawer_AttachOverlayExitEnd_ObjectDisposedException_Does_Not_Escape_The_Renderer()
    {
        var module = _ctx.SetupComponentsModule();
        module.SetupVoid("attachOverlayExitEnd", _ => true)
            .SetException(new ObjectDisposedException("DotNetObjectReference`1[[Lumeo.DrawerContent]]"));

        var overlay = _ctx.Services.GetRequiredService<Lumeo.Services.IOverlayService>();
        var cut = _ctx.Render<Lumeo.OverlayProvider>();
        var id = OpenAndGetId(overlay, () => overlay.ShowDrawerAsync<Body>(title: "Dr"));
        cut.WaitForState(() => cut.Markup.Contains("BODY"));

        var ex = await CancelAndAssertNoUnhandledException(cut, overlay, id);
        Assert.Null(ex);
    }

    [Fact]
    public async Task AlertDialog_AttachOverlayExitEnd_ObjectDisposedException_Does_Not_Escape_The_Renderer()
    {
        var module = _ctx.SetupComponentsModule();
        module.SetupVoid("attachOverlayExitEnd", _ => true)
            .SetException(new ObjectDisposedException("DotNetObjectReference`1[[Lumeo.AlertDialogContent]]"));

        var overlay = _ctx.Services.GetRequiredService<Lumeo.Services.IOverlayService>();
        var cut = _ctx.Render<Lumeo.OverlayProvider>();
        var id = OpenAndGetId(overlay, () => overlay.ShowAlertDialogAsync(new AlertDialogOptions { Title = "Alert" }));
        cut.WaitForState(() => cut.Markup.Contains("Alert"));

        var ex = await CancelAndAssertNoUnhandledException(cut, overlay, id);
        Assert.Null(ex);
    }
}
