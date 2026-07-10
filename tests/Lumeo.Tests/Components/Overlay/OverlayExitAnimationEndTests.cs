using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Overlay;

/// <summary>
/// B11 (final) — Radix-Presence-style overlay exit. The consumer's report ("the
/// backdrop animates away but the panel doesn't move with it") survived four timing
/// patches because it was NOT a timing bug: the open-time containing-block guard
/// (<c>attachOverlaySlideEnd</c> stamps inline <c>animation:none !important</c> on
/// the panel once the enter animation settles) OVERRODE the <c>animate-slide-out-*</c>
/// / <c>animate-zoom-out</c> class the component applied on close, so the panel could
/// not animate at all while the never-stamped backdrop faded solo. A frame-by-frame
/// rAF trace confirmed the panel's computed <c>animationName</c> was literally
/// <c>none</c> throughout every close path.
///
/// The fix hands the unmount to the panel's OWN exit animationend
/// (<c>attachOverlayExitEnd</c>), which first strips that inline guard so the exit
/// keyframe runs, then notifies <c>OnExitAnimationEnd</c> once it finishes — dropping
/// backdrop + panel together. The C# timer is demoted to a strict fallback.
///
/// bUnit can't observe computed style, so these tests lock the C# contract: the exit
/// window keeps BOTH elements mounted, the animationend callback is wired for every
/// service overlay type and drops both elements together, and the fallback timer
/// still unmounts if that callback never arrives.
/// </summary>
public class OverlayExitAnimationEndTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly OverlayService _overlay = new();
    private readonly TrackingInteropService _interop = new();

    public OverlayExitAnimationEndTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddScoped<OverlayService>(_ => _overlay);
        _ctx.Services.AddScoped<IOverlayService>(_ => _overlay);
        // Tracking double captures the exit-end wiring so a test can SIMULATE the
        // browser animationend by invoking OnExitAnimationEnd (it never auto-fires).
        _ctx.Services.AddScoped<IComponentInteropService>(_ => _interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private sealed class Body : ComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, "BODY");
    }

    private string Open(string type)
    {
        OverlayInstance? shown = null;
        _overlay.OnShow += i => shown = i;
        _ = type switch
        {
            "Sheet" => _overlay.ShowSheetAsync<Body>(title: "S", side: Lumeo.Side.Right),
            "Dialog" => _overlay.ShowDialogAsync<Body>(title: "D"),
            "Drawer" => _overlay.ShowDrawerAsync<Body>(title: "Dr"),
            "AlertDialog" => _overlay.ShowAlertDialogAsync(new AlertDialogOptions { Title = "ALERTBODY" }),
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };
        return shown!.Id;
    }

    // Panel role + the exit class + the marker text per type.
    private static (string Role, string ExitClass, string Marker) Shape(string type) => type switch
    {
        "Sheet" => ("dialog", "animate-slide-out-to-right", "BODY"),
        "Dialog" => ("dialog", "animate-zoom-out", "BODY"),
        "Drawer" => ("dialog", "animate-slide-out-to-bottom", "BODY"),
        "AlertDialog" => ("alertdialog", "animate-zoom-out", "ALERTBODY"),
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    [Theory]
    [InlineData("Sheet")]
    [InlineData("Dialog")]
    [InlineData("Drawer")]
    [InlineData("AlertDialog")]
    public async Task Backdrop_stays_mounted_alongside_panel_during_exit_window(string type)
    {
        var (role, exitClass, marker) = Shape(type);
        var cut = _ctx.Render<Lumeo.OverlayProvider>();
        var id = Open(type);
        cut.WaitForState(() => cut.Markup.Contains(marker));

        await cut.InvokeAsync(() => _overlay.Cancel(id));

        // The regression: the backdrop must NOT drop a frame before the panel. In the
        // same render commit the panel carries its exit class AND the backdrop is
        // still present with its fade-out — both leave together, never backdrop-first.
        Assert.NotEmpty(cut.FindAll($"[role='{role}'].{CssEscape(exitClass)}"));
        Assert.NotEmpty(cut.FindAll(".animate-fade-out"));
        Assert.Contains(marker, cut.Markup); // panel still mounted during the exit
    }

    [Theory]
    [InlineData("Sheet")]
    [InlineData("Dialog")]
    [InlineData("Drawer")]
    [InlineData("AlertDialog")]
    public async Task AnimationEnd_callback_drops_backdrop_and_panel_together(string type)
    {
        var (role, _, marker) = Shape(type);
        var cut = _ctx.Render<Lumeo.OverlayProvider>();
        var id = Open(type);
        cut.WaitForState(() => cut.Markup.Contains(marker));

        await cut.InvokeAsync(() => _overlay.Cancel(id));

        // Exit window keeps BOTH elements mounted. Asserted SYNCHRONOUSLY on the
        // just-committed close render — the exit render commits inline within the
        // Cancel dispatch (all test interop is instant), so this runs BEFORE any
        // wall-clock poll, when the content's 280 ms and the provider's 320 ms
        // fallback timers provably cannot yet have fired. Latch state, not
        // wall-clock — the canonical overlay-exit-family pattern, mirroring
        // Backdrop_stays_mounted_alongside_panel_during_exit_window. This REPLACES
        // the old ordering, where these two transient asserts sat AFTER a
        // WaitForAssertion poll and so raced the fallback timers under a starved CI
        // thread pool (flapped at 612 ms > 320 ms > 280 ms; passed locally).
        Assert.NotEmpty(cut.FindAll($"[role='{role}']"));
        Assert.NotEmpty(cut.FindAll(".animate-fade-out"));

        // The content wired the panel's animationend to OnExitAnimationEnd. That
        // wiring lands in a follow-up OnAfterRender turn, so it must be polled — but
        // it is a MONOTONIC latch (OverlayExitEndWirings only grows;
        // LastOverlayExitCallback is never re-nulled), so a starved poll merely waits
        // longer (up to the 10 s module ceiling) and can never spuriously fail, even
        // if the fallback timers fire during it.
        cut.WaitForAssertion(() => Assert.NotEmpty(_interop.OverlayExitEndWirings));
        var callback = _interop.LastOverlayExitCallback!;
        Assert.NotNull(callback);

        // Simulate the browser firing animationend on the panel: it drops the
        // backdrop AND panel in the same render commit (removed together). This is
        // idempotent with the fallback timers — FinishExit latches on `_exiting` and
        // FinishClose/RemoveOverlay are safe once-per-id, so whichever lands first
        // wins and the other no-ops. The empty end-state is terminal (nothing
        // re-opens the overlay), so this poll is monotonic and cannot spuriously fail.
        await cut.InvokeAsync(() => callback.OnExitAnimationEnd());
        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll($"[role='{role}']"));
            Assert.Empty(cut.FindAll(".animate-fade-out"));
        });
    }

    [Fact]
    public async Task Fallback_timer_unmounts_when_animationend_never_fires()
    {
        // Declarative Sheet (no provider) so ONLY the content's own fallback timer can
        // unmount it — the tracking double never fires the animationend callback.
        var cut = _ctx.Render<Lumeo.Sheet>(p => p
            .Add(s => s.Open, true)
            .Add(s => s.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<Lumeo.SheetContent>(0);
                b.AddAttribute(1, "Side", Lumeo.Side.Right);
                b.AddAttribute(2, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "BODY")));
                b.CloseComponent();
            })));
        Assert.NotEmpty(cut.FindAll("[role='dialog']"));

        cut.Render(p => p.Add(s => s.Open, false));

        // The animationend path is wired…
        cut.WaitForAssertion(() => Assert.NotEmpty(_interop.OverlayExitEndWirings));
        // …but the callback is never invoked, so the fallback timer must still unmount
        // the panel. Stable end-state poll; inherits the 10 s module ceiling
        // (TestContextExtensions) — returns the instant it does, but a starved CI thread
        // pool delaying the fallback-timer dispatch can't trip it.
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[role='dialog']")));
    }

    // Minimal CSS class escaper for the compound attribute+class selectors above
    // (the class names here contain only [a-z-], so this is effectively identity,
    // but keep it explicit so a future class with a digit/':' doesn't silently break
    // the selector).
    private static string CssEscape(string cls) => cls.Replace(":", "\\:");
}
