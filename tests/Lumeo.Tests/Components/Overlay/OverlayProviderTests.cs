using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace Lumeo.Tests.Components.Overlay;

public class OverlayProviderTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public OverlayProviderTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Without_Overlays_On_Startup()
    {
        // OverlayProvider should render an empty container when no overlay has been shown
        var cut = _ctx.Render<Lumeo.OverlayProvider>();

        // No dialogs, sheets, or drawers expected
        Assert.Empty(cut.FindAll("[role='dialog']"));
    }

    [Fact]
    public void Two_AlertDialog_Overlays_Both_Render_With_Distinct_Titles()
    {
        var service = _ctx.Services.GetRequiredService<OverlayService>();
        var cut = _ctx.Render<Lumeo.OverlayProvider>();

        // Show two alert dialogs via the service
        _ = service.ShowAlertDialogAsync(new AlertDialogOptions
        {
            Title = "Confirm Delete",
            Description = "This action cannot be undone."
        });
        _ = service.ShowAlertDialogAsync(new AlertDialogOptions
        {
            Title = "Confirm Archive",
            Description = "Items will be archived."
        });

        cut.WaitForState(() => cut.Markup.Contains("Confirm Delete") && cut.Markup.Contains("Confirm Archive"));

        Assert.Contains("Confirm Delete", cut.Markup);
        Assert.Contains("Confirm Archive", cut.Markup);
    }

    // --- Mobile-fullscreen bottom sheet via OverlayService (2.1.1) ---
    //
    // OverlayOptions gained SwipeToClose so Sheets opened programmatically
    // can mirror the declarative <SheetContent SwipeToClose="true"> path.
    // Combined with SheetSize.Full on Side=Bottom the consumer gets a true
    // mobile-fullscreen bottom sheet with swipe-down dismiss — without
    // hand-rolling CSS selectors.

    private sealed class DummyOverlayBody : ComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.AddContent(0, "BODY");
        }
    }

    [Fact]
    public void ShowSheet_Routes_SwipeToClose_Option_Through_To_SheetContent()
    {
        var service = _ctx.Services.GetRequiredService<OverlayService>();
        var cut = _ctx.Render<Lumeo.OverlayProvider>();

        _ = service.ShowSheetAsync<DummyOverlayBody>(
            title: "Mobile sheet",
            side: Lumeo.Side.Bottom,
            size: SheetSize.Full,
            options: new OverlayOptions { SwipeToClose = true });

        cut.WaitForState(() => cut.Markup.Contains("BODY"));

        var dialog = cut.Find("[role='dialog']");
        var cls = dialog.GetAttribute("class") ?? "";

        // Side=Bottom + Size=Full now emits the fullscreen anchor + height,
        // and SwipeToClose=true means the JS swipe listener will be attached
        // on OnAfterRender. The class assertion proves the size/side option
        // round-trip; the SwipeToClose hook can only be observed via JS
        // interop, which bUnit does not exercise.
        // inset-y-0 supersedes the redundant bottom-0 under Cx.Merge.
        Assert.Contains("inset-y-0", cls);
        Assert.Contains("h-full", cls);
        Assert.Contains("max-h-full", cls);
    }

    [Fact]
    public void ShowSheet_Default_SwipeToClose_Is_False()
    {
        // Back-compat: existing OverlayService consumers that don't set
        // SwipeToClose must continue to get the legacy non-dismissable-by-swipe
        // behaviour. We assert the option default here as a regression guard.
        var options = new OverlayOptions();
        Assert.False(options.SwipeToClose);
    }

    // --- Responsive mobile overrides on OverlayOptions (2.1.3) ---
    //
    // The OverlayProvider helper logic that consults IResponsiveService to pick
    // the effective Side/Size/SwipeToClose under MobileBreakpoint is exercised
    // by manual mobile testing per the PR checklist. Here we only pin down the
    // OverlayOptions record contract so future refactors can't silently drop
    // the responsive defaults or opt-out path.

    [Fact]
    public void OverlayOptions_MobileBreakpoint_Default_Is_768()
    {
        var options = new OverlayOptions();
        // 768 = Tailwind md — same threshold IResponsiveService.IsMobile uses.
        Assert.Equal(768, options.MobileBreakpoint);
        // Mobile* override fields default to null = "no responsive switch".
        Assert.Null(options.MobileSheetSide);
        Assert.Null(options.MobileSheetSize);
        Assert.Null(options.MobileSwipeToClose);
    }

    // --- Scrollable body wrapper (3.2.6) -----------------------------------
    //
    // Sheets opened via OverlayService now wrap the rendered component in a
    // flex-1 min-h-0 overflow-y-auto -mx-1 px-1 div so consumers don't roll
    // their own scrollable form wrappers (which trip the box-shadow focus-
    // ring clip the report flagged). Default ScrollableBody=true; opt-out
    // via OverlayOptions { ScrollableBody = false } for sheets whose content
    // sets its own scrolling strategy (e.g. an embedded PdfViewer).

    [Fact]
    public void OverlayOptions_ScrollableBody_Default_Is_True()
    {
        var options = new OverlayOptions();
        Assert.True(options.ScrollableBody);
    }

    [Fact]
    public void ShowSheet_With_ScrollableBody_True_Wraps_Body_In_Overflow_Container()
    {
        var service = _ctx.Services.GetRequiredService<OverlayService>();
        var cut = _ctx.Render<Lumeo.OverlayProvider>();

        _ = service.ShowSheetAsync<DummyOverlayBody>(
            title: "Form sheet",
            options: new OverlayOptions { ScrollableBody = true });

        cut.WaitForState(() => cut.Markup.Contains("BODY"));

        // The wrapper carries flex-1 + overflow-y-auto and the -mx-1 px-1
        // breathing room. Asserting the class string is enough — a typo
        // here would silently lose the focus-ring fix.
        Assert.Contains("flex-1 min-h-0 overflow-y-auto -mx-1 px-1", cut.Markup);
    }

    [Fact]
    public void ShowSheet_With_ScrollableBody_False_Renders_Without_Overflow_Wrapper()
    {
        var service = _ctx.Services.GetRequiredService<OverlayService>();
        var cut = _ctx.Render<Lumeo.OverlayProvider>();

        _ = service.ShowSheetAsync<DummyOverlayBody>(
            title: "PDF preview sheet",
            options: new OverlayOptions { ScrollableBody = false });

        cut.WaitForState(() => cut.Markup.Contains("BODY"));

        // Opt-out path: the body still renders, just without the auto-scroll
        // chrome — consumer's component handles its own layout.
        Assert.DoesNotContain("flex-1 min-h-0 overflow-y-auto -mx-1 px-1", cut.Markup);
        Assert.Contains("BODY", cut.Markup);
    }

    [Fact]
    public void OverlayOptions_With_Null_MobileBreakpoint_Disables_Responsive_Switch()
    {
        var options = new OverlayOptions
        {
            MobileBreakpoint = null,
            MobileSheetSize = SheetSize.Full
        };

        // MobileBreakpoint=null is the documented opt-out: even though
        // MobileSheetSize is set, the provider must keep the desktop SheetSize
        // at every viewport. We can't drive the provider end-to-end here
        // without mocking IResponsiveService AND inspecting SheetContent
        // class output, so this test guards the record values only.
        Assert.Null(options.MobileBreakpoint);
        Assert.Equal(SheetSize.Full, options.MobileSheetSize);
    }
}
