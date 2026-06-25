using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.SignaturePad;

/// <summary>
/// Triage #205 (low, keyboard-a11y) — "Clearing the pad does not return focus to
/// the wrapper / does not move focus off the now-disabled Clear button."
///
/// Activating the Clear button via the keyboard clears the pad, which disables the
/// Clear button (it's empty now). A disabled control can't hold focus, so the
/// browser drops keyboard focus to &lt;body&gt; — the pad becomes effectively
/// keyboard-stranded. The fix gives the labelled wrapper region a stable id and
/// has <c>ClearAsync</c> call <c>Interop.FocusElement(wrapperId)</c> after clearing
/// so focus is restored to the region.
///
/// bUnit can't move real DOM focus, so these tests assert the MECHANISM: the
/// component issues a <c>FocusElement</c> interop call targeting the wrapper's id
/// after a clear. They FAIL against the pre-fix component (which never refocused)
/// and PASS with the fix.
/// </summary>
public class SignaturePadClearFocusTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public SignaturePadClearFocusTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private const string DrawnUrl = "data:image/png;base64,AAAA";

    /// <summary>
    /// Activating the Clear button (which then becomes disabled) restores focus to
    /// the labelled wrapper region so keyboard focus isn't dropped to body.
    /// </summary>
    [Fact]
    public async Task Clear_Button_Restores_Focus_To_Wrapper_Region()
    {
        var cut = _ctx.Render<L.SignaturePad>(p => p.Add(s => s.Value, DrawnUrl));

        // The wrapper region carries a stable id that focus is restored to.
        var region = cut.Find("[role='group']");
        var regionId = region.GetAttribute("id");
        Assert.False(string.IsNullOrEmpty(regionId));

        // No focus call before the clear.
        Assert.DoesNotContain(regionId!, _interop.FocusElementCalls);

        // Activate the Clear button (the keyboard/click activation path).
        await cut.InvokeAsync(() => cut.Instance.ClearAsync());

        // The fix refocuses the wrapper region after clearing.
        Assert.Contains(regionId!, _interop.FocusElementCalls);
    }

    /// <summary>
    /// The Escape/Delete keyboard clear path also lands focus back on the wrapper
    /// region — clearing via any path keeps the pad keyboard-operable.
    /// </summary>
    [Fact]
    public void Escape_Clear_Restores_Focus_To_Wrapper_Region()
    {
        string? bound = DrawnUrl;
        var cut = _ctx.Render<L.SignaturePad>(p => p
            .Add(s => s.Value, bound)
            .Add(s => s.ValueChanged, EventCallback.Factory.Create<string?>(this, v => bound = v)));

        var regionId = cut.Find("[role='group']").GetAttribute("id");
        Assert.False(string.IsNullOrEmpty(regionId));

        cut.Find("[role='group']").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Contains(regionId!, _interop.FocusElementCalls);
    }
}
