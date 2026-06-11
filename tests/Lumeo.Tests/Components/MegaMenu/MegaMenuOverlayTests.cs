using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.MegaMenu;

/// <summary>
/// Regression tests for the menu-family activation/dismissal audit.
///
/// Overlay rules: the open MegaMenu panel must register click-outside dismissal
/// (and unregister on close/dispose) and close on Escape — including when focus
/// is inside the panel, not just on the trigger.
///
/// Double-fire: the MegaMenuItem trigger is a native button, so the browser
/// synthesizes a click for Enter/Space; the old keydown handler also toggled on
/// those keys, making keyboard activation a net no-op. bUnit does NOT
/// synthesize native clicks from keydown, so the tests emulate the browser by
/// dispatching keydown THEN click and asserting the END state is open.
/// </summary>
public class MegaMenuOverlayTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public MegaMenuOverlayTests()
    {
        _ctx.AddLumeoServices();
        // Last registration wins: route component interop through the tracker.
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderMegaMenu()
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.MegaMenu>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.MegaMenuItem>(0);
                b.AddAttribute(1, "Label", "Products");
                b.AddAttribute(2, "ChildContent", (RenderFragment)(item =>
                {
                    item.OpenComponent<L.MegaMenuPanel>(0);
                    item.AddAttribute(1, "ChildContent", (RenderFragment)(panel =>
                    {
                        panel.OpenComponent<L.MegaMenuLink>(0);
                        panel.AddAttribute(1, "Title", "Analytics");
                        panel.AddAttribute(2, "Href", "#analytics");
                        panel.CloseComponent();
                    }));
                    item.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    // --- Activation (double-fire regression) ---

    [Theory]
    [InlineData("Enter")]
    [InlineData(" ")]
    public void Trigger_Keydown_Then_NativeClick_Leaves_Panel_Open(string key)
    {
        var cut = RenderMegaMenu();

        // Browser order for Enter/Space on a native <button>: keydown, then a
        // synthesized click. The end state must be OPEN.
        var trigger = cut.Find("button");
        trigger.KeyDown(new KeyboardEventArgs { Key = key });
        cut.Find("button").Click();

        cut.WaitForAssertion(() => Assert.Contains("Analytics", cut.Markup));
    }

    [Fact]
    public void Trigger_Click_Opens_And_ReClick_Closes_Panel()
    {
        var cut = RenderMegaMenu();

        cut.Find("button").Click();
        Assert.Contains("Analytics", cut.Markup);

        cut.Find("button").Click();
        Assert.DoesNotContain("Analytics", cut.Markup);
    }

    // --- Escape handling ---

    [Fact]
    public void Escape_On_Trigger_Closes_Panel()
    {
        var cut = RenderMegaMenu();
        cut.Find("button").Click();
        Assert.Contains("Analytics", cut.Markup);

        cut.Find("button").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        cut.WaitForAssertion(() => Assert.DoesNotContain("Analytics", cut.Markup));
    }

    [Fact]
    public void Escape_Inside_Open_Panel_Closes_Panel()
    {
        var cut = RenderMegaMenu();
        cut.Find("button").Click();
        Assert.Contains("Analytics", cut.Markup);

        // Dispatch from the panel itself: the keydown must bubble to the item
        // root and close — previously only the trigger button handled Escape,
        // so pressing it with focus inside the panel did nothing.
        cut.Find("[role='menu']").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        cut.WaitForAssertion(() => Assert.DoesNotContain("Analytics", cut.Markup));
    }

    // --- Click-outside dismissal ---

    [Fact]
    public void ClickOutside_Registered_On_Open_And_Unregistered_On_Close()
    {
        var cut = RenderMegaMenu();
        Assert.Empty(_interop.ClickOutsideRegistrations);

        cut.Find("button").Click();
        cut.WaitForAssertion(() =>
        {
            var reg = Assert.Single(_interop.ClickOutsideRegistrations);
            Assert.StartsWith("megamenu-item-", reg.ElementId);
            Assert.StartsWith("megamenu-trigger-", reg.TriggerElementId);
        });

        cut.Find("button").Click(); // close
        cut.WaitForAssertion(() =>
        {
            var unreg = Assert.Single(_interop.ClickOutsideUnregistrations);
            Assert.StartsWith("megamenu-item-", unreg);
        });
    }

    [Fact]
    public async Task ClickOutside_Handler_Closes_Panel()
    {
        var cut = RenderMegaMenu();
        cut.Find("button").Click();
        cut.WaitForAssertion(() => Assert.Single(_interop.ClickOutsideRegistrations));

        var reg = _interop.ClickOutsideRegistrations[0];
        await cut.InvokeAsync(() => reg.Handler());

        cut.WaitForAssertion(() => Assert.DoesNotContain("Analytics", cut.Markup));
    }
}
