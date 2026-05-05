using System;
using System.Threading.Tasks;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Alert;

/// <summary>
/// Covers the AutoDismiss timer/dispose race — the regression where the timer
/// callback would still fire OnDismiss + StateHasChanged after the test context
/// (and therefore the component) had already been disposed.
/// </summary>
public class AlertAutoDismissTests
{
    [Fact]
    public async Task Disposing_Before_AutoDismiss_Fires_Does_Not_Invoke_OnDismiss()
    {
        var ctx = new BunitContext();
        ctx.AddLumeoServices();

        var dismissCount = 0;

        ctx.Render<Lumeo.Alert>(p => p
            .Add(x => x.AutoDismissMs, 50)
            .Add(x => x.OnDismiss, EventCallback.Factory.Create(this, () => dismissCount++))
            .AddChildContent("Auto-dismiss test"));

        // Dispose the context (and therefore the Alert component) before the
        // AutoDismiss timeout elapses. The pre-fix timer would still run its
        // callback ~50ms later and call OnDismiss + StateHasChanged on the
        // disposed component.
        await ctx.DisposeAsync();

        // Wait past the original AutoDismiss timeout.
        await Task.Delay(150);

        Assert.Equal(0, dismissCount);
    }

    [Fact]
    public async Task Disposing_Before_AutoDismiss_Fires_Does_Not_Throw_Unobserved()
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
            var ctx = new BunitContext();
            ctx.AddLumeoServices();

            ctx.Render<Lumeo.Alert>(p => p
                .Add(x => x.AutoDismissMs, 50)
                .AddChildContent("Auto-dismiss test"));

            await ctx.DisposeAsync();

            for (var i = 0; i < 3; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                await Task.Delay(80);
            }

            var winner = await Task.WhenAny(unobservedTcs.Task, Task.Delay(300));
            Assert.NotSame(unobservedTcs.Task, winner);
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= handler;
        }
    }

    [Fact]
    public async Task AutoDismiss_Still_Invokes_OnDismiss_Without_Dispose()
    {
        var ctx = new BunitContext();
        ctx.AddLumeoServices();

        var dismissCount = 0;

        var cut = ctx.Render<Lumeo.Alert>(p => p
            .Add(x => x.AutoDismissMs, 30)
            .Add(x => x.OnDismiss, EventCallback.Factory.Create(this, () => dismissCount++))
            .AddChildContent("Auto-dismiss test"));

        // Wait long enough for the timer to fire.
        await Task.Delay(200);
        cut.WaitForState(() => dismissCount == 1, TimeSpan.FromSeconds(2));

        Assert.Equal(1, dismissCount);

        await ctx.DisposeAsync();
    }
}
