using System;
using System.Threading.Tasks;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.ContextMenu;

/// <summary>
/// Wave-2 lifecycle regression (triage #73): ContextMenuSub schedules a 200ms close via
/// a detached <c>Task.Run</c> (ScheduleClose -&gt; Task.Delay(200) -&gt;
/// <c>InvokeAsync(SetOpen)</c>). The component declared NO IDisposable/IAsyncDisposable,
/// so when it was torn down while a close was pending the timer fired
/// <c>InvokeAsync(StateHasChanged)</c> on a disposed component (ObjectDisposedException)
/// and the CancellationTokenSource leaked (post-dispose render + dangling CTS).
///
/// The fix makes ContextMenuSub <c>@implements IDisposable</c>; Dispose() cancels +
/// disposes the close CTS (via CancelClose) and the scheduled continuation swallows the
/// teardown race (TaskCanceledException / _disposed guard). These tests fail on the
/// pre-fix source (no IDisposable, no CTS teardown) and pass with the fix.
///
/// Mirrors DropdownMenuLifecycleTests, which fixed the identical bug (n=78) in the
/// sibling DropdownMenuSub.
/// </summary>
[Collection("UnobservedTaskException")] // serialized + isolated: see UnobservedTaskExceptionCollection
public class ContextMenuSubLifecycleTests
{
    // An open ContextMenu whose content hosts a ContextMenuSub. The sub-trigger's
    // mouseenter opens the sub; its mouseleave (while open) calls SubContext.ScheduleClose(),
    // arming the 200ms close timer that the bug failed to cancel on dispose.
    private static RenderFragment OpenMenuWithSub => builder =>
    {
        builder.OpenComponent<L.ContextMenu>(0);
        builder.AddAttribute(1, "Open", true);
        builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
        {
            b.OpenComponent<L.ContextMenuContent>(0);
            b.AddAttribute(1, "ChildContent", (RenderFragment)(content =>
            {
                content.OpenComponent<L.ContextMenuSub>(0);
                content.AddAttribute(1, "ChildContent", (RenderFragment)(sub =>
                {
                    sub.OpenComponent<L.ContextMenuSubTrigger>(0);
                    sub.AddAttribute(1, "ChildContent",
                        (RenderFragment)(t => t.AddContent(0, "More")));
                    sub.CloseComponent();

                    sub.OpenComponent<L.ContextMenuSubContent>(0);
                    sub.AddAttribute(1, "ChildContent", (RenderFragment)(sc =>
                    {
                        sc.OpenComponent<L.ContextMenuItem>(0);
                        sc.AddAttribute(1, "ChildContent",
                            (RenderFragment)(item => item.AddContent(0, "Sub Item")));
                        sc.CloseComponent();
                    }));
                    sub.CloseComponent();
                }));
                content.CloseComponent();
            }));
            b.CloseComponent();
        }));
        builder.CloseComponent();
    };

    [Fact]
    public async Task ContextMenuSub_Implements_IDisposable()
    {
        // The bug: the component declared no IDisposable/IAsyncDisposable at all, so its
        // close timer + CTS were never cleaned up on teardown. The fix adds IDisposable.
        var ctx = new BunitContext();
        ctx.AddLumeoServices();

        var cut = ctx.Render(OpenMenuWithSub);
        var sub = cut.FindComponent<L.ContextMenuSub>().Instance;

        Assert.IsAssignableFrom<IDisposable>(sub);

        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task Disposing_While_A_Close_Is_Scheduled_Does_Not_Throw()
    {
        var ctx = new BunitContext();
        ctx.AddLumeoServices();

        var cut = ctx.Render(OpenMenuWithSub);
        var sub = cut.FindComponent<L.ContextMenuSub>().Instance;

        // The sub-trigger renders a <button aria-haspopup="menu" role="menuitem">.
        // mouseenter opens the sub; mouseleave (while open) schedules the 200ms close.
        cut.Find("button[aria-haspopup='menu']").MouseEnter();
        cut.Find("button[aria-haspopup='menu']").MouseLeave();

        // Tear the component down while that close is still pending. Pre-fix there was no
        // Dispose to cancel the CTS (and no IDisposable to invoke); the timer then fired
        // SetOpen on the disposed component. The fix cancels the CTS in Dispose so this is
        // a clean, throw-free teardown.
        var exception = Record.Exception(() => ((IDisposable)sub).Dispose());
        Assert.Null(exception);

        // Double-dispose must also be safe (guarded by the _disposed flag / CancelClose
        // nulling the CTS) — teardown can run more than once.
        Assert.Null(Record.Exception(() => ((IDisposable)sub).Dispose()));

        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task Disposing_Sub_With_Pending_Close_Does_Not_Throw_Unobserved()
    {
        // End-to-end guard through the real renderer: arm the close timer, dispose the
        // whole context, and confirm the detached Task.Run continuation does NOT fault
        // with a post-dispose ObjectDisposedException (which surfaces as an
        // UnobservedTaskException). Pre-fix the CTS was never cancelled, so the timer ran
        // ~200ms after dispose and called StateHasChanged on a disposed renderer.
        var unobservedTcs = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);

        EventHandler<UnobservedTaskExceptionEventArgs> handler = (_, e) =>
        {
            e.SetObserved();
            unobservedTcs.TrySetResult(e.Exception);
        };

        TaskScheduler.UnobservedTaskException += handler;
        try
        {
            var ctx = new BunitContext();
            ctx.AddLumeoServices();

            var cut = ctx.Render(OpenMenuWithSub);
            cut.Find("button[aria-haspopup='menu']").MouseEnter();
            cut.Find("button[aria-haspopup='menu']").MouseLeave();

            await ctx.DisposeAsync();

            for (var i = 0; i < 3; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                await Task.Delay(120);
            }

            var winner = await Task.WhenAny(unobservedTcs.Task, Task.Delay(400));
            Assert.NotSame(unobservedTcs.Task, winner);
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= handler;
        }
    }
}
