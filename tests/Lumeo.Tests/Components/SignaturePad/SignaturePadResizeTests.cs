using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.SignaturePad;

/// <summary>
/// Triage #120 (medium, state-on-data-change) — "Changing Width/Height after first
/// render blanks the canvas with no reload of the existing signature."
///
/// Re-emitting the <c>&lt;canvas&gt;</c> <c>width</c>/<c>height</c> attributes on a
/// runtime Width/Height change resets (blanks) the canvas drawing buffer. The
/// original component synced StrokeColor/StrokeWidth/Disabled/Value in
/// <c>OnAfterRenderAsync</c> but never tracked Width/Height, so after a resize the
/// just-drawn signature silently disappeared with no repaint.
///
/// The fix tracks <c>_lastWidth</c>/<c>_lastHeight</c> and, when either changes,
/// re-issues <c>SignaturePadLoadDataUrl(_canvasId, _currentValue)</c> so the existing
/// signature is repainted after the surface reset.
///
/// bUnit can't drive the real canvas buffer, so these tests assert the MECHANISM:
/// the repaint interop call (<c>SignaturePadLoadDataUrl</c>) fires with the live
/// value after a dimension change. They FAIL against the pre-fix component (which
/// never re-issued the load on resize) and PASS with the fix.
/// </summary>
public class SignaturePadResizeTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public SignaturePadResizeTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private const string DrawnUrl = "data:image/png;base64,DRAWN_SIGNATURE";

    /// <summary>
    /// After a signature is drawn, a runtime Width change must repaint the existing
    /// signature — the JS surface reset from the new width attribute would otherwise
    /// leave a blank canvas.
    /// </summary>
    [Fact]
    public async Task Width_Change_Repaints_Existing_Signature()
    {
        var cut = _ctx.Render<L.SignaturePad>(p => p
            .Add(s => s.Width, 500)
            .Add(s => s.Height, 200)
            .Add(s => s.Value, null));

        // User draws — the value now lives in the component's backing field.
        await cut.InvokeAsync(() => cut.Instance.OnStrokeEnded(DrawnUrl));

        // No repaint yet (no resize has happened).
        Assert.Empty(_interop.SignaturePadLoadCalls);

        // Parent changes the canvas Width at runtime: this re-emits the width
        // attribute and blanks the buffer, so the component must reload.
        cut.Render(p => p
            .Add(s => s.Width, 800)
            .Add(s => s.Height, 200)
            .Add(s => s.Value, null));

        // The fix repaints the existing (drawn) value after the surface reset.
        var load = Assert.Single(_interop.SignaturePadLoadCalls);
        Assert.Equal(DrawnUrl, load.DataUrl);
    }

    /// <summary>
    /// Same contract for a Height change.
    /// </summary>
    [Fact]
    public async Task Height_Change_Repaints_Existing_Signature()
    {
        var cut = _ctx.Render<L.SignaturePad>(p => p
            .Add(s => s.Width, 500)
            .Add(s => s.Height, 200)
            .Add(s => s.Value, null));

        await cut.InvokeAsync(() => cut.Instance.OnStrokeEnded(DrawnUrl));
        Assert.Empty(_interop.SignaturePadLoadCalls);

        cut.Render(p => p
            .Add(s => s.Width, 500)
            .Add(s => s.Height, 400)
            .Add(s => s.Value, null));

        var load = Assert.Single(_interop.SignaturePadLoadCalls);
        Assert.Equal(DrawnUrl, load.DataUrl);
    }

    /// <summary>
    /// Regression guard: an unrelated parent re-render that does NOT change
    /// Width/Height must not trigger a (destructive) reload — the resize repaint is
    /// gated strictly on a real dimension change.
    /// </summary>
    [Fact]
    public async Task Unrelated_ReRender_Without_Dimension_Change_Does_Not_Reload()
    {
        var cut = _ctx.Render<L.SignaturePad>(p => p
            .Add(s => s.Width, 500)
            .Add(s => s.Height, 200)
            .Add(s => s.Value, null));

        await cut.InvokeAsync(() => cut.Instance.OnStrokeEnded(DrawnUrl));

        // Re-render with the SAME dimensions (only an unrelated cosmetic param moves).
        cut.Render(p => p
            .Add(s => s.Width, 500)
            .Add(s => s.Height, 200)
            .Add(s => s.StrokeColor, "#ff0000")
            .Add(s => s.Value, null));

        Assert.Empty(_interop.SignaturePadLoadCalls);
    }
}
