using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Lumeo.Tests.Components.NavigationMenu;

/// <summary>
/// Round-8 (Codex) — the round-6/7 stale-intent fix OVERCORRECTED. In
/// SetActiveItemId's controlled branch the clear-check ran right after
/// ValueChanged.InvokeAsync, BEFORE the parent pushed the new Value back through
/// OnParametersSet, so _open still held the OLD value. `_open != value` was
/// therefore true even when the parent ACCEPTED the open, and the deferred
/// keyboard focus intent was cleared — so an accepted controlled ArrowDown open
/// no longer moved focus into the content.
///
/// The fix resolves the intent at ADOPTION time (OnParametersSet): keep it when
/// the adopted Value matches the latched item (accept → focus fires), clear it
/// when adoption goes elsewhere / null (veto). The veto leg is covered by
/// <see cref="NavigationMenuStaleFocusIntentTests"/> (must stay green); this class
/// covers the accepted leg (pre-fix failing).
/// </summary>
public class NavigationMenuAcceptedFocusIntentTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public NavigationMenuAcceptedFocusIntentTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Accepted_Controlled_Keyboard_Open_Moves_Focus_Into_The_Content()
    {
        // An ACCEPTING controlled parent (@bind-Value) pushes the emitted value
        // back, so the ArrowDown open is adopted.
        var cut = _ctx.Render<NavigationMenuControlledFocusHost>();

        cut.Find("button").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });

        // The submenu opened (parent adopted "products")...
        Assert.Contains("Products content", cut.Markup);
        var contentId = cut.Find("[role='menu']").GetAttribute("id");

        // ...and focus moved INTO the content. Pre-fix the intent was cleared right
        // after ValueChanged (while _open still held the old value), so this focus
        // call was never issued.
        cut.WaitForAssertion(() => Assert.Contains(_interop.FocusElementCalls, id => id == contentId));
    }
}
