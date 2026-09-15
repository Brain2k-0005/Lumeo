using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.ContextMenu;

/// <summary>
/// Field report #464, finding 2 — mirrored onto ContextMenu, which shares the
/// same radio-item shape as DropdownMenu: Radix closes the menu on selection,
/// same as a plain item, but ContextMenuRadioItem never requested the close.
/// </summary>
public class ContextMenuRadioItemCloseOnSelectTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ContextMenuRadioItemCloseOnSelectTests() => _ctx.AddLumeoServices();

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderOpenContextMenuWithRadioGroup()
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
                    inner.OpenComponent<L.ContextMenuRadioGroup>(0);
                    inner.AddAttribute(1, "Value", "a");
                    inner.AddAttribute(2, "ChildContent", (RenderFragment)(rg =>
                    {
                        rg.OpenComponent<L.ContextMenuRadioItem>(0);
                        rg.AddAttribute(1, "Value", "a");
                        rg.AddAttribute(2, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Apple")));
                        rg.CloseComponent();

                        rg.OpenComponent<L.ContextMenuRadioItem>(3);
                        rg.AddAttribute(4, "Value", "b");
                        rg.AddAttribute(5, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Banana")));
                        rg.CloseComponent();
                    }));
                    inner.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    [Fact]
    public void Selecting_A_RadioItem_Closes_The_Menu()
    {
        var cut = RenderOpenContextMenuWithRadioGroup();
        Assert.Equal("open", cut.Find("[data-slot='context-menu-content']").GetAttribute("data-state"));

        cut.FindAll("[role='menuitemradio']")[1].Click();

        Assert.Equal("closed", cut.Find("[data-slot='context-menu-content']").GetAttribute("data-state"));
    }

    [Fact]
    public void Selecting_A_RadioItem_Still_Updates_The_Selection()
    {
        var cut = RenderOpenContextMenuWithRadioGroup();

        cut.FindAll("[role='menuitemradio']")[1].Click();

        Assert.Equal("true", cut.FindAll("[role='menuitemradio']")[1].GetAttribute("aria-checked"));
    }
}
