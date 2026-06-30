using System.Reflection;
using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Menubar;

/// <summary>
/// Battle-test #62 (lifecycle): <c>MenubarSub</c> schedules a deferred close via a
/// background <c>Task.Run</c> + <c>Task.Delay(200)</c> that ends in
/// <c>InvokeAsync(SetOpen(false))</c> (which calls <c>StateHasChanged</c>). Before the
/// fix, <c>MenubarSub</c> did NOT implement <see cref="IDisposable"/>, so when the
/// component was unmounted mid-delay the pending timer (its CancellationTokenSource +
/// background Task) leaked, fired after 200ms, and called <c>StateHasChanged</c> on a
/// disposed component — faulting the background task with an
/// <see cref="ObjectDisposedException"/>.
///
/// The fix adds <c>@implements IDisposable</c> + a <c>Dispose()</c> that calls
/// <c>CancelClose()</c> (which cancels + disposes the CTS and nulls the
/// <c>_closeCts</c> field), and wraps the continuation's <c>InvokeAsync</c> in a
/// <c>try/catch (ObjectDisposedException)</c> so any surviving race is swallowed
/// cleanly. These tests reproduce the exact unmount-mid-delay repro.
/// </summary>
public class MenubarSubDisposeTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public MenubarSubDisposeTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    /// <summary>
    /// Child that captures the cascaded <see cref="L.MenubarSub.MenubarSubContext"/>
    /// so the test can drive SetOpen/ScheduleClose exactly the way
    /// MenubarSubTrigger / MenubarSubContent do (open on enter, ScheduleClose on
    /// mouse-leave) without standing up the full positioned submenu DOM.
    /// </summary>
    private sealed class SubContextProbe : ComponentBase
    {
        [CascadingParameter] public L.MenubarSub.MenubarSubContext? Captured { get; set; }

        public L.MenubarSub.MenubarSubContext Context =>
            Captured ?? throw new InvalidOperationException("Sub context not cascaded");
    }

    /// <summary>
    /// Host that conditionally renders a single <see cref="L.MenubarSub"/> (gated by
    /// <see cref="ShowSub"/>) containing the capturing probe. Re-rendering the host
    /// with ShowSub=false unmounts — and disposes — the MenubarSub.
    /// </summary>
    private sealed class SubVisibilityProbe : ComponentBase
    {
        [Parameter] public bool ShowSub { get; set; } = true;

        protected override void BuildRenderTree(
            Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            if (!ShowSub) return;

            builder.OpenComponent<L.MenubarSub>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(sub =>
            {
                sub.OpenComponent<SubContextProbe>(0);
                sub.CloseComponent();
            }));
            builder.CloseComponent();
        }
    }

    private static CancellationTokenSource? GetCloseCts(L.MenubarSub sub)
    {
        var field = typeof(L.MenubarSub).GetField(
            "_closeCts", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (CancellationTokenSource?)field!.GetValue(sub);
    }

    [Fact]
    public async Task Unmounting_MenubarSub_While_Close_Timer_Is_Pending_Cancels_The_Timer()
    {
        var cut = _ctx.Render<SubVisibilityProbe>(p => p.Add(x => x.ShowSub, true));
        var sub = cut.FindComponent<L.MenubarSub>().Instance;
        var ctx = cut.FindComponent<SubContextProbe>().Instance.Context;

        // Open the submenu (so a later SetOpen(false) would actually flip state and
        // call StateHasChanged), then schedule the deferred close — exactly the
        // sequence MenubarSubTrigger/Content drive on mouse-enter then mouse-leave.
        await cut.InvokeAsync(() => ctx.SetOpen.InvokeAsync(true));
        await cut.InvokeAsync(() => ctx.ScheduleClose());

        // The close timer is now live: its CancellationTokenSource is allocated and
        // un-cancelled. (Guards the repro precondition.)
        var ctsBefore = GetCloseCts(sub);
        Assert.NotNull(ctsBefore);
        Assert.False(ctsBefore!.IsCancellationRequested);

        // Unmount the MenubarSub WHILE its 200ms close timer is still pending.
        cut.Render(p => p.Add(x => x.ShowSub, false));
        Assert.Empty(cut.FindComponents<SubContextProbe>());

        // With the fix, Dispose() -> CancelClose() cancelled + disposed the CTS and
        // nulled _closeCts, so the continuation returns early and never calls
        // StateHasChanged on the disposed component. Without the fix MenubarSub has
        // no Dispose, so _closeCts stays non-null and the timer survives to fire.
        var ctsAfter = GetCloseCts(sub);
        Assert.Null(ctsAfter);
        Assert.True(ctsBefore.IsCancellationRequested,
            "Dispose() should have cancelled the pending close timer's token.");
    }

    [Fact]
    public async Task Tearing_Down_The_Context_While_Close_Timer_Is_Pending_Cancels_The_Timer()
    {
        // Render the MenubarSub directly and tear down the whole bUnit context to
        // exercise teardown — a component's root need not be disposable, so disposing
        // the context is the idiomatic way to drive child teardown.
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.MenubarSub>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(sub =>
            {
                sub.OpenComponent<SubContextProbe>(0);
                sub.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var sub = cut.FindComponent<L.MenubarSub>().Instance;
        var ctx = cut.FindComponent<SubContextProbe>().Instance.Context;
        await cut.InvokeAsync(() => ctx.SetOpen.InvokeAsync(true));
        await cut.InvokeAsync(() => ctx.ScheduleClose());

        var ctsBefore = GetCloseCts(sub);
        Assert.NotNull(ctsBefore);
        Assert.False(ctsBefore!.IsCancellationRequested);

        // Tear down the whole context (idempotent) WHILE the 200ms timer is pending.
        // Disposing the context disposes every rendered component, invoking
        // MenubarSub.Dispose -> CancelClose. Without the fix MenubarSub has no
        // Dispose and the leaked CTS is never cancelled.
        await _ctx.DisposeAsync();

        Assert.Null(GetCloseCts(sub));
        Assert.True(ctsBefore.IsCancellationRequested,
            "Context teardown should have cancelled the pending close timer's token.");
    }
}
