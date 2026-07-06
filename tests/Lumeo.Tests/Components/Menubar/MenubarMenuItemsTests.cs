using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Menubar;

/// <summary>
/// Wave 4 — Menubar reaches DropdownMenu subcomponent parity: MenubarCheckboxItem,
/// MenubarRadioGroup/MenubarRadioItem, MenubarGroup, plus item <c>inset</c> and
/// <c>variant="destructive"</c>. Mirrors the DropdownMenu equivalents 1:1.
/// </summary>
public class MenubarMenuItemsTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public MenubarMenuItemsTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // --- Item inset / variant ---

    [Fact]
    public void Item_Inset_Adds_Ps8_And_DataInset()
    {
        var cut = _ctx.Render<L.MenubarItem>(p =>
        {
            p.Add(i => i.Inset, true);
            p.Add(i => i.ChildContent, (RenderFragment)(c => c.AddContent(0, "Item")));
        });
        var btn = cut.Find("button");
        Assert.Equal("true", btn.GetAttribute("data-inset"));
        Assert.Contains("ps-8", btn.GetAttribute("class") ?? "");
    }

    [Fact]
    public void Item_Destructive_Variant_Adds_TextDestructive_And_DataVariant()
    {
        var cut = _ctx.Render<L.MenubarItem>(p =>
        {
            p.Add(i => i.Variant, L.MenuItemVariant.Destructive);
            p.Add(i => i.ChildContent, (RenderFragment)(c => c.AddContent(0, "Delete")));
        });
        var btn = cut.Find("button");
        Assert.Equal("destructive", btn.GetAttribute("data-variant"));
        Assert.Contains("text-destructive", btn.GetAttribute("class") ?? "");
    }

    // --- MenubarGroup ---

    [Fact]
    public void Group_Renders_Role_Group()
    {
        var cut = _ctx.Render<L.MenubarGroup>(p =>
            p.Add(g => g.ChildContent, (RenderFragment)(c => c.AddContent(0, "grouped"))));
        Assert.NotEmpty(cut.FindAll("[role='group']"));
        Assert.Contains("grouped", cut.Markup);
    }

    // --- MenubarCheckboxItem ---

    [Fact]
    public void CheckboxItem_Reflects_Checked_In_AriaChecked()
    {
        var cut = _ctx.Render<L.MenubarCheckboxItem>(p =>
        {
            p.Add(i => i.Checked, true);
            p.Add(i => i.ChildContent, (RenderFragment)(c => c.AddContent(0, "Show grid")));
        });
        Assert.Equal("true", cut.Find("[role='menuitemcheckbox']").GetAttribute("aria-checked"));
    }

    [Fact]
    public void CheckboxItem_Toggles_Optimistically_And_Emits_CheckedChanged()
    {
        bool? emitted = null;
        var cut = _ctx.Render<L.MenubarCheckboxItem>(p =>
        {
            p.Add(i => i.Checked, false);
            p.Add(i => i.CheckedChanged, EventCallback.Factory.Create<bool>(this, v => emitted = v));
            p.Add(i => i.ChildContent, (RenderFragment)(c => c.AddContent(0, "Show grid")));
        });

        cut.Find("button").Click();

        Assert.True(emitted);
        // Optimistic: aria-checked flips even though the parent hasn't pushed a new
        // Checked value back (uncontrolled/controlled-veto safety, like DropdownMenu).
        Assert.Equal("true", cut.Find("[role='menuitemcheckbox']").GetAttribute("aria-checked"));
    }

    // --- MenubarRadioGroup / MenubarRadioItem ---

    [Fact]
    public void RadioGroup_Marks_Only_The_Selected_Item_Checked()
    {
        var cut = _ctx.Render<L.MenubarRadioGroup>(p =>
        {
            p.Add(g => g.Value, "b");
            p.Add(g => g.ChildContent, (RenderFragment)(inner =>
            {
                inner.OpenComponent<L.MenubarRadioItem>(0);
                inner.AddAttribute(1, "Value", "a");
                inner.AddAttribute(2, "ChildContent", (RenderFragment)(c => c.AddContent(0, "A")));
                inner.CloseComponent();
                inner.OpenComponent<L.MenubarRadioItem>(3);
                inner.AddAttribute(4, "Value", "b");
                inner.AddAttribute(5, "ChildContent", (RenderFragment)(c => c.AddContent(0, "B")));
                inner.CloseComponent();
            }));
        });

        var items = cut.FindAll("[role='menuitemradio']");
        Assert.Equal(2, items.Count);
        Assert.Equal("false", items[0].GetAttribute("aria-checked"));
        Assert.Equal("true", items[1].GetAttribute("aria-checked"));
    }

    [Fact]
    public void RadioItem_Click_Selects_And_Emits_ValueChanged()
    {
        string? selected = null;
        var cut = _ctx.Render<L.MenubarRadioGroup>(p =>
        {
            p.Add(g => g.Value, "a");
            p.Add(g => g.ValueChanged, EventCallback.Factory.Create<string>(this, v => selected = v));
            p.Add(g => g.ChildContent, (RenderFragment)(inner =>
            {
                inner.OpenComponent<L.MenubarRadioItem>(0);
                inner.AddAttribute(1, "Value", "a");
                inner.AddAttribute(2, "ChildContent", (RenderFragment)(c => c.AddContent(0, "A")));
                inner.CloseComponent();
                inner.OpenComponent<L.MenubarRadioItem>(3);
                inner.AddAttribute(4, "Value", "b");
                inner.AddAttribute(5, "ChildContent", (RenderFragment)(c => c.AddContent(0, "B")));
                inner.CloseComponent();
            }));
        });

        cut.FindAll("[role='menuitemradio']")[1].Click();
        Assert.Equal("b", selected);
    }
}
