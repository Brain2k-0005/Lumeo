using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.TreeView;

using Item = L.TreeView<string>.TreeViewItem<string>;

/// <summary>
/// UX feature: clicking anywhere on a PARENT row toggles its expansion in addition to selecting
/// it (VS Code file-tree pattern — larger hit/touch target), enabled by DEFAULT
/// (ExpandOnRowClick=true). Leaf rows only select. ExpandOnRowClick=false opts back into the old
/// split where the chevron is the sole expand trigger. The chevron works in both modes and a
/// selection-modifier (Ctrl/Meta/Shift) click never toggles expansion.
/// </summary>
public class TreeViewRowClickExpandTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TreeViewRowClickExpandTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // A collapsed parent with one child, plus a sibling leaf.
    private static List<Item> Tree() =>
    [
        new()
        {
            Text = "Music", Value = "music", IsExpanded = false,
            Children = [new() { Text = "playlist.m3u", Value = "playlist" }]
        },
        new() { Text = "readme.txt", Value = "readme" }
    ];

    private static IElement TreeItem(IRenderedComponent<L.TreeView<string>> cut, string text)
        => cut.FindAll("[role='treeitem']").First(el => el.Children[0].TextContent.Contains(text));

    private static IElement Row(IRenderedComponent<L.TreeView<string>> cut, string text)
        => TreeItem(cut, text).Children[0];

    // ---- default (ExpandOnRowClick=true): row click selects AND toggles expansion ----

    [Fact]
    public async Task Parent_row_click_selects_and_expands_by_default()
    {
        var cut = _ctx.Render<L.TreeView<string>>(p => p.Add(c => c.Items, Tree()));

        // Collapsed to start; its child is not rendered.
        Assert.Equal("false", TreeItem(cut, "Music").GetAttribute("aria-expanded"));
        Assert.DoesNotContain("playlist.m3u", cut.Markup);

        await cut.InvokeAsync(() => Row(cut, "Music").Click());

        // One row click both selected the parent and expanded it.
        Assert.Equal("true", TreeItem(cut, "Music").GetAttribute("aria-selected"));
        Assert.Equal("true", TreeItem(cut, "Music").GetAttribute("aria-expanded"));
        Assert.Contains("playlist.m3u", cut.Markup);
    }

    [Fact]
    public async Task Second_parent_row_click_collapses_but_keeps_selection()
    {
        var cut = _ctx.Render<L.TreeView<string>>(p => p.Add(c => c.Items, Tree()));

        await cut.InvokeAsync(() => Row(cut, "Music").Click()); // expand + select
        await cut.InvokeAsync(() => Row(cut, "Music").Click()); // collapse, stays selected

        Assert.Equal("false", TreeItem(cut, "Music").GetAttribute("aria-expanded"));
        Assert.Equal("true", TreeItem(cut, "Music").GetAttribute("aria-selected"));
        Assert.DoesNotContain("playlist.m3u", cut.Markup);
    }

    [Fact]
    public async Task Leaf_row_click_only_selects_and_has_no_expander()
    {
        var cut = _ctx.Render<L.TreeView<string>>(p => p.Add(c => c.Items, Tree()));

        var leaf = TreeItem(cut, "readme.txt");
        // A leaf exposes no expand chevron and no aria-expanded.
        Assert.Null(leaf.QuerySelector("button"));
        Assert.Null(leaf.GetAttribute("aria-expanded"));

        await cut.InvokeAsync(() => Row(cut, "readme.txt").Click());

        Assert.Equal("true", TreeItem(cut, "readme.txt").GetAttribute("aria-selected"));
    }

    [Fact]
    public async Task Chevron_click_still_toggles_without_selecting_in_default_mode()
    {
        var cut = _ctx.Render<L.TreeView<string>>(p => p.Add(c => c.Items, Tree()));

        // The chevron (stopPropagation) expands but never selects — even in row-click mode.
        await cut.InvokeAsync(() => cut.Find("button[aria-label='Expand']").Click());

        Assert.Equal("true", TreeItem(cut, "Music").GetAttribute("aria-expanded"));
        Assert.Equal("false", TreeItem(cut, "Music").GetAttribute("aria-selected"));
    }

    // ---- opt-out (ExpandOnRowClick=false): the old split ----

    [Fact]
    public async Task Opt_out_row_click_selects_without_expanding_and_chevron_still_works()
    {
        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, Tree())
            .Add(c => c.ExpandOnRowClick, false));

        await cut.InvokeAsync(() => Row(cut, "Music").Click());

        // Row click selected but did NOT expand.
        Assert.Equal("true", TreeItem(cut, "Music").GetAttribute("aria-selected"));
        Assert.Equal("false", TreeItem(cut, "Music").GetAttribute("aria-expanded"));

        // The chevron remains the expand trigger and does not disturb the selection.
        await cut.InvokeAsync(() => cut.Find("button[aria-label='Expand']").Click());
        Assert.Equal("true", TreeItem(cut, "Music").GetAttribute("aria-expanded"));
        Assert.Equal("true", TreeItem(cut, "Music").GetAttribute("aria-selected"));
    }

    // ---- lazy parents load on row click, exactly like the chevron ----

    [Fact]
    public async Task Lazy_parent_loads_children_on_row_click()
    {
        var calls = 0;
        Func<Item, Task<List<Item>>> loader = _ =>
        {
            calls++;
            return Task.FromResult(new List<Item>
            {
                new() { Text = "Child-A", Value = "a", IsLeaf = true },
                new() { Text = "Child-B", Value = "b", IsLeaf = true }
            });
        };
        var items = new List<Item> { new() { Text = "Lazy", Value = "lazy", IsLeaf = false } };

        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, items)
            .Add(c => c.LoadChildren, loader));

        Assert.Equal(0, calls);

        await cut.InvokeAsync(() => Row(cut, "Lazy").Click());

        // Row click fetched the branch, expanded it, and selected the parent.
        Assert.Equal(1, calls);
        Assert.True(items[0].ChildrenLoaded);
        Assert.Equal("true", TreeItem(cut, "Lazy").GetAttribute("aria-expanded"));
        Assert.Equal("true", TreeItem(cut, "Lazy").GetAttribute("aria-selected"));
        Assert.Contains("Child-A", cut.Markup);
    }

    // ---- selection-modifier clicks never toggle expansion ----

    [Fact]
    public async Task Modifier_click_selects_without_toggling_expansion()
    {
        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, Tree())
            .Add(c => c.MultiSelect, true));

        // Ctrl+click builds a multi-selection; the folder must not flap open.
        await cut.InvokeAsync(() => Row(cut, "Music").Click(new MouseEventArgs { CtrlKey = true }));

        Assert.Equal("true", TreeItem(cut, "Music").GetAttribute("aria-selected"));
        Assert.Equal("false", TreeItem(cut, "Music").GetAttribute("aria-expanded"));
        Assert.DoesNotContain("playlist.m3u", cut.Markup);
    }
}
