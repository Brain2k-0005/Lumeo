using Bunit;
using Lumeo.Services;
using Xunit;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using L = Lumeo;

namespace Lumeo.Tests.Components.BackToTop;

/// <summary>
/// Reality check against the gap-scan assumption: BackToTopBehaviorTests already pins
/// both requested keyboard angles — "Clicking_Invokes_ScrollToTop_Interop" (the native
/// button's default Enter/Space-synthesized click, the exact handler a keydown would
/// run) and "Hidden_Until_Visibility_Event_Marks_It_Visible" (no button — hence no
/// phantom tab stop — before the visibility callback fires). This file adds the one
/// keyboard-specific angle neither of those covers: once visible, the button carries no
/// tabindex override, so native Tab genuinely reaches it rather than merely existing in
/// the DOM.
/// </summary>
public class BackToTopKeyboardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public BackToTopKeyboardTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public async Task Visible_Button_Carries_No_Tabindex_Override_So_Native_Tab_Reaches_It()
    {
        var cut = _ctx.Render<L.BackToTop>();
        var reg = Assert.Single(_ctx.JSInterop.Invocations, i => i.Identifier == "registerBackToTop");
        var id = Assert.IsType<string>(reg.Arguments[0]);
        var interop = _ctx.Services.GetRequiredService<ComponentInteropService>();

        await cut.InvokeAsync(() => interop.OnScrollVisibilityChanged(id, true));

        var button = cut.Find("button");
        Assert.False(button.HasAttribute("tabindex"));
        Assert.False(button.HasAttribute("disabled"));
    }
}
