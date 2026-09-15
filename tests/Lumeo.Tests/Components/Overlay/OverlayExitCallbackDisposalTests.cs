using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Overlay;

/// <summary>
/// Field report #464 (carried finding) — "overlay exit animation calls back after the
/// component is disposed (page change with an open dropdown)". The shared exit-animation
/// mechanism (<c>IOverlayExitCallback</c> + <c>attachOverlayExitEnd</c>, see
/// <see cref="OverlayExitAnimationEndTests"/>) keeps the panel mounted after close and hands
/// the unmount to the panel's own <c>animationend</c>, notifying <c>[JSInvokable]
/// OnExitAnimationEnd</c>. If the host page navigates away WHILE that exit is still pending,
/// Blazor disposes the component (and the fixed overlays' <c>_selfRef</c> is disposed with
/// it) — but the JS call can still be in flight, and nothing stopped
/// <c>OnExitAnimationEnd</c> -&gt; <c>FinishExit()</c> -&gt; <c>StateHasChanged()</c> from
/// running against an already-disposed component. <see cref="OverlayProvider"/> already
/// guards its own deferred-unmount callback with a <c>_disposed</c> flag (see
/// <c>FinishClose</c>); the per-content components (Sheet, DropdownMenu, ...) did not have
/// the same guard on their <c>FinishExit</c>. These tests reproduce the callback landing
/// after dispose and assert it is a safe no-op.
/// </summary>
public class OverlayExitCallbackDisposalTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public OverlayExitCallbackDisposalTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static RenderFragment SheetFragment(bool open) => builder =>
    {
        builder.OpenComponent<L.Sheet>(0);
        builder.AddAttribute(1, "Open", open);
        builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
        {
            b.OpenComponent<L.SheetContent>(0);
            b.AddAttribute(1, "Side", L.Side.Right);
            b.AddAttribute(2, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Body")));
            b.CloseComponent();
        }));
        builder.CloseComponent();
    };

    [Fact]
    public async Task Sheet_ExitAnimationEnd_After_Dispose_Does_Not_Throw()
    {
        var cut = _ctx.Render<ConditionalRoot>(p => p
            .Add(x => x.Show, true)
            .AddChildContent(SheetFragment(open: true)));

        // Close: latches the exit and (on the follow-up render) wires the
        // animationend callback through the tracking interop double.
        cut.Render(p => p.Add(x => x.Show, true).AddChildContent(SheetFragment(open: false)));
        cut.WaitForAssertion(() => Assert.NotEmpty(_interop.OverlayExitEndWirings));
        var callback = _interop.LastOverlayExitCallback!;
        Assert.NotNull(callback);

        // Dispose the whole subtree WHILE the exit is still pending (the page-navigation
        // scenario from the field report) — the fallback timer + _selfRef are torn down,
        // but the captured callback reference (standing in for an in-flight JS call) is
        // invoked anyway.
        cut.Render(p => p.Add(x => x.Show, false));

        var exception = await Record.ExceptionAsync(() => callback.OnExitAnimationEnd());
        Assert.Null(exception);

        // Idempotent: a second late call (e.g. both the JS callback and the fallback
        // timer landing after dispose) must also be a safe no-op.
        Assert.Null(await Record.ExceptionAsync(() => callback.OnExitAnimationEnd()));
    }

    private static RenderFragment DropdownMenuFragment(bool open) => builder =>
    {
        builder.OpenComponent<L.DropdownMenu>(0);
        builder.AddAttribute(1, "Open", open);
        builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
        {
            b.OpenComponent<L.DropdownMenuTrigger>(0);
            b.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "Menu")));
            b.CloseComponent();
            b.OpenComponent<L.DropdownMenuContent>(2);
            b.AddAttribute(3, "ChildContent", (RenderFragment)(c => c.AddContent(0, "items")));
            b.CloseComponent();
        }));
        builder.CloseComponent();
    };

    [Fact]
    public async Task DropdownMenu_ExitAnimationEnd_After_Dispose_Does_Not_Throw()
    {
        var cut = _ctx.Render<ConditionalRoot>(p => p
            .Add(x => x.Show, true)
            .AddChildContent(DropdownMenuFragment(open: true)));

        cut.Render(p => p.Add(x => x.Show, true).AddChildContent(DropdownMenuFragment(open: false)));
        cut.WaitForAssertion(() => Assert.NotEmpty(_interop.OverlayExitEndWirings));
        var callback = _interop.LastOverlayExitCallback!;
        Assert.NotNull(callback);

        cut.Render(p => p.Add(x => x.Show, false));

        var exception = await Record.ExceptionAsync(() => callback.OnExitAnimationEnd());
        Assert.Null(exception);
        Assert.Null(await Record.ExceptionAsync(() => callback.OnExitAnimationEnd()));
    }
}
