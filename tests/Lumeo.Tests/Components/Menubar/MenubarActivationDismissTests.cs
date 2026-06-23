using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Menubar;

/// <summary>
/// Regression tests for the menu-family activation/dismissal audit.
///
/// Double-fire: MenubarTrigger/MenubarSubTrigger are native buttons, so the
/// browser synthesizes a click for Enter/Space. The old keydown handlers also
/// toggled on those keys, making keyboard activation a net no-op (keydown
/// opened, the synthesized click closed). bUnit does NOT synthesize native
/// clicks from keydown, so these tests emulate the browser by dispatching
/// keydown THEN click and asserting the END state is open.
///
/// Dismissal: click-outside must exclude the menu wrapper (the trigger lives
/// inside it), otherwise mousedown on the open trigger closes the menu and the
/// trigger's click re-opens it — the open trigger could never close its menu.
///
/// Keyboard nav: MenubarContent must have role=menu, take focus on open, and
/// support Up/Down/Home/End plus Left/Right menubar navigation (WAI-ARIA).
/// </summary>
public class MenubarActivationDismissTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public MenubarActivationDismissTests()
    {
        _ctx.AddLumeoServices();
        // Last registration wins: route component interop through the tracker.
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderMenubar(bool twoMenus = false, bool withSub = false)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Menubar>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.MenubarMenu>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(menu =>
                {
                    menu.OpenComponent<L.MenubarTrigger>(0);
                    menu.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "File")));
                    menu.CloseComponent();

                    menu.OpenComponent<L.MenubarContent>(1);
                    menu.AddAttribute(2, "ChildContent", (RenderFragment)(content =>
                    {
                        content.OpenComponent<L.MenubarItem>(0);
                        content.AddAttribute(1, "ChildContent", (RenderFragment)(i => i.AddContent(0, "New File")));
                        content.CloseComponent();

                        if (withSub)
                        {
                            content.OpenComponent<L.MenubarSub>(1);
                            content.AddAttribute(2, "ChildContent", (RenderFragment)(sub =>
                            {
                                sub.OpenComponent<L.MenubarSubTrigger>(0);
                                sub.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "More Tools")));
                                sub.CloseComponent();

                                sub.OpenComponent<L.MenubarSubContent>(1);
                                sub.AddAttribute(2, "ChildContent", (RenderFragment)(sc =>
                                {
                                    sc.OpenComponent<L.MenubarItem>(0);
                                    sc.AddAttribute(1, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Sub Action")));
                                    sc.CloseComponent();
                                }));
                                sub.CloseComponent();
                            }));
                            content.CloseComponent();
                        }
                    }));
                    menu.CloseComponent();
                }));
                b.CloseComponent();

                if (twoMenus)
                {
                    b.OpenComponent<L.MenubarMenu>(1);
                    b.AddAttribute(2, "ChildContent", (RenderFragment)(menu =>
                    {
                        menu.OpenComponent<L.MenubarTrigger>(0);
                        menu.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "Edit")));
                        menu.CloseComponent();

                        menu.OpenComponent<L.MenubarContent>(1);
                        menu.AddAttribute(2, "ChildContent", (RenderFragment)(content =>
                        {
                            content.OpenComponent<L.MenubarItem>(0);
                            content.AddAttribute(1, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Undo")));
                            content.CloseComponent();
                        }));
                        menu.CloseComponent();
                    }));
                    b.CloseComponent();
                }
            }));
            builder.CloseComponent();
        });
    }

    // --- Trigger activation (double-fire regression) ---

    [Theory]
    [InlineData("Enter")]
    [InlineData(" ")]
    public void Trigger_Keydown_Then_NativeClick_Leaves_Menu_Open(string key)
    {
        var cut = RenderMenubar();

        // Browser order for Enter/Space on a native <button>: keydown, then a
        // synthesized click. The end state must be OPEN.
        var trigger = cut.Find("button");
        trigger.KeyDown(new KeyboardEventArgs { Key = key });
        cut.Find("button").Click();

        Assert.Contains("New File", cut.Markup);
    }

    [Fact]
    public void Trigger_ArrowDown_Keydown_Opens_Menu()
    {
        // Arrow keys do not synthesize clicks — keydown alone must open.
        var cut = RenderMenubar();
        cut.Find("button").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });

        Assert.Contains("New File", cut.Markup);
    }

    // --- Content semantics, focus and dismissal ---

    [Fact]
    public void Content_Has_Role_Menu_When_Open()
    {
        var cut = RenderMenubar();
        cut.Find("button").Click();

        var content = cut.Find("[role='menu']");
        Assert.Equal("vertical", content.GetAttribute("aria-orientation"));
    }

    [Fact]
    public void Escape_Returns_Focus_To_The_Trigger()
    {
        // WCAG 2.4.3: closing the menu from the keyboard (Escape) must move focus
        // back to the trigger that opened it, not drop it to <body>.
        var cut = RenderMenubar();
        var trigger = cut.Find("button");
        var triggerId = trigger.GetAttribute("id");
        Assert.False(string.IsNullOrEmpty(triggerId));

        trigger.Click(); // open (focus moves into the menu content)
        cut.Find("[role='menu']").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Contains(triggerId, _interop.FocusedElementIds);
    }

    [Fact]
    public void Content_Registers_ClickOutside_Excluding_Menu_Wrapper()
    {
        var cut = RenderMenubar();
        cut.Find("button").Click();

        cut.WaitForAssertion(() =>
        {
            var reg = Assert.Single(_interop.ClickOutsideRegistrations);
            Assert.StartsWith("menubar-content-", reg.ElementId);
            // The exclusion must be the menu wrapper (which contains the
            // trigger), never null — otherwise the open trigger can never
            // close its own menu (mousedown closes, click re-opens).
            Assert.NotNull(reg.TriggerElementId);
            var wrapper = cut.Find($"[id='{reg.TriggerElementId}']");
            Assert.NotNull(wrapper.QuerySelector("button"));
        });
    }

    [Fact]
    public async Task Content_ClickOutside_Handler_Closes_Menu()
    {
        var cut = RenderMenubar();
        cut.Find("button").Click();
        cut.WaitForAssertion(() => Assert.Single(_interop.ClickOutsideRegistrations));

        var reg = _interop.ClickOutsideRegistrations[0];
        await cut.InvokeAsync(() => reg.Handler());

        cut.WaitForAssertion(() => Assert.DoesNotContain("New File", cut.Markup));
    }

    [Fact]
    public void Content_Focuses_Itself_On_Open()
    {
        var cut = RenderMenubar();
        cut.Find("button").Click();

        // Without this focus call, the content's @onkeydown handler never
        // receives events and keyboard nav silently breaks.
        cut.WaitForAssertion(() =>
            Assert.Contains(_interop.FocusElementCalls, id => id.StartsWith("menubar-content-")));
    }

    [Fact]
    public void Content_Escape_Closes_Menu()
    {
        var cut = RenderMenubar();
        cut.Find("button").Click();

        cut.Find("[role='menu']").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.DoesNotContain("New File", cut.Markup);
    }

    // --- Item navigation (Up/Down/Home/End) ---

    [Fact]
    public void Content_ArrowDown_Focuses_First_Item_Then_End_Focuses_Last()
    {
        _interop.MenuItemCount = 3;
        var cut = RenderMenubar();
        cut.Find("button").Click();

        var content = cut.Find("[role='menu']");
        content.KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        cut.WaitForAssertion(() =>
            Assert.Contains(_interop.FocusMenuItemCalls, c => c.ContainerId.StartsWith("menubar-content-") && c.Index == 0));

        content.KeyDown(new KeyboardEventArgs { Key = "End" });
        cut.WaitForAssertion(() =>
            Assert.Contains(_interop.FocusMenuItemCalls, c => c.ContainerId.StartsWith("menubar-content-") && c.Index == 2));
    }

    [Fact]
    public void Content_ArrowUp_From_Start_Wraps_To_Last_Item()
    {
        _interop.MenuItemCount = 3;
        var cut = RenderMenubar();
        cut.Find("button").Click();

        cut.Find("[role='menu']").KeyDown(new KeyboardEventArgs { Key = "ArrowUp" });

        cut.WaitForAssertion(() =>
            Assert.Contains(_interop.FocusMenuItemCalls, c => c.ContainerId.StartsWith("menubar-content-") && c.Index == 2));
    }

    // --- Left/Right menubar navigation from inside the open menu ---

    [Fact]
    public void Content_ArrowRight_Moves_To_Next_Menubar_Menu()
    {
        var cut = RenderMenubar(twoMenus: true);
        cut.FindAll("button")[0].Click();
        Assert.Contains("New File", cut.Markup);

        cut.Find("[role='menu']").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("New File", cut.Markup);
            Assert.Contains("Undo", cut.Markup);
        });
    }

    [Fact]
    public void Content_ArrowLeft_Moves_To_Previous_Menubar_Menu()
    {
        var cut = RenderMenubar(twoMenus: true);
        cut.FindAll("button")[1].Click();
        Assert.Contains("Undo", cut.Markup);

        cut.Find("[role='menu']").KeyDown(new KeyboardEventArgs { Key = "ArrowLeft" });

        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("Undo", cut.Markup);
            Assert.Contains("New File", cut.Markup);
        });
    }

    // --- Submenu interplay ---

    [Theory]
    [InlineData("Enter")]
    [InlineData(" ")]
    public void SubTrigger_Keydown_Then_NativeClick_Leaves_Submenu_Open(string key)
    {
        var cut = RenderMenubar(withSub: true);
        cut.Find("button").Click(); // open the File menu

        var subTrigger = cut.Find("button[aria-haspopup='menu']");
        subTrigger.KeyDown(new KeyboardEventArgs { Key = key });
        cut.Find("button[aria-haspopup='menu']").Click();

        cut.WaitForAssertion(() => Assert.Contains("Sub Action", cut.Markup));
    }

    [Fact]
    public void SubTrigger_ArrowRight_Opens_Submenu_Without_Navigating_Menubar()
    {
        var cut = RenderMenubar(twoMenus: true, withSub: true);
        cut.FindAll("button")[0].Click(); // open the File menu

        cut.Find("button[aria-haspopup='menu']").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Sub Action", cut.Markup);   // submenu opened
            Assert.Contains("New File", cut.Markup);     // File menu still open
            Assert.DoesNotContain("Undo", cut.Markup);   // menubar did NOT move on
        });
    }

    [Fact]
    public void SubContent_ArrowLeft_Closes_Submenu_Without_Navigating_Menubar()
    {
        var cut = RenderMenubar(twoMenus: true, withSub: true);
        cut.FindAll("button")[0].Click(); // open the File menu
        cut.Find("button[aria-haspopup='menu']").Click(); // open the submenu
        cut.WaitForAssertion(() => Assert.Contains("Sub Action", cut.Markup));

        var subContent = cut.FindAll("[role='menu']").First(d => (d.Id ?? "").StartsWith("menubar-sub-content"));
        subContent.KeyDown(new KeyboardEventArgs { Key = "ArrowLeft" });

        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("Sub Action", cut.Markup); // submenu closed
            Assert.Contains("New File", cut.Markup);         // File menu still open
            Assert.DoesNotContain("Undo", cut.Markup);       // menubar did NOT navigate
        });
    }
}
