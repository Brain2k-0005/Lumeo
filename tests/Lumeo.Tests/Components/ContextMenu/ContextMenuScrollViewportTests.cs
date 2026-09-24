using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.ContextMenu;

/// <summary>
/// #520 follow-up: ContextMenuContent shares DropdownMenuContent's overflow-visible /
/// fixed-submenu pattern, so it gets the same built-in scrollable inner viewport
/// (see <see cref="Lumeo.Tests.Components.DropdownMenu.DropdownMenuScrollViewportTests"/>
/// for the full rationale).
/// </summary>
public class ContextMenuScrollViewportTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ContextMenuScrollViewportTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> Render(string? maxHeight = null)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.ContextMenu>(0);
            builder.AddAttribute(1, "IsOpen", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.ContextMenuTrigger>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Right-click here")));
                b.CloseComponent();

                b.OpenComponent<L.ContextMenuContent>(0);
                if (maxHeight is not null) b.AddAttribute(1, "MaxHeight", maxHeight);
                b.AddAttribute(2, "ChildContent", (RenderFragment)(inner =>
                {
                    inner.OpenComponent<L.ContextMenuItem>(0);
                    inner.AddAttribute(1, "ChildContent", (RenderFragment)(item => item.AddContent(0, "Menu Item 1")));
                    inner.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    [Fact]
    public void Inner_Viewport_Has_Overflow_Y_Auto()
    {
        var cut = Render();
        var viewport = cut.Find("[data-slot='context-menu-viewport']");
        Assert.Contains("overflow-y-auto", viewport.ClassName);
    }

    [Fact]
    public void Default_Viewport_Max_Height_Uses_The_Live_Available_Height_Var_With_24rem_Fallback()
    {
        var cut = Render();
        var style = cut.Find("[data-slot='context-menu-viewport']").GetAttribute("style") ?? "";
        Assert.Contains("max-height: var(--lumeo-dropdown-available-height, 24rem)", style);
    }

    [Fact]
    public void MaxHeight_Parameter_Overrides_The_Viewport_Cap()
    {
        var cut = Render(maxHeight: "18rem");
        var style = cut.Find("[data-slot='context-menu-viewport']").GetAttribute("style") ?? "";
        Assert.Contains("max-height: 18rem", style);
    }

    [Fact]
    public void Outer_Content_Stays_Overflow_Visible()
    {
        var cut = Render();
        var contentClass = cut.Find("[data-slot='context-menu-content']").GetAttribute("class") ?? "";
        Assert.Contains("overflow-visible", contentClass);
    }
}
