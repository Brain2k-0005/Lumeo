using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.SignaturePad;

/// <summary>
/// Triage #15 (high, state-on-data-change) — "Parent re-render with one-way Value
/// (or a normalised round-trip) wipes the just-drawn signature and its vector data."
///
/// The original component wrote the JS-emitted data URL straight into the
/// <c>Value</c> [Parameter] from <c>OnStrokeEnded</c>, and the OnAfterRender sync
/// block reloaded the canvas whenever <c>_lastSyncedValue != Value</c>. With a
/// one-way binding (or an uncontrolled <c>Value="@x"</c> that stays null) the very
/// next parent re-render resets <c>Value</c> back to the consumer's value, the
/// equality check then fired, and <c>SignaturePadLoadDataUrl</c> wiped the freshly
/// drawn signature — the placeholder reappeared and the Clear button went disabled.
///
/// The fix keeps the drawn value in a private backing field (<c>_currentValue</c>)
/// instead of the parameter, and only reloads when the PARENT genuinely changes
/// <c>Value</c> (tracked via <c>_lastParamValue</c> / emitted-echo suppression).
///
/// bUnit can't drive the real canvas, but the C#-observable symptoms of the wipe
/// are the placeholder span and the Clear button's disabled state — both are
/// driven by the live value. These tests reproduce the exact sequence
/// (render -> OnStrokeEnded -> parent re-render with the old Value) and assert the
/// drawn state survived. They FAIL against the pre-fix component (which reverted to
/// the placeholder) and PASS with the controlled-parameter fix.
/// </summary>
public class SignaturePadValuePersistenceTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public SignaturePadValuePersistenceTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private const string DrawnUrl = "data:image/png;base64,DRAWN_SIGNATURE";

    /// <summary>
    /// One-way / uncontrolled binding: the consumer never writes the change back,
    /// so a follow-up parent re-render re-supplies the original (null) Value. The
    /// just-drawn signature must survive — placeholder hidden, Clear enabled.
    /// </summary>
    [Fact]
    public async Task OneWay_ReRender_With_Unchanged_Value_Keeps_Drawn_Signature()
    {
        // Render with a null Value (uncontrolled): placeholder is shown initially.
        var cut = _ctx.Render<L.SignaturePad>(p => p.Add(s => s.Value, null));
        Assert.Contains("Sign here", cut.Markup);
        Assert.True(cut.Find("button[aria-label='Clear signature']").HasAttribute("disabled"));

        // User draws — JS reports the stroke via the [JSInvokable] callback.
        await cut.InvokeAsync(() => cut.Instance.OnStrokeEnded(DrawnUrl));

        // The drawn signature is now live: placeholder gone, Clear enabled.
        Assert.DoesNotContain("Sign here", cut.Markup);
        Assert.False(cut.Find("button[aria-label='Clear signature']").HasAttribute("disabled"));

        // Parent re-renders and re-supplies the SAME (original null) Value — the
        // classic one-way / unrelated-re-render case. Pre-fix this reset Value to
        // null and reloaded the canvas, wiping the drawing.
        cut.Render(p => p.Add(s => s.Value, null));

        // Signature must STILL be present.
        Assert.DoesNotContain("Sign here", cut.Markup);
        Assert.False(cut.Find("button[aria-label='Clear signature']").HasAttribute("disabled"));
    }

    /// <summary>
    /// Controlled binding with a normalised round-trip: the parent echoes back the
    /// exact value we emitted. That must be recognised as our own round-trip and
    /// must NOT trigger a destructive raster reload (which would discard vector
    /// data); the signature stays present.
    /// </summary>
    [Fact]
    public async Task Controlled_Echo_Of_Emitted_Value_Keeps_Drawn_Signature()
    {
        string? bound = null;
        var cut = _ctx.Render<L.SignaturePad>(p => p
            .Add(s => s.Value, bound)
            .Add(s => s.ValueChanged, EventCallback.Factory.Create<string?>(this, v => bound = v)));

        await cut.InvokeAsync(() => cut.Instance.OnStrokeEnded(DrawnUrl));
        // The two-way callback captured the emitted value.
        Assert.Equal(DrawnUrl, bound);

        // Controlled parent pushes the captured value straight back in.
        cut.Render(p => p
            .Add(s => s.Value, bound)
            .Add(s => s.ValueChanged, EventCallback.Factory.Create<string?>(this, v => bound = v)));

        Assert.DoesNotContain("Sign here", cut.Markup);
        Assert.False(cut.Find("button[aria-label='Clear signature']").HasAttribute("disabled"));
    }

    /// <summary>
    /// Regression guard for the legitimate controlled path: when the PARENT
    /// genuinely changes Value to a brand-new external image, the component still
    /// adopts it (placeholder stays hidden, Clear stays enabled). This proves the
    /// fix suppresses only same-value/echo reloads, not real consumer-driven sets.
    /// </summary>
    [Fact]
    public void External_Value_Change_Is_Still_Adopted()
    {
        var cut = _ctx.Render<L.SignaturePad>(p => p.Add(s => s.Value, null));
        Assert.Contains("Sign here", cut.Markup);

        // Parent sets a real signature image from outside.
        cut.Render(p => p.Add(s => s.Value, "data:image/png;base64,EXTERNAL"));

        Assert.DoesNotContain("Sign here", cut.Markup);
        Assert.False(cut.Find("button[aria-label='Clear signature']").HasAttribute("disabled"));
    }
}
