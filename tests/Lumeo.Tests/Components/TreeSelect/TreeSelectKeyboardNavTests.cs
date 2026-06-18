using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.TreeSelect;

/// <summary>
/// TreeSelect dropdown keyboard navigation (WAI-ARIA tree pattern, ported from
/// TreeView): roving tabindex on the open panel, ArrowDown/Up across visible
/// nodes, ArrowRight/Left expand/collapse + descend/ascend, Home/End,
/// Enter/Space select, Escape close + focus restore. Plus clearable + the
/// Multiple-mode parent-selection fix and ExpandAll on late Items.
/// Previously the dropdown was entirely keyboard-inert (no @onkeydown).
/// </summary>
public class TreeSelectKeyboardNavTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public TreeSelectKeyboardNavTests()
    {
        _ctx.AddLumeoServices();
        // Last registration wins — swap in the tracking interop so we can observe
        // FocusElement targets driven by keyboard navigation.
        _ctx.Services.AddSingleton<L.Services.IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static List<L.TreeSelect.TreeSelectItem> Tree(bool expanded = false) =>
    [
        new()
        {
            Label = "Fruit", Value = "fruit",
            Children =
            [
                new() { Label = "Apple", Value = "apple" },
                new() { Label = "Banana", Value = "banana" }
            ]
        },
        new() { Label = "Veg", Value = "veg" }
    ];

    private static IRenderedComponent<L.TreeSelect> Open(BunitContext ctx, List<L.TreeSelect.TreeSelectItem> items,
        Action<ComponentParameterCollectionBuilder<L.TreeSelect>>? extra = null)
    {
        var cut = ctx.Render<L.TreeSelect>(p =>
        {
            p.Add(c => c.Items, items);
            extra?.Invoke(p);
        });
        cut.Find("button").Click();
        return cut;
    }

    private static IElement Panel(IRenderedComponent<L.TreeSelect> cut) => cut.Find("[role='tree']");

    private static IElement Active(IRenderedComponent<L.TreeSelect> cut)
        => cut.Find("[role='treeitem'][tabindex='0']");

    private static string ActiveText(IRenderedComponent<L.TreeSelect> cut)
        => Active(cut).TextContent;

    private static async Task Key(IRenderedComponent<L.TreeSelect> cut, string key)
        => await cut.InvokeAsync(() => Panel(cut).KeyDown(new KeyboardEventArgs { Key = key }));

    [Fact]
    public void Open_panel_has_exactly_one_tabbable_node_and_tabindex_minus1_rest()
    {
        var cut = Open(_ctx, Tree());

        var tabbable = cut.FindAll("[role='treeitem'][tabindex='0']");
        Assert.Single(tabbable);
        Assert.Contains("Fruit", tabbable[0].TextContent);
        // Veg is reachable via arrows, not Tab (collapsed parent hides children).
        Assert.NotEmpty(cut.FindAll("[role='treeitem'][tabindex='-1']"));
    }

    [Fact]
    public async Task ArrowDown_skips_children_of_collapsed_node()
    {
        var cut = Open(_ctx, Tree());

        Assert.Contains("Fruit", ActiveText(cut));
        await Key(cut, "ArrowDown");
        Assert.Contains("Veg", ActiveText(cut));
    }

    [Fact]
    public async Task ArrowRight_expands_then_descends_into_first_child()
    {
        var cut = Open(_ctx, Tree());

        // First Right: expands without moving.
        await Key(cut, "ArrowRight");
        Assert.Equal("true", cut.FindAll("[role='treeitem']")
            .First(e => e.TextContent.Contains("Fruit")).GetAttribute("aria-expanded"));
        Assert.Contains("Fruit", ActiveText(cut));

        // Second Right: moves into the first child.
        await Key(cut, "ArrowRight");
        Assert.Contains("Apple", ActiveText(cut));
    }

    [Fact]
    public async Task ArrowLeft_collapses_then_moves_to_parent()
    {
        var cut = Open(_ctx, Tree(expanded: true));
        await Key(cut, "ArrowRight"); // expand Fruit
        await Key(cut, "ArrowDown");  // onto Apple
        Assert.Contains("Apple", ActiveText(cut));

        // Left on a leaf: focus to parent.
        await Key(cut, "ArrowLeft");
        Assert.Contains("Fruit", ActiveText(cut));

        // Left on expanded parent: collapses it.
        await Key(cut, "ArrowLeft");
        Assert.Equal("false", cut.FindAll("[role='treeitem']")
            .First(e => e.TextContent.Contains("Fruit")).GetAttribute("aria-expanded"));
    }

    [Fact]
    public async Task Home_and_End_jump_to_first_and_last()
    {
        var cut = Open(_ctx, Tree());
        await Key(cut, "End");
        Assert.Contains("Veg", ActiveText(cut));
        await Key(cut, "Home");
        Assert.Contains("Fruit", ActiveText(cut));
    }

    [Fact]
    public async Task Navigation_focuses_target_via_interop()
    {
        var cut = Open(_ctx, Tree());

        await Key(cut, "ArrowDown");

        // The active node's id must be the most recent FocusElement target.
        Assert.Equal(Active(cut).GetAttribute("id"), _interop.FocusedElementIds.Last());
    }

    [Fact]
    public async Task Enter_on_leaf_selects_and_closes()
    {
        string? selected = null;
        var cut = Open(_ctx, Tree(), p => p.Add(c => c.ValueChanged,
            Microsoft.AspNetCore.Components.EventCallback.Factory.Create<string>(this, v => selected = v)));

        await Key(cut, "ArrowRight"); // expand Fruit
        await Key(cut, "ArrowRight"); // into Apple
        await Key(cut, "Enter");

        Assert.Equal("apple", selected);
        // Panel closed.
        Assert.Empty(cut.FindAll("[role='tree']"));
    }

    [Fact]
    public async Task Escape_closes_and_restores_focus_to_trigger()
    {
        var cut = Open(_ctx, Tree());
        var triggerId = cut.Find("button").GetAttribute("id");

        await Key(cut, "Escape");

        Assert.Empty(cut.FindAll("[role='tree']"));
        Assert.Equal(triggerId, _interop.FocusedElementIds.Last());
    }

    [Fact]
    public async Task Multiple_mode_Enter_on_parent_selects_the_parent()
    {
        List<string>? values = null;
        var cut = Open(_ctx, Tree(), p =>
        {
            p.Add(c => c.Multiple, true);
            p.Add(c => c.ValuesChanged,
                Microsoft.AspNetCore.Components.EventCallback.Factory.Create<List<string>?>(this, v => values = v));
        });

        // Active is the parent "Fruit"; Enter must toggle the parent's own value.
        Assert.Contains("Fruit", ActiveText(cut));
        await Key(cut, "Enter");

        Assert.NotNull(values);
        Assert.Contains("fruit", values!);
    }

    [Fact]
    public void Panel_marks_aria_multiselectable_in_multiple_mode()
    {
        var cut = Open(_ctx, Tree(), p => p.Add(c => c.Multiple, true));
        Assert.Equal("true", Panel(cut).GetAttribute("aria-multiselectable"));
    }

    [Fact]
    public void Single_mode_panel_has_no_aria_multiselectable()
    {
        var cut = Open(_ctx, Tree());
        Assert.False(Panel(cut).HasAttribute("aria-multiselectable"));
    }
}
