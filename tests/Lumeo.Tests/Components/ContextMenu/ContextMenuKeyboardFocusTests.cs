using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.ContextMenu;

/// <summary>
/// Regression tests for the menu-family activation/dismissal audit.
///
/// Keyboard reachability: ContextMenuContent must move focus to its container
/// when it opens — without that, its @onkeydown handler (arrows/Home/End/
/// Escape) never receives events and keyboard nav silently breaks.
///
/// Double-fire: ContextMenuSubTrigger is a native button, so the browser
/// synthesizes a click for Enter/Space; the old keydown handler also opened on
/// those keys, making keyboard activation a net no-op. bUnit does NOT
/// synthesize native clicks from keydown, so the tests emulate the browser by
/// dispatching keydown THEN click and asserting the END state is open.
/// </summary>
public class ContextMenuKeyboardFocusTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public ContextMenuKeyboardFocusTests()
    {
        _ctx.AddLumeoServices();
        // Last registration wins: route component interop through the tracker.
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderContextMenu(bool withSub = false)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.ContextMenu>(0);
            builder.AddAttribute(1, "Open", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.ContextMenuTrigger>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Right-click here")));
                b.CloseComponent();

                b.OpenComponent<L.ContextMenuContent>(1);
                b.AddAttribute(2, "ChildContent", (RenderFragment)(inner =>
                {
                    inner.OpenComponent<L.ContextMenuItem>(0);
                    inner.AddAttribute(1, "ChildContent", (RenderFragment)(item => item.AddContent(0, "Menu Item 1")));
                    inner.CloseComponent();

                    if (withSub)
                    {
                        inner.OpenComponent<L.ContextMenuSub>(1);
                        inner.AddAttribute(2, "ChildContent", (RenderFragment)(sub =>
                        {
                            sub.OpenComponent<L.ContextMenuSubTrigger>(0);
                            sub.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "More Tools")));
                            sub.CloseComponent();

                            sub.OpenComponent<L.ContextMenuSubContent>(1);
                            sub.AddAttribute(2, "ChildContent", (RenderFragment)(sc =>
                            {
                                sc.OpenComponent<L.ContextMenuItem>(0);
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

    // --- Keyboard reachability ---

    [Fact]
    public void Content_Focuses_Itself_On_Open()
    {
        var cut = RenderContextMenu();

        // Without this focus call the @onkeydown handler is unreachable: focus
        // stays wherever the right-click happened.
        cut.WaitForAssertion(() =>
            Assert.Contains(_interop.FocusElementCalls, id => id.StartsWith("context-menu-content-")));
    }

    [Fact]
    public void Content_ArrowDown_Focuses_First_Item()
    {
        _interop.MenuItemCount = 2;
        var cut = RenderContextMenu();

        cut.Find("[role='menu']").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });

        cut.WaitForAssertion(() =>
            Assert.Contains(_interop.FocusMenuItemCalls, c => c.ContainerId.StartsWith("context-menu-content-") && c.Index == 0));
    }

    // --- Submenu trigger activation (double-fire regression) ---

    [Theory]
    [InlineData("Enter")]
    [InlineData(" ")]
    public void SubTrigger_Keydown_Then_NativeClick_Leaves_Submenu_Open(string key)
    {
        var cut = RenderContextMenu(withSub: true);

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
        var cut = RenderContextMenu(withSub: true);

        cut.Find("button[aria-haspopup='menu']").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        cut.WaitForAssertion(() => Assert.Contains("Sub Item A", cut.Markup));
    }
}
