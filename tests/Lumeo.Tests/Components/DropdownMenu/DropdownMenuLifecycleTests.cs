using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.DropdownMenu;

/// <summary>
/// Wave-2 lifecycle regression (n=78): DropdownMenuSub schedules a 200ms close via a
/// detached Task.Run that calls SetOpen on the component. The component declared NO
/// IDisposable/IAsyncDisposable, so when it was torn down while a close was pending the
/// timer fired InvokeAsync(SetOpen) on a disposed component (ObjectDisposedException)
/// and the CancellationTokenSource leaked.
///
/// The fix makes DropdownMenuSub @implements IDisposable; Dispose() cancels + disposes
/// the close CTS (via CancelClose) and the scheduled continuation swallows the teardown
/// race (ObjectDisposedException/TaskCanceledException). These tests fail on the pre-fix
/// source (no IDisposable, no CTS teardown) and pass with the fix.
/// </summary>
public class DropdownMenuLifecycleTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DropdownMenuLifecycleTests() => _ctx.AddLumeoServices();

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // A DropdownMenuSub containing only a sub-trigger. The trigger's mouseenter opens the
    // sub; its mouseleave (while open) calls SubContext.ScheduleClose(), arming the 200ms
    // close timer that the bug failed to cancel on dispose.
    private static RenderFragment Sub => b =>
    {
        b.OpenComponent<L.DropdownMenuSub>(0);
        b.AddAttribute(1, "ChildContent", (RenderFragment)(s =>
        {
            s.OpenComponent<L.DropdownMenuSubTrigger>(0);
            s.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "More")));
            s.CloseComponent();
        }));
        b.CloseComponent();
    };

    [Fact]
    public void DropdownMenuSub_Implements_IDisposable()
    {
        // The bug: the component declared no IDisposable/IAsyncDisposable at all, so its
        // close timer + CTS were never cleaned up on teardown. The fix adds IDisposable.
        var cut = _ctx.Render<L.DropdownMenuSub>(p => p
            .Add(s => s.ChildContent, (RenderFragment)(t => t.AddContent(0, "More"))));

        Assert.IsAssignableFrom<IDisposable>(cut.Instance);
    }

    [Fact]
    public void Disposing_While_A_Close_Is_Scheduled_Does_Not_Throw()
    {
        // Render an OPEN-able sub and arm its close timer through the real pointer path.
        var cut = _ctx.Render(Sub);
        var sub = cut.FindComponent<L.DropdownMenuSub>().Instance;

        // mouseenter opens the sub; mouseleave (while open) schedules the 200ms close.
        cut.Find("[role='menuitem']").MouseEnter();
        cut.Find("[role='menuitem']").MouseLeave();

        // Tear the component down while that close is still pending. Pre-fix there was no
        // Dispose to cancel the CTS (and no IDisposable to invoke); the timer then fired
        // SetOpen on the disposed component. The fix cancels the CTS in Dispose so this is
        // a clean, throw-free teardown.
        var exception = Record.Exception(() => ((IDisposable)sub).Dispose());

        Assert.Null(exception);

        // Double-dispose must also be safe (guarded by the _disposed flag / CancelClose
        // nulling the CTS) — teardown can run more than once.
        Assert.Null(Record.Exception(() => ((IDisposable)sub).Dispose()));
    }
}
