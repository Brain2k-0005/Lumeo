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
[Collection("UnobservedTaskException")] // serialized + isolated: see UnobservedTaskExceptionCollection
public class SafeAsyncDispatcherTests
{
    [Fact]
    public async Task FireAndForget_Does_Not_Surface_UnobservedTaskException_When_InvokeAsync_Throws_Synchronously()
    {
        // TaskScheduler.UnobservedTaskException is PROCESS-WIDE; under the parallel
        // test run other tests' faulted fire-and-forget tasks also raise it. Tag this
        // test's own exception with a unique marker and only record THAT one — without
        // this, a foreign unobserved exception landing in the GC window trips the
        // assertion (the flake). We still SetObserved() everything to keep the process
        // alive, exactly as before.
        var marker = "SAD-sync-" + Guid.NewGuid().ToString("N");
        var unobservedTcs = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);

        EventHandler<UnobservedTaskExceptionEventArgs> handler = (_, e) =>
        {
            e.SetObserved();
            if (e.Exception?.ToString().Contains(marker) == true)
                unobservedTcs.TrySetResult(e.Exception);
        };

        TaskScheduler.UnobservedTaskException += handler;
        try
        {
            // invokeAsync that throws synchronously (mimics InvokeAsync called on a
            // disposed renderer/circuit). The pre-fix code path would create an
            // unobserved Task here.
            Func<Func<Task>, Task> faultingInvoke = _ => throw new ObjectDisposedException(marker);

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

            // If we get OUR marked exception here, the dispatcher leaked it (test fails).
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
        // Process-wide event (see the sync test above): scope to this test's own
        // exception via a unique marker so a parallel test's unobserved fault can't
        // trip the assertion.
        var marker = "SAD-faulted-" + Guid.NewGuid().ToString("N");
        var unobservedTcs = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);

        EventHandler<UnobservedTaskExceptionEventArgs> handler = (_, e) =>
        {
            e.SetObserved();
            if (e.Exception?.ToString().Contains(marker) == true)
                unobservedTcs.TrySetResult(e.Exception);
        };

        TaskScheduler.UnobservedTaskException += handler;
        try
        {
            // Faulted task return — same shape as a renderer that's already disposed.
            Func<Func<Task>, Task> faultingInvoke = _ =>
                Task.FromException(new ObjectDisposedException(marker));

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
