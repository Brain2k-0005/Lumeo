using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.OtpInput;

/// <summary>
/// Battle-wave-2 triage LIFECYCLE bugs for OtpInput:
///
/// #42 (medium) — the paste interop (`RegisterOtpPaste`) was wired only on the
/// FIRST render, and the JS `paste` listener attaches to exactly the
/// `${baseId}-0..length-1` cells that exist at registration time. A runtime
/// Length change therefore left the old listener bound to the now-removed cells
/// (a leak) and never wired the new cells. The fix tracks the registered length
/// and, when Length changes, unregisters the old span then registers the new one;
/// DisposeAsync unregisters against the LAST-registered length, not the live one.
///
/// #157 (low) — DisposeAsync only swallowed JSDisconnectedException, asymmetric
/// with OnAfterRenderAsync which also swallows ObjectDisposedException; so a
/// teardown that raced a circuit/prerender disposal could throw. The fix mirrors
/// the OnAfterRenderAsync catch set.
///
/// JS interop is mocked, so these tests assert the RECORDED interop calls
/// (TrackingInteropService.OtpPasteRegistrations / OtpPasteUnregistrations) and
/// the absence of a throw on dispose (Record.ExceptionAsync). They fail against
/// the pre-fix source (one-shot firstRender registration; dispose unregistering
/// against the live Length; no ObjectDisposedException catch).
/// </summary>
public class OtpInputPasteLifecycleTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public OtpInputPasteLifecycleTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // The per-instance OTP base id is internal (LumeoIds.New("otp")); recover it
    // from a rendered cell's id, which is "{baseId}-{index}".
    private static string BaseIdOf(IRenderedComponent<L.OtpInput> cut)
    {
        var id = cut.FindAll("input")[0].GetAttribute("id")!;
        return id[..id.LastIndexOf('-')];
    }

    // ──────────────────────────────────────────────────────────────────────────
    // #42 — first render registers the paste listener exactly once, against the
    // initial Length.
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void First_Render_Registers_Paste_Listener_Once_For_Initial_Length()
    {
        var cut = _ctx.Render<L.OtpInput>(p => p.Add(c => c.Length, 4));

        var baseId = BaseIdOf(cut);
        Assert.Single(_interop.OtpPasteRegistrations);
        Assert.Equal((baseId, 4), _interop.OtpPasteRegistrations[0]);
        Assert.Empty(_interop.OtpPasteUnregistrations);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // #42 — the core fix: a runtime Length change re-wires the paste listener.
    // The old span (length 4) is unregistered and the new span (length 6)
    // registered, against the SAME base id. Pre-fix (firstRender-only) the
    // registration latched at 4 and never re-ran, so the new cells had no
    // listener and the old one leaked.
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Changing_Length_At_Runtime_Reregisters_Paste_Listener_For_New_Length()
    {
        var cut = _ctx.Render<L.OtpInput>(p => p.Add(c => c.Length, 4));
        var baseId = BaseIdOf(cut);

        cut.Render(p => p.Add(c => c.Length, 6));

        // Old span torn down, new span wired — both against the same base id.
        Assert.Contains((baseId, 4), _interop.OtpPasteUnregistrations);
        Assert.Contains((baseId, 6), _interop.OtpPasteRegistrations);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // #42 — regression guard: a re-render that does NOT change Length must not
    // re-register (no churn / no duplicate listeners).
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Rerender_Without_Length_Change_Does_Not_Reregister()
    {
        var cut = _ctx.Render<L.OtpInput>(p => p
            .Add(c => c.Length, 4)
            .Add(c => c.Value, "1"));

        cut.Render(p => p
            .Add(c => c.Length, 4)
            .Add(c => c.Value, "12"));

        Assert.Single(_interop.OtpPasteRegistrations);
        Assert.Empty(_interop.OtpPasteUnregistrations);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // #42 (dispose tail) — DisposeAsync unregisters against the LAST-registered
    // length, not the live Length. After growing 4 → 6, disposing must
    // unregister the length-6 span. (Pre-fix the dispose used the live Length,
    // which happened to match here, but the same-instance contract is that
    // dispose tears down exactly what was registered.)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Dispose_Unregisters_Against_Last_Registered_Length()
    {
        var cut = _ctx.Render<L.OtpInput>(p => p.Add(c => c.Length, 4));
        var baseId = BaseIdOf(cut);

        cut.Render(p => p.Add(c => c.Length, 6));

        await cut.Instance.DisposeAsync();

        // The terminal unregister tears down the length-6 span that was last
        // registered.
        Assert.Equal((baseId, 6), _interop.OtpPasteUnregistrations[^1]);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // #157 — DisposeAsync swallows ObjectDisposedException raised by the interop
    // teardown (mirroring OnAfterRenderAsync), so a circuit/prerender disposal
    // race never surfaces a throw. The unregister was still ATTEMPTED.
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Dispose_Swallows_ObjectDisposedException_From_Interop_Teardown()
    {
        var cut = _ctx.Render<L.OtpInput>(p => p.Add(c => c.Length, 6));

        _interop.ThrowObjectDisposedOnUnregisterOtpPaste = true;

        var ex = await Record.ExceptionAsync(() => cut.Instance.DisposeAsync().AsTask());

        Assert.Null(ex);
        // The teardown path ran (and would have thrown without the catch).
        Assert.NotEmpty(_interop.OtpPasteUnregistrations);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // #157 (idempotence) — disposing twice is a clean no-op: the second dispose
    // skips the unregister (the registered-length guard is reset to -1) and does
    // not throw.
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Disposing_Twice_Unregisters_Once_And_Does_Not_Throw()
    {
        var cut = _ctx.Render<L.OtpInput>(p => p.Add(c => c.Length, 6));

        await cut.Instance.DisposeAsync();
        Assert.Single(_interop.OtpPasteUnregistrations);

        var ex = await Record.ExceptionAsync(() => cut.Instance.DisposeAsync().AsTask());

        Assert.Null(ex);
        Assert.Single(_interop.OtpPasteUnregistrations);
    }
}
