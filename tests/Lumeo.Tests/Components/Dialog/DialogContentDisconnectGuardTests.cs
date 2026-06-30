using System.Reflection;
using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Dialog;

/// <summary>
/// #176 (battle-wave2, lifecycle) — DialogContent.OnAfterRenderAsync opens the
/// dialog by calling <c>Interop.LockScroll()</c> + <c>Interop.SetupFocusTrap()</c>.
/// If the Blazor circuit disconnects in the window between renders (e.g. the user
/// navigates away while the dialog is opening), those interop calls throw
/// <see cref="JSDisconnectedException"/>. Pre-fix the open branch was unguarded —
/// only <c>Cleanup()</c> caught the disconnect — so a disconnect during open
/// surfaced as an unhandled exception. The fix wraps the open-branch interop in
/// the same <c>try/catch (JSDisconnectedException)</c> Cleanup() already uses, so
/// a disconnect during setup degrades gracefully.
/// </summary>
public class DialogContentDisconnectGuardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _inner = new();
    private readonly IComponentInteropService _interop;

    public DialogContentDisconnectGuardTests()
    {
        // An interop facade that forwards every call to the real tracking fake
        // EXCEPT the two dialog-open calls (LockScroll / SetupFocusTrap), which
        // throw JSDisconnectedException to model a circuit dropped mid-open.
        _interop = ThrowingInteropProxy.Create(_inner, throwOn: new[] { "LockScroll", "SetupFocusTrap" });

        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.Dialog> RenderOpenDialog() =>
        _ctx.Render<L.Dialog>(p => p
            .Add(d => d.Open, true)
            .AddChildContent<L.DialogContent>(cp => cp.AddChildContent("Body")));

    [Fact]
    public void Opening_Dialog_Swallows_JSDisconnectedException_From_Open_Branch_Interop()
    {
        // Rendering with Open=true runs OnAfterRenderAsync's open branch, which
        // calls LockScroll()+SetupFocusTrap(). Both throw JSDisconnectedException
        // here. Pre-fix this propagated out of the render; with the guard it is
        // swallowed and the render completes cleanly.
        IRenderedComponent<L.Dialog>? cut = null;
        var ex = Record.Exception(() => cut = RenderOpenDialog());

        Assert.Null(ex);
        // The dialog surface still rendered despite the failed open-branch interop.
        Assert.NotNull(cut);
        Assert.NotEmpty(cut!.FindAll("[role='dialog']"));
    }

    [Fact]
    public async Task Disconnect_During_Open_Does_Not_Throw_On_Subsequent_Dispose()
    {
        // After a disconnect during open, tearing the component down must also
        // not throw (Cleanup is already guarded, and _wasOpen was never latched
        // because the disconnect aborted before _wasOpen = true).
        _ = RenderOpenDialog();

        // Tear the whole render tree down — this disposes DialogContent, exercising
        // its Cleanup() teardown after the disconnect-during-open. (Dialog itself is
        // not IAsyncDisposable; the guarded cleanup lives in DialogContent.) bUnit's
        // DisposeAsync is idempotent, so the IAsyncLifetime teardown is a safe no-op.
        var ex = await Record.ExceptionAsync(async () => await _ctx.DisposeAsync());

        Assert.Null(ex);
    }

    /// <summary>
    /// Runtime-generated <see cref="IComponentInteropService"/> that forwards to
    /// an inner instance but throws <see cref="JSDisconnectedException"/> for a
    /// named set of methods. Using a DispatchProxy keeps the double resilient to
    /// the (large) interface growing, without touching the shared
    /// TrackingInteropService helper.
    /// </summary>
    public class ThrowingInteropProxy : DispatchProxy
    {
        private IComponentInteropService _target = default!;
        private HashSet<string> _throwOn = default!;

        public static IComponentInteropService Create(IComponentInteropService target, string[] throwOn)
        {
            var proxy = DispatchProxy.Create<IComponentInteropService, ThrowingInteropProxy>();
            var tp = (ThrowingInteropProxy)(object)proxy;
            tp._target = target;
            tp._throwOn = new HashSet<string>(throwOn, StringComparer.Ordinal);
            return proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod is not null && _throwOn.Contains(targetMethod.Name))
                throw new JSDisconnectedException($"circuit dropped during {targetMethod.Name}");

            return targetMethod!.Invoke(_target, args);
        }
    }
}
