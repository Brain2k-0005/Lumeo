using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.DropdownMenu;

/// <summary>
/// Field report #464, finding 2: Radix closes the menu when a
/// <c>menuitemradio</c> or <c>menuitemcheckbox</c> is selected, same as a
/// plain item — DropdownMenuRadioItem and DropdownMenuCheckboxItem never
/// requested the close, so the menu stayed open after a selection.
/// </summary>
public class DropdownMenuRadioCheckboxCloseOnSelectTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DropdownMenuRadioCheckboxCloseOnSelectTests() => _ctx.AddLumeoServices();

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderWithRadioGroup()
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
                    inner.OpenComponent<L.DropdownMenuRadioGroup>(0);
                    inner.AddAttribute(1, "Value", "a");
                    inner.AddAttribute(2, "ChildContent", (RenderFragment)(rg =>
                    {
                        rg.OpenComponent<L.DropdownMenuRadioItem>(0);
                        rg.AddAttribute(1, "Value", "a");
                        rg.AddAttribute(2, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Apple")));
                        rg.CloseComponent();

                        rg.OpenComponent<L.DropdownMenuRadioItem>(3);
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

    private IRenderedComponent<IComponent> RenderWithCheckboxItem()
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
                    inner.OpenComponent<L.DropdownMenuCheckboxItem>(0);
                    inner.AddAttribute(1, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Show grid")));
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
        var cut = RenderWithRadioGroup();
        Assert.Equal("open", cut.Find("[data-slot='dropdown-menu-content']").GetAttribute("data-state"));

        cut.FindAll("[role='menuitemradio']")[1].Click();

        Assert.Equal("closed", cut.Find("[data-slot='dropdown-menu-content']").GetAttribute("data-state"));
    }

    [Fact]
    public void Selecting_A_RadioItem_Still_Updates_The_Selection()
    {
        // The close fix must not regress the existing selection behaviour.
        var cut = RenderWithRadioGroup();

        cut.FindAll("[role='menuitemradio']")[1].Click();

        Assert.Equal("true", cut.FindAll("[role='menuitemradio']")[1].GetAttribute("aria-checked"));
    }

    [Fact]
    public void Toggling_A_CheckboxItem_Closes_The_Menu()
    {
        var cut = RenderWithCheckboxItem();
        Assert.Equal("open", cut.Find("[data-slot='dropdown-menu-content']").GetAttribute("data-state"));

        cut.Find("[role='menuitemcheckbox']").Click();

        Assert.Equal("closed", cut.Find("[data-slot='dropdown-menu-content']").GetAttribute("data-state"));
    }

    [Fact]
    public void Toggling_A_CheckboxItem_Still_Updates_The_Checked_State()
    {
        var cut = RenderWithCheckboxItem();

        cut.Find("[role='menuitemcheckbox']").Click();

        Assert.Equal("true", cut.Find("[role='menuitemcheckbox']").GetAttribute("aria-checked"));
    }
}
