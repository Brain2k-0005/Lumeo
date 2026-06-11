using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.DropdownMenu;

/// <summary>
/// Regression tests for the menu-family activation/dismissal audit.
///
/// Double-fire: DropdownMenuSubTrigger is a native button, so the browser
/// synthesizes a click for Enter/Space. The old keydown handler also opened on
/// those keys, making keyboard activation a net no-op (keydown opened, the
/// synthesized click toggled closed). bUnit does NOT synthesize native clicks
/// from keydown, so these tests emulate the browser by dispatching keydown
/// THEN click and asserting the END state is open.
///
/// Dismissal: DropdownMenuContent must register click-outside with the wrapper
/// (containing the trigger) excluded, never null — otherwise mousedown on the
/// open trigger closes the menu and the trigger's click re-opens it.
/// </summary>
public class DropdownMenuActivationDismissTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public DropdownMenuActivationDismissTests()
    {
        _ctx.AddLumeoServices();
        // Last registration wins: route component interop through the tracker.
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderDropdownMenu(bool withSub = false)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.DropdownMenu>(0);
            builder.AddAttribute(1, "Open", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.DropdownMenuTrigger>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Open Menu")));
                b.CloseComponent();

                b.OpenComponent<L.DropdownMenuContent>(1);
                b.AddAttribute(2, "ChildContent", (RenderFragment)(inner =>
                {
                    inner.OpenComponent<L.DropdownMenuItem>(0);
                    inner.AddAttribute(1, "ChildContent", (RenderFragment)(item => item.AddContent(0, "Item 1")));
                    inner.CloseComponent();

                    if (withSub)
                    {
                        inner.OpenComponent<L.DropdownMenuSub>(1);
                        inner.AddAttribute(2, "ChildContent", (RenderFragment)(sub =>
                        {
                            sub.OpenComponent<L.DropdownMenuSubTrigger>(0);
                            sub.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "More Tools")));
                            sub.CloseComponent();

                            sub.OpenComponent<L.DropdownMenuSubContent>(1);
                            sub.AddAttribute(2, "ChildContent", (RenderFragment)(sc =>
                            {
                                sc.OpenComponent<L.DropdownMenuItem>(0);
                                sc.AddAttribute(1, "ChildContent", (RenderFragment)(item => item.AddContent(0, "Sub Item A")));
                                sc.CloseComponent();
                            }));
                            sub.CloseComponent();
                        }));
                        inner.CloseComponent();
                    }
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    // --- Submenu trigger activation (double-fire regression) ---

    [Theory]
    [InlineData("Enter")]
    [InlineData(" ")]
    public void SubTrigger_Keydown_Then_NativeClick_Leaves_Submenu_Open(string key)
    {
        var cut = RenderDropdownMenu(withSub: true);

        // Browser order for Enter/Space on a native <button>: keydown, then a
        // synthesized click. The end state must be OPEN.
        var subTrigger = cut.Find("button[aria-haspopup='menu']");
        subTrigger.KeyDown(new KeyboardEventArgs { Key = key });
        cut.Find("button[aria-haspopup='menu']").Click();

        cut.WaitForAssertion(() => Assert.Contains("Sub Item A", cut.Markup));
    }

    [Fact]
    public void SubTrigger_ArrowRight_Keydown_Opens_Submenu()
    {
        // Arrow keys do not synthesize clicks — keydown alone must open.
        var cut = RenderDropdownMenu(withSub: true);

        cut.Find("button[aria-haspopup='menu']").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        cut.WaitForAssertion(() => Assert.Contains("Sub Item A", cut.Markup));
    }

    // --- Click-outside trigger exclusion ---

    [Fact]
    public void Content_Registers_ClickOutside_Excluding_Wrapper()
    {
        var cut = RenderDropdownMenu();

        cut.WaitForAssertion(() =>
        {
            var reg = Assert.Single(_interop.ClickOutsideRegistrations);
            Assert.StartsWith("dropdown-content-", reg.ElementId);
            // The exclusion must be the wrapper containing the trigger, never
            // null — otherwise the open trigger can never close its own menu
            // (mousedown closes it, the click re-opens it).
            Assert.NotNull(reg.TriggerElementId);
            var wrapper = cut.Find($"[id='{reg.TriggerElementId}']");
            Assert.NotNull(wrapper.QuerySelector("[role='button']"));
        });
    }

    [Fact]
    public async Task Content_ClickOutside_Handler_Closes_Menu()
    {
        var cut = RenderDropdownMenu();
        cut.WaitForAssertion(() => Assert.Single(_interop.ClickOutsideRegistrations));

        var reg = _interop.ClickOutsideRegistrations[0];
        await cut.InvokeAsync(() => reg.Handler());

        cut.WaitForAssertion(() => Assert.DoesNotContain("Item 1", cut.Markup));
    }
}
