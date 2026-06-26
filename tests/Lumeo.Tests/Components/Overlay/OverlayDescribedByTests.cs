using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace Lumeo.Tests.Components.Overlay;

/// <summary>
/// Battle-test regression (n=181, keyboard-a11y): service-opened overlays
/// emitted <c>aria-describedby</c> pointing at an id that is never rendered.
/// The OverlayProvider only ever renders a *Header / *Title for an overlay — it
/// never renders a *Description — yet DialogContent / SheetContent /
/// DrawerContent used to bind <c>aria-describedby="@Context.DescriptionId"</c>
/// unconditionally. With no matching element in the DOM this is a dangling IDREF
/// that confuses assistive tech.
///
/// The fix mirrors the existing AlertDialog pattern: each root tracks whether a
/// *Description child registered (via a persistent DescriptionRegistry), and the
/// content wires aria-describedby only when one did. These tests assert the
/// OBSERVABLE markup — the attribute is absent on every service-opened overlay
/// (which never has a description) — so they fail before the fix and pass after.
/// bUnit cannot move real focus, so nothing here touches activeElement.
/// </summary>
public class OverlayDescribedByTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public OverlayDescribedByTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private sealed class DummyOverlayBody : ComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
            => builder.AddContent(0, "BODY");
    }

    [Fact]
    public void Service_Opened_Dialog_Omits_Dangling_Aria_Describedby()
    {
        var service = _ctx.Services.GetRequiredService<OverlayService>();
        var cut = _ctx.Render<Lumeo.OverlayProvider>();

        _ = service.ShowDialogAsync<DummyOverlayBody>(title: "Settings");

        cut.WaitForState(() => cut.Markup.Contains("BODY"));

        var dialog = cut.Find("[role='dialog']");
        // The provider rendered no DialogDescription, so before the fix this
        // attribute pointed at a never-rendered id (a dangling IDREF). After the
        // fix the attribute is omitted entirely.
        Assert.False(dialog.HasAttribute("aria-describedby"));
    }

    [Fact]
    public void Service_Opened_Sheet_Omits_Dangling_Aria_Describedby()
    {
        var service = _ctx.Services.GetRequiredService<OverlayService>();
        var cut = _ctx.Render<Lumeo.OverlayProvider>();

        _ = service.ShowSheetAsync<DummyOverlayBody>(title: "Filters");

        cut.WaitForState(() => cut.Markup.Contains("BODY"));

        var dialog = cut.Find("[role='dialog']");
        Assert.False(dialog.HasAttribute("aria-describedby"));
    }

    [Fact]
    public void Service_Opened_Drawer_Omits_Dangling_Aria_Describedby()
    {
        var service = _ctx.Services.GetRequiredService<OverlayService>();
        var cut = _ctx.Render<Lumeo.OverlayProvider>();

        _ = service.ShowDrawerAsync<DummyOverlayBody>(title: "Details");

        cut.WaitForState(() => cut.Markup.Contains("BODY"));

        var dialog = cut.Find("[role='dialog']");
        Assert.False(dialog.HasAttribute("aria-describedby"));
    }
}
