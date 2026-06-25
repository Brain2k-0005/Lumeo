using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.NavigationMenu;

/// <summary>
/// Battle-test #61 (keyboard-a11y): ArrowDown on a NavigationMenuTrigger used to
/// focus the content element synchronously from the trigger's keydown handler:
///
///   case "ArrowDown":
///       if (!IsActive) await Context.SetActiveItemId.InvokeAsync(ItemContext.ItemId);
///       await Interop.FocusElement(ItemContext.ContentId);
///
/// SetActiveItemId only SCHEDULES the re-render that mounts the content &lt;div&gt;,
/// so in a real browser the content element does not exist yet when FocusElement
/// runs and focus is silently lost. The fix moves the focus-into-content into
/// NavigationMenuContent.OnAfterRenderAsync (consumed via a one-shot request the
/// trigger records on the parent), so focus only fires once the content has
/// actually rendered — mirroring MenubarContent's focus-on-open.
///
/// Discriminating repro: render an item with a trigger but NO
/// NavigationMenuContent. The item still owns a generated ContentId.
///   • Old code: the trigger blindly calls Interop.FocusElement(ContentId) on
///     ArrowDown — a focus call is recorded against an element that never exists.
///   • Fixed code: the trigger only records a deferred focus request; with no
///     content component to render and consume it, NO focus call is recorded.
/// </summary>
public class NavigationMenuArrowDownFocusDeferredTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public NavigationMenuArrowDownFocusDeferredTests()
    {
        _ctx.AddLumeoServices();
        // Last registration wins: route component interop through the tracker.
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    /// <summary>Renders a NavigationMenu whose single item has a trigger but no
    /// NavigationMenuContent — so ArrowDown has no content element to focus.</summary>
    private IRenderedComponent<IComponent> RenderTriggerOnlyMenu() => _ctx.Render(builder =>
    {
        builder.OpenComponent<L.NavigationMenu>(0);
        builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
        {
            b.OpenComponent<L.NavigationMenuList>(0);
            b.AddAttribute(1, "ChildContent", (RenderFragment)(list =>
            {
                list.OpenComponent<L.NavigationMenuItem>(0);
                list.AddAttribute(1, "ChildContent", (RenderFragment)(item =>
                {
                    item.OpenComponent<L.NavigationMenuTrigger>(0);
                    item.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "Products")));
                    item.CloseComponent();
                    // Intentionally NO NavigationMenuContent.
                }));
                list.CloseComponent();
            }));
            b.CloseComponent();
        }));
        builder.CloseComponent();
    });

    /// <summary>Renders a NavigationMenu whose single item has a trigger AND a
    /// content panel, so the deferred focus request is consumed once the content
    /// renders.</summary>
    private IRenderedComponent<IComponent> RenderMenuWithContent() => _ctx.Render(builder =>
    {
        builder.OpenComponent<L.NavigationMenu>(0);
        builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
        {
            b.OpenComponent<L.NavigationMenuList>(0);
            b.AddAttribute(1, "ChildContent", (RenderFragment)(list =>
            {
                list.OpenComponent<L.NavigationMenuItem>(0);
                list.AddAttribute(1, "ChildContent", (RenderFragment)(item =>
                {
                    item.OpenComponent<L.NavigationMenuTrigger>(0);
                    item.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "Products")));
                    item.CloseComponent();

                    item.OpenComponent<L.NavigationMenuContent>(1);
                    item.AddAttribute(2, "ChildContent", (RenderFragment)(c => c.AddContent(0, "Products content")));
                    item.CloseComponent();
                }));
                list.CloseComponent();
            }));
            b.CloseComponent();
        }));
        builder.CloseComponent();
    });

    [Fact]
    public void ArrowDown_Does_Not_Focus_A_Content_Element_That_Was_Never_Rendered()
    {
        var cut = RenderTriggerOnlyMenu();
        var trigger = cut.Find("button");

        // Before the fix the trigger calls Interop.FocusElement(ContentId) here,
        // targeting an element that does not exist. After the fix the trigger only
        // records a deferred request, and with no content component nothing
        // consumes it — so no focus call is issued.
        trigger.KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });

        Assert.Empty(_interop.FocusElementCalls);
    }

    [Fact]
    public void ArrowDown_Focuses_Content_After_It_Renders_When_Content_Is_Present()
    {
        var cut = RenderMenuWithContent();
        var trigger = cut.Find("button");

        trigger.KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });

        // The submenu opens and focus moves into the now-rendered content panel.
        Assert.Contains("Products content", cut.Markup);
        var contentId = cut.Find("[role='menu']").GetAttribute("id");
        cut.WaitForAssertion(() => Assert.Contains(_interop.FocusElementCalls, id => id == contentId));
    }
}
