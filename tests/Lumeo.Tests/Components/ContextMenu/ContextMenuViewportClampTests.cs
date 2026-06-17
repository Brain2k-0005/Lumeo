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
/// Regression tests for #224 — the root ContextMenu opened at raw click
/// coordinates and never clamped to the viewport, so right-clicking near the
/// bottom/right edge rendered it partly off-screen (submenus already flip).
/// ContextMenuContent now calls PositionAtPoint to clamp the menu.
/// </summary>
public class ContextMenuViewportClampTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public ContextMenuViewportClampTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderContextMenu()
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.ContextMenu>(0);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.ContextMenuTrigger>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Right-click here")));
                b.CloseComponent();

                b.OpenComponent<L.ContextMenuContent>(1);
                b.AddAttribute(2, "ChildContent", (RenderFragment)(inner =>
                {
                    inner.OpenComponent<L.ContextMenuItem>(0);
                    inner.AddAttribute(1, "ChildContent", (RenderFragment)(item => item.AddContent(0, "Item 1")));
                    inner.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    [Fact]
    public void Opening_Root_Menu_Clamps_To_Viewport()
    {
        var cut = RenderContextMenu();

        // Right-click near the bottom-right corner.
        cut.Find("[aria-expanded]").TriggerEvent("oncontextmenu",
            new MouseEventArgs { ClientX = 1900, ClientY = 1050 });

        cut.WaitForAssertion(() =>
        {
            Assert.Single(_interop.PositionAtPointCalls);
            var call = _interop.PositionAtPointCalls[0];
            Assert.StartsWith("context-menu-content-", call.ContentId);
            Assert.Equal(1900, call.X);
            Assert.Equal(1050, call.Y);
        });
    }

    [Fact]
    public void Trigger_AriaExpanded_Uses_Lowercase_Tokens()
    {
        var cut = RenderContextMenu();
        var trigger = cut.Find("[aria-expanded]");
        Assert.Equal("false", trigger.GetAttribute("aria-expanded"));

        cut.Find("[aria-expanded]").TriggerEvent("oncontextmenu",
            new MouseEventArgs { ClientX = 10, ClientY = 10 });

        cut.WaitForAssertion(() =>
            Assert.Equal("true", cut.Find("[aria-expanded]").GetAttribute("aria-expanded")));
    }

    // --- ContextMenuCheckboxItem Disabled (#224 a11y gap) ---

    [Fact]
    public void CheckboxItem_Disabled_Is_Marked_And_Does_Not_Toggle()
    {
        var toggled = 0;
        var cut = _ctx.Render<L.ContextMenuCheckboxItem>(p => p
            .Add(c => c.Disabled, true)
            .Add(c => c.Checked, false)
            .Add(c => c.CheckedChanged, EventCallback.Factory.Create<bool>(this, _ => toggled++))
            .Add(c => c.ChildContent, (RenderFragment)(b => b.AddContent(0, "Show grid"))));

        var btn = cut.Find("button");
        Assert.True(btn.HasAttribute("disabled"));

        // Even if a click event reaches it, the guard must prevent toggling.
        btn.Click();
        Assert.Equal(0, toggled);
    }
}
