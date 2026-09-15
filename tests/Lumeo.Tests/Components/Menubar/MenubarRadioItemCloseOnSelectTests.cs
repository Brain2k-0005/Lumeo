using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Menubar;

/// <summary>
/// Field report #464, finding 2 — mirrored onto Menubar, which shares the same
/// radio-item shape as DropdownMenu: Radix closes the menu on selection, same
/// as a plain item, but MenubarRadioItem never requested the close (its own
/// doc comment even documented the non-closing as the intended behaviour,
/// which this fixes).
/// </summary>
public class MenubarRadioItemCloseOnSelectTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public MenubarRadioItemCloseOnSelectTests() => _ctx.AddLumeoServices();

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderMenubarWithRadioGroup()
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
                    menu.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "View")));
                    menu.CloseComponent();

                    menu.OpenComponent<L.MenubarContent>(1);
                    menu.AddAttribute(2, "ChildContent", (RenderFragment)(content =>
                    {
                        content.OpenComponent<L.MenubarRadioGroup>(0);
                        content.AddAttribute(1, "Value", "a");
                        content.AddAttribute(2, "ChildContent", (RenderFragment)(rg =>
                        {
                            rg.OpenComponent<L.MenubarRadioItem>(0);
                            rg.AddAttribute(1, "Value", "a");
                            rg.AddAttribute(2, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Apple")));
                            rg.CloseComponent();

                            rg.OpenComponent<L.MenubarRadioItem>(3);
                            rg.AddAttribute(4, "Value", "b");
                            rg.AddAttribute(5, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Banana")));
                            rg.CloseComponent();
                        }));
                        content.CloseComponent();
                    }));
                    menu.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    [Fact]
    public void Selecting_A_RadioItem_Closes_The_Menu()
    {
        var cut = RenderMenubarWithRadioGroup();
        cut.Find("button").Click(); // open the View menu
        Assert.Equal("open", cut.Find("[data-slot='menubar-content']").GetAttribute("data-state"));

        cut.FindAll("[role='menuitemradio']")[1].Click();

        Assert.Equal("closed", cut.Find("[data-slot='menubar-content']").GetAttribute("data-state"));
    }

    [Fact]
    public void Selecting_A_RadioItem_Still_Updates_The_Selection()
    {
        var cut = RenderMenubarWithRadioGroup();
        cut.Find("button").Click(); // open the View menu

        cut.FindAll("[role='menuitemradio']")[1].Click();

        Assert.Equal("true", cut.FindAll("[role='menuitemradio']")[1].GetAttribute("aria-checked"));
    }
}
