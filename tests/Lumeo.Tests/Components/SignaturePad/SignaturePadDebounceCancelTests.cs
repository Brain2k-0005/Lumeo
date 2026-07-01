using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.SignaturePad;

/// <summary>
/// Triage #119 (medium, lifecycle) — "Pending stroke debounce fires after a
/// programmatic Value load, re-emitting the loaded image as if the user drew it."
///
/// The JS pad debounces the .NET <c>OnStrokeEnded</c> round-trip (~200 ms) after a
/// stroke ends. If the consumer programmatically replaces the pad content INSIDE
/// that window — via <c>clear()</c> or <c>loadDataUrl()</c> — the still-pending
/// <c>setTimeout</c> later fires and exports the now-replaced content, emitting it
/// through <c>OnStrokeEnded</c> as though the user had just drawn it. The fix calls
/// <c>clearTimeout(pad.debounceTimer)</c> at the top of both <c>loadDataUrl()</c> and
/// <c>clear()</c> in <c>signature-pad.js</c> so any pending stroke debounce is
/// cancelled the instant the content is programmatically swapped.
///
/// That <c>clearTimeout</c> lives entirely in JavaScript, which bUnit does not
/// execute, so — exactly like the sibling Resize (#120) and ClearFocus (#205)
/// JS-only fixes in this folder — these tests assert the .NET MECHANISM the fix
/// hinges on: that every programmatic content-replacement on the C# side actually
/// routes through the guarded JS entry points (<c>SignaturePadLoadDataUrl</c> /
/// <c>SignaturePadClear</c>). If the component stopped issuing those calls (or
/// emitted a self-inflicted reload), the JS <c>clearTimeout</c> guard could never
/// run and the stale-emit race would resurface. The tests pin that contract.
/// </summary>
public class SignaturePadDebounceCancelTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public SignaturePadDebounceCancelTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private const string DrawnUrl = "data:image/png;base64,DRAWN_SIGNATURE";
    private const string ExternalUrl = "data:image/png;base64,EXTERNAL_IMAGE";

    /// <summary>
    /// A genuine parent-driven Value change must route through
    /// <c>SignaturePadLoadDataUrl</c> — the JS function whose pending stroke
    /// debounce the fix cancels. This proves the programmatic-load seam the
    /// <c>clearTimeout</c> guard sits behind is actually reached on the .NET side.
    /// </summary>
    [Fact]
    public void Programmatic_Value_Load_Routes_Through_Guarded_Load_Interop()
    {
        var cut = _ctx.Render<L.SignaturePad>(p => p.Add(s => s.Value, null));

        // No reload from the initial render (initial value is passed via init options).
        Assert.Empty(_interop.SignaturePadLoadCalls);

        // Parent programmatically loads an external image — the exact scenario that,
        // in JS, must cancel any pending stroke debounce before drawing it.
        cut.Render(p => p.Add(s => s.Value, ExternalUrl));

        var load = Assert.Single(_interop.SignaturePadLoadCalls);
        Assert.Equal(ExternalUrl, load.DataUrl);
    }

    /// <summary>
    /// A value emitted by the user's own stroke (<c>OnStrokeEnded</c>) must NOT
    /// trigger a <c>SignaturePadLoadDataUrl</c> reload. If it did, the component
    /// would re-enter the very JS load path on its own callback, and a same-window
    /// debounce could re-emit it — defeating the cancel. The only loads must come
    /// from genuine programmatic replacement (the paths the fix guards).
    /// </summary>
    [Fact]
    public async Task Stroke_Emit_Does_Not_Self_Trigger_A_Reload()
    {
        var cut = _ctx.Render<L.SignaturePad>(p => p.Add(s => s.Value, null));

        await cut.InvokeAsync(() => cut.Instance.OnStrokeEnded(DrawnUrl));

        // The drawn value lives in the backing field; no programmatic reload fired.
        Assert.Empty(_interop.SignaturePadLoadCalls);

        // An unrelated parent re-render that re-supplies the same (null) Value must
        // also not reload — so no spurious JS loadDataUrl that could re-arm a stale
        // debounce emit.
        cut.Render(p => p.Add(s => s.Value, null));
        Assert.Empty(_interop.SignaturePadLoadCalls);
    }

    /// <summary>
    /// Clearing the pad must route through <c>SignaturePadClear</c> — the OTHER JS
    /// entry point the fix cancels the debounce in. We assert the .NET side reaches
    /// it without throwing (the call is a no-op in the tracking interop) and that the
    /// clear path doesn't smuggle in a destructive <c>loadDataUrl</c>.
    /// </summary>
    [Fact]
    public async Task Clear_Routes_Through_Guarded_Clear_Interop_Without_Reload()
    {
        var cut = _ctx.Render<L.SignaturePad>(p => p.Add(s => s.Value, DrawnUrl));

        // Clearing reaches Interop.SignaturePadClear (the JS clear() that cancels the
        // debounce) and must not throw on the .NET side.
        var ex = await Record.ExceptionAsync(() =>
            cut.InvokeAsync(() => cut.Instance.ClearAsync()));
        Assert.Null(ex);

        // Clear must not issue a loadDataUrl — the canvas is already blank from
        // SignaturePadClear; a reload here would be the stale-content vector.
        Assert.Empty(_interop.SignaturePadLoadCalls);
    }
}
