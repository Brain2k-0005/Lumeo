using System;
using System.Threading.Tasks;
using Lumeo.Services;
using Xunit;

namespace Lumeo.Tests.Services;

/// <summary>
/// Covers SafeAsyncDispatcher.FireAndForget — specifically the regression where the
/// outer InvokeAsync(...) Task was unobserved when the dispatcher itself faulted
/// synchronously (e.g. circuit/renderer disposed before the work delegate ran).
/// </summary>
public class SafeAsyncDispatcherTests
{
    [Fact]
    public async Task FireAndForget_Does_Not_Surface_UnobservedTaskException_When_InvokeAsync_Throws_Synchronously()
    {
        var unobservedTcs = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);

        EventHandler<UnobservedTaskExceptionEventArgs> handler = (_, e) =>
        {
            // Mark as observed so the test process doesn't crash.
            e.SetObserved();
            unobservedTcs.TrySetResult(e.Exception);
        };

        TaskScheduler.UnobservedTaskException += handler;
        try
        {
            // invokeAsync that throws synchronously (mimics InvokeAsync called on a
            // disposed renderer/circuit). The pre-fix code path would create an
            // unobserved Task here.
            Func<Func<Task>, Task> faultingInvoke = _ => throw new ObjectDisposedException("Renderer");

            SafeAsyncDispatcher.FireAndForget(
                faultingInvoke,
                () => Task.CompletedTask,
                "Lumeo.Tests.SafeAsyncDispatcher");

            // Force GC to make the runtime walk Task finalizers — that's how
            // unobserved exceptions get raised.
            for (var i = 0; i < 3; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                await Task.Delay(50);
            }

            // If we get a result here, an unobserved exception fired (test fails).
            // We expect a timeout instead — meaning the dispatcher swallowed it.
            var winner = await Task.WhenAny(unobservedTcs.Task, Task.Delay(500));
            Assert.NotSame(unobservedTcs.Task, winner);
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= handler;
        }
    }

    [Fact]
    public async Task FireAndForget_Does_Not_Surface_UnobservedTaskException_When_InvokeAsync_Returns_Faulted_Task()
    {
        var unobservedTcs = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);

        EventHandler<UnobservedTaskExceptionEventArgs> handler = (_, e) =>
        {
            e.SetObserved();
            unobservedTcs.TrySetResult(e.Exception);
        };

        TaskScheduler.UnobservedTaskException += handler;
        try
        {
            // Faulted task return — same shape as a renderer that's already disposed.
            Func<Func<Task>, Task> faultingInvoke = _ =>
                Task.FromException(new ObjectDisposedException("Renderer"));

            SafeAsyncDispatcher.FireAndForget(
                faultingInvoke,
                () => Task.CompletedTask,
                "Lumeo.Tests.SafeAsyncDispatcher");

            for (var i = 0; i < 3; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                await Task.Delay(50);
            }

            var winner = await Task.WhenAny(unobservedTcs.Task, Task.Delay(500));
            Assert.NotSame(unobservedTcs.Task, winner);
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= handler;
        }
    }

    [Fact]
    public async Task FireAndForget_Still_Runs_Work_When_InvokeAsync_Is_Healthy()
    {
        var ranTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Healthy invokeAsync — just executes the delegate inline.
        Func<Func<Task>, Task> healthyInvoke = fn => fn();

        SafeAsyncDispatcher.FireAndForget(
            healthyInvoke,
            () =>
            {
                ranTcs.TrySetResult(true);
                return Task.CompletedTask;
            },
            "Lumeo.Tests.SafeAsyncDispatcher");

        var winner = await Task.WhenAny(ranTcs.Task, Task.Delay(1000));
        Assert.Same(ranTcs.Task, winner);
        Assert.True(await ranTcs.Task);
    }
}
