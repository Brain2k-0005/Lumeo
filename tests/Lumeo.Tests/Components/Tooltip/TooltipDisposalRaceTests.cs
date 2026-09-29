using System.Linq;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Tooltip;

/// <summary>
/// Overlay-disposal-race regression (5.12.2). Overlay content that awaits JS in
/// OnAfterRenderAsync (PositionFixed, or Cleanup's UnpositionFixed on close) and
/// gets disposed mid-await used to resume with a disposed <c>_selfRef</c> and pass
/// it into <c>AttachOverlayExitEnd</c>. <c>JSRuntime.TrackObjectReference</c> then
/// threw <see cref="ObjectDisposedException"/> straight out of OnAfterRenderAsync,
/// which killed the whole Blazor Server circuit (production report: a menu button
/// navigates away while its tooltip is still closing).
///
/// The fix has three layers: (1) each affected component's own <c>_disposed</c>
/// flag, set as the FIRST statement of DisposeAsync, with early returns after
/// every await that could race a concurrent disposal; (2) <see cref="Lumeo.Services.OverlayExitAnimator"/>
/// (the shared exit-latch these ten components — Tooltip/DropdownMenu/HoverCard/
/// Menubar/NavigationMenu and their sub-content — all go through) tracks its own
/// disposed state and both skips <c>WireExitAsync</c>'s wiring call and catches
/// <see cref="ObjectDisposedException"/>/<see cref="System.OperationCanceledException"/>/
/// <see cref="Microsoft.JSInterop.JSDisconnectedException"/> around it; (3)
/// <see cref="Lumeo.Services.ComponentInteropService.AttachOverlayExitEnd{T}"/>
/// itself now also catches those same exceptions around the JS call, so even a
/// caller that predates the shared animator (Dialog/Sheet/Drawer/AlertDialog, which
/// wire their own inline exit callback) is protected.
/// </summary>
public class TooltipDisposalRaceTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TooltipDisposalRaceTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderTooltip()
        => _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Tooltip>(0);
            builder.AddAttribute(1, "ShowDelay", 0);
            builder.AddAttribute(2, "HideDelay", 0);
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.TooltipTrigger>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Hover me")));
                b.CloseComponent();
                b.OpenComponent<L.TooltipContent>(2);
                b.AddAttribute(3, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Tooltip text")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

    private static MouseEventArgs Mouse => new();

    [Fact]
    public void AttachOverlayExitEnd_ObjectDisposedException_Does_Not_Escape_The_Renderer()
    {
        var module = _ctx.SetupComponentsModule();
        module.SetupVoid("attachOverlayExitEnd", _ => true)
            .SetException(new ObjectDisposedException("DotNetObjectReference`1[[Lumeo.TooltipContent]]"));

        var cut = RenderTooltip();
        cut.Find("div").MouseEnter(Mouse);

        // Close latches the exit and OnAfterRenderAsync's WireExitAsync call hands the
        // (in this test, always-throwing) attachOverlayExitEnd its DotNetObjectReference.
        // Pre-fix this exception escaped uncaught and killed the render/circuit.
        var ex = Record.Exception(() => cut.Find("div").MouseLeave(Mouse));
        Assert.Null(ex);
    }

    [Fact]
    public async Task Dispose_While_Cleanups_UnpositionFixed_Is_Pending_Does_Not_Throw()
    {
        var module = _ctx.SetupComponentsModule();
        module.Setup<string>("positionFixed", _ => true).SetResult("bottom");
        // If the _disposed early-return guard is missing, resuming reaches
        // attachOverlayExitEnd with a disposed _selfRef; make that call throw the
        // exact exception the real TrackObjectReference call throws, so a missing
        // guard fails this test too (not just silently "gets lucky").
        module.SetupVoid("attachOverlayExitEnd", _ => true)
            .SetException(new ObjectDisposedException("DotNetObjectReference`1[[Lumeo.TooltipContent]]"));
        // No .SetResult() yet: stays pending until we call it — this is the "Cleanup
        // -> UnpositionFixed on close" await the bug report names, hit while the exit
        // (Exiting=true, latched by the close render) is already armed. Only the FIRST
        // call is parked on this plan: DisposeAsync's own Cleanup() call (a pre-existing,
        // unrelated "_registered still true" re-entrancy — DisposeAsync doesn't know the
        // render-driven Cleanup() is already in flight) issues a SECOND unpositionFixed
        // call, which falls through to the module's Loose-mode default.
        var unpositionFixedCalls = 0;
        var unpositionFixed = module.SetupVoid("unpositionFixed", _ => unpositionFixedCalls++ == 0);

        var cut = RenderTooltip();
        Record.Exception(() => cut.Find("div").MouseEnter(Mouse)); // open, completes fully
        Assert.NotEmpty(cut.FindAll("[role='tooltip']"));

        // Close: OnParametersSet latches Exiting=true; OnAfterRenderAsync's Cleanup()
        // branch calls UnpositionFixed, now parked on the pending invocation above.
        var closeEx = Record.Exception(() => cut.Find("div").MouseLeave(Mouse));
        Assert.Null(closeEx);

        var content = cut.FindComponent<L.TooltipContent>().Instance;

        // Dispose the component directly WHILE UnpositionFixed is still pending —
        // mirrors AffixDisposeLifecycleTests' cut.Instance.DisposeAsync() idiom.
        var disposeEx = await Record.ExceptionAsync(async () => await content.DisposeAsync());
        Assert.Null(disposeEx);

        // Let the parked UnpositionFixed call resolve. Pre-fix, OnAfterRenderAsync
        // resumes, falls through to WireExitAsync (Exiting is still true) with a
        // disposed _selfRef, and attachOverlayExitEnd's exception escapes. That resumed
        // continuation runs on the renderer's own dispatcher asynchronously — a plain
        // delay lets it run, and a follow-up render through the SAME renderer (a
        // neutral probe, not another dispatch on the now-disposed TooltipContent
        // instance) is what surfaces a renderer-captured unhandled exception.
        unpositionFixed.SetVoidResult();
        await Task.Delay(100);
        var resumeEx = Record.Exception(() => _ctx.Render(b => b.AddContent(0, "probe")));
        Assert.Null(resumeEx);

        // Prove the EARLY RETURN actually fired (not just that something downstream
        // swallowed the exception): attachOverlayExitEnd must never have been reached.
        var invocations = _ctx.JSInterop.Invocations.Select(i => i.Identifier).ToList();
        Assert.DoesNotContain("attachOverlayExitEnd", invocations);
    }
}
