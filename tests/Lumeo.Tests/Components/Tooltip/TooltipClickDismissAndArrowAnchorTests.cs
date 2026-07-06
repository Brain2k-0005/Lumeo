using System.Reflection;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Tooltip;

/// <summary>
/// Regression tests for two user-reported Tooltip bugs (follow-ups to the 4.0.4
/// focus-visible fix, which only covered the FOCUS path):
///
/// 1. Click-pin path: HandleTap toggled the tap-to-pin state on EVERY click — the rc.44
///    tap-to-pin feature was meant for touch (no hover there), but a desktop MOUSE click
///    also flipped _pinned=true, so the tooltip stuck open after clicking the trigger
///    (mouseleave only clears _hovered; _pinned kept it open) until a click landed
///    somewhere else. Reported on the sidebar-toggle button, structurally affecting every
///    Tooltip-wrapped clickable element; consumers had NO workaround (no synthetic event
///    can clear the internal state, and there is no imperative close API). Fixed: only a
///    TOUCH tap pins; mouse/pen/keyboard activation now CLOSES the tooltip (Radix parity —
///    clicking a tooltip trigger dismisses its hint).
///
/// 2. Arrow anchor: the arrow was hardcoded box-centered (left-1/2 / top-1/2), so when the
///    viewport-edge clamp shifted the box away from the trigger (trigger near the screen
///    edge), the arrow pointed into empty space. Fixed: the arrow now renders at
///    var(--lumeo-arrow-x/y, 50%), written by positionFixed from the trigger's actual
///    center within the final box (floating-ui "arrow middleware" equivalent); the 50%
///    fallback keeps the classic centered arrow before the first JS placement (and here in
///    bUnit, where no JS runs).
/// </summary>
public class TooltipClickDismissAndArrowAnchorTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    // Mirrors ComponentInteropService's private _jsModuleVersion computation exactly —
    // see the identical helper (and its full rationale) in TooltipTests.
    private static readonly string TestJsModuleVersion =
        typeof(Lumeo.Services.ComponentInteropService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
        ?? typeof(Lumeo.Services.ComponentInteropService).Assembly.GetName().Version?.ToString()
        ?? "0.0.0";

    public TooltipClickDismissAndArrowAnchorTests()
    {
        _ctx.AddLumeoServices();
        // Keyboard-focus tests need the focus-visible gate to report true (real keyboard
        // navigation); Loose mode's bool default would be false. Same stub as TooltipTests.
        _ctx.JSInterop.SetupModule($"./_content/Lumeo/js/components.js?v={TestJsModuleVersion}")
            .Setup<bool>("isActiveElementFocusVisible")
            .SetResult(true);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderTooltip(L.Side side = L.Side.Top)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Tooltip>(0);
            builder.AddAttribute(1, "ShowDelay", 0);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.TooltipTrigger>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Hover me")));
                b.CloseComponent();

                b.OpenComponent<L.TooltipContent>(2);
                b.AddAttribute(3, "Side", side);
                b.AddAttribute(4, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Tooltip text")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    private static Microsoft.AspNetCore.Components.Web.PointerEventArgs Pointer(string type) =>
        new() { PointerType = type };

    // ---- 1. Click-pin path -------------------------------------------------------------

    [Fact]
    public void Mouse_Click_Closes_A_Hover_Opened_Tooltip()
    {
        var cut = RenderTooltip();
        cut.Find("div").MouseEnter(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        Assert.NotEmpty(cut.FindAll("[role='tooltip']"));

        // A real mouse click = pointerdown(mouse) followed by click.
        cut.Find("div").TriggerEvent("onpointerdown", Pointer("mouse"));
        cut.Find("div").Click();

        // The content stays mounted through its zoom-out exit window (B11 parity), so
        // poll for the unmount rather than asserting instant removal.
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[role='tooltip']")), timeout: TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void After_A_Mouse_Click_The_Tooltip_Stays_Closed_Until_The_Pointer_ReEnters()
    {
        var cut = RenderTooltip();
        cut.Find("div").MouseEnter(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cut.Find("div").TriggerEvent("onpointerdown", Pointer("mouse"));
        cut.Find("div").Click();
        // Wait out the zoom-out exit window so the dismissal is fully settled before
        // testing the re-enter path.
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[role='tooltip']")), timeout: TimeSpan.FromSeconds(5));

        // Cursor resting on the trigger: no new mouseenter fires, so nothing reopens it.
        // Only an actual leave + re-enter shows the hint again (hover still works).
        cut.Find("div").MouseLeave(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cut.Find("div").MouseEnter(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        Assert.NotEmpty(cut.FindAll("[role='tooltip']"));
    }

    [Fact]
    public void Touch_Tap_Still_Pins_The_Tooltip_Open()
    {
        var cut = RenderTooltip();

        // A touch tap = pointerdown(touch) followed by click. Pin must survive a
        // (browser-emulated) mouseleave — that's the whole point of pinning on touch.
        cut.Find("div").TriggerEvent("onpointerdown", Pointer("touch"));
        cut.Find("div").Click();
        Assert.NotEmpty(cut.FindAll("[role='tooltip']"));

        cut.Find("div").MouseLeave(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        Assert.NotEmpty(cut.FindAll("[role='tooltip']"));
    }

    [Fact]
    public void Second_Touch_Tap_Unpins()
    {
        var cut = RenderTooltip();
        cut.Find("div").TriggerEvent("onpointerdown", Pointer("touch"));
        cut.Find("div").Click();
        Assert.NotEmpty(cut.FindAll("[role='tooltip']"));

        cut.Find("div").TriggerEvent("onpointerdown", Pointer("touch"));
        cut.Find("div").Click();
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[role='tooltip']")), timeout: TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Keyboard_Activation_Closes_A_Focus_Opened_Tooltip()
    {
        var cut = RenderTooltip();
        await cut.Find("div").TriggerEventAsync("onfocusin", new Microsoft.AspNetCore.Components.Web.FocusEventArgs());
        Assert.NotEmpty(cut.FindAll("[role='tooltip']"));

        // Enter/Space on a focused button fires click with NO preceding pointerdown —
        // that's the keyboard-activation signature. Radix parity: activation dismisses.
        cut.Find("div").Click();
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[role='tooltip']")), timeout: TimeSpan.FromSeconds(5));
    }

    // ---- 2. Arrow anchor ---------------------------------------------------------------

    [Theory]
    [InlineData(L.Side.Top, "left: var(--lumeo-arrow-x, 50%)")]
    [InlineData(L.Side.Bottom, "left: var(--lumeo-arrow-x, 50%)")]
    [InlineData(L.Side.Left, "top: var(--lumeo-arrow-y, 50%)")]
    [InlineData(L.Side.Right, "top: var(--lumeo-arrow-y, 50%)")]
    public void Arrow_Anchors_Via_The_Position_Aware_CSS_Var_With_A_Centered_Fallback(L.Side side, string expectedStyle)
    {
        var cut = RenderTooltip(side);
        cut.Find("div").MouseEnter(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        var arrow = cut.Find("[role='tooltip'] .rotate-45");
        Assert.Equal(expectedStyle, arrow.GetAttribute("style"));
        // The old hardcoded box-centering must be gone — it's exactly what kept the arrow
        // pointing into empty space when the box was clamped at the viewport edge.
        Assert.DoesNotContain("left-1/2", arrow.ClassList);
        Assert.DoesNotContain("top-1/2", arrow.ClassList);
    }
}
