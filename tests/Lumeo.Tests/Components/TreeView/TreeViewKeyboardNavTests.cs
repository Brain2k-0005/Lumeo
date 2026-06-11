using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.TreeView;

/// <summary>
/// WAI-ARIA tree keyboard pattern: roving tabindex (exactly one tabbable node),
/// ArrowDown/Up across the VISIBLE nodes in depth-first order, ArrowRight
/// expand / move-into-child, ArrowLeft collapse / move-to-parent, Home/End,
/// plus the structural aria-level / aria-posinset / aria-setsize attributes.
/// Previously child nodes were keyboard-unreachable (tabindex -1, no Up/Down).
/// </summary>
public class TreeViewKeyboardNavTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TreeViewKeyboardNavTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static List<L.TreeView<string>.TreeViewItem<string>> Tree(bool expanded = true) =>
    [
        new()
        {
            Text = "Documents", Value = "docs", IsExpanded = expanded,
            Children =
            [
                new() { Text = "Resume", Value = "resume" },
                new() { Text = "Cover Letter", Value = "cover" }
            ]
        },
        new() { Text = "Images", Value = "imgs" }
    ];

    private static IElement TreeItem(IRenderedComponent<L.TreeView<string>> cut, string text)
        => cut.FindAll("[role='treeitem']").First(el => el.Children[0].TextContent.Contains(text));

    /// <summary>The single node currently holding the roving tabindex.</summary>
    private static IElement Active(IRenderedComponent<L.TreeView<string>> cut)
        => cut.Find("[role='treeitem'][tabindex='0']");

    private static string ActiveText(IRenderedComponent<L.TreeView<string>> cut)
        => Active(cut).Children[0].TextContent;

    private static async Task Key(IRenderedComponent<L.TreeView<string>> cut, string key)
        => await cut.InvokeAsync(() => Active(cut).KeyDown(new KeyboardEventArgs { Key = key }));

    [Fact]
    public void Exactly_one_node_is_tabbable_initially()
    {
        var cut = _ctx.Render<L.TreeView<string>>(p => p.Add(c => c.Items, Tree()));

        var tabbable = cut.FindAll("[role='treeitem'][tabindex='0']");
        Assert.Single(tabbable);
        Assert.Contains("Documents", tabbable[0].Children[0].TextContent);
        // Every other node is reachable via arrows, not Tab.
        Assert.Equal(3, cut.FindAll("[role='treeitem'][tabindex='-1']").Count);
    }

    [Fact]
    public async Task ArrowDown_moves_through_visible_nodes_depth_first()
    {
        var cut = _ctx.Render<L.TreeView<string>>(p => p.Add(c => c.Items, Tree()));

        Assert.Contains("Documents", ActiveText(cut));
        await Key(cut, "ArrowDown");
        Assert.Contains("Resume", ActiveText(cut));
        await Key(cut, "ArrowDown");
        Assert.Contains("Cover Letter", ActiveText(cut));
        await Key(cut, "ArrowDown");
        Assert.Contains("Images", ActiveText(cut));
        // Bottom of the tree: stays put.
        await Key(cut, "ArrowDown");
        Assert.Contains("Images", ActiveText(cut));

        Assert.Single(cut.FindAll("[role='treeitem'][tabindex='0']"));
    }

    [Fact]
    public async Task ArrowDown_skips_children_of_collapsed_nodes()
    {
        var cut = _ctx.Render<L.TreeView<string>>(p => p.Add(c => c.Items, Tree(expanded: false)));

        Assert.Contains("Documents", ActiveText(cut));
        await Key(cut, "ArrowDown");
        Assert.Contains("Images", ActiveText(cut));
    }

    [Fact]
    public async Task ArrowUp_moves_to_previous_visible_node()
    {
        var cut = _ctx.Render<L.TreeView<string>>(p => p.Add(c => c.Items, Tree()));

        await Key(cut, "End");
        Assert.Contains("Images", ActiveText(cut));
        await Key(cut, "ArrowUp");
        Assert.Contains("Cover Letter", ActiveText(cut));
        await Key(cut, "ArrowUp");
        Assert.Contains("Resume", ActiveText(cut));
        await Key(cut, "ArrowUp");
        Assert.Contains("Documents", ActiveText(cut));
        // Top of the tree: stays put.
        await Key(cut, "ArrowUp");
        Assert.Contains("Documents", ActiveText(cut));
    }

    [Fact]
    public async Task Home_and_End_jump_to_first_and_last_visible_node()
    {
        var cut = _ctx.Render<L.TreeView<string>>(p => p.Add(c => c.Items, Tree()));

        await Key(cut, "End");
        Assert.Contains("Images", ActiveText(cut));
        await Key(cut, "Home");
        Assert.Contains("Documents", ActiveText(cut));
    }

    [Fact]
    public async Task ArrowRight_expands_then_moves_into_first_child()
    {
        var cut = _ctx.Render<L.TreeView<string>>(p => p.Add(c => c.Items, Tree(expanded: false)));

        // First Right: expands without moving focus.
        await Key(cut, "ArrowRight");
        Assert.Equal("true", TreeItem(cut, "Documents").GetAttribute("aria-expanded"));
        Assert.Contains("Documents", ActiveText(cut));

        // Second Right: moves into the first child.
        await Key(cut, "ArrowRight");
        Assert.Contains("Resume", ActiveText(cut));
    }

    [Fact]
    public async Task ArrowLeft_collapses_or_moves_to_parent()
    {
        var cut = _ctx.Render<L.TreeView<string>>(p => p.Add(c => c.Items, Tree()));

        // Move onto the leaf child first.
        await Key(cut, "ArrowDown");
        Assert.Contains("Resume", ActiveText(cut));

        // Left on a leaf: focus moves to the parent.
        await Key(cut, "ArrowLeft");
        Assert.Contains("Documents", ActiveText(cut));

        // Left on an expanded node: collapses it, focus stays.
        await Key(cut, "ArrowLeft");
        Assert.Equal("false", TreeItem(cut, "Documents").GetAttribute("aria-expanded"));
        Assert.Contains("Documents", ActiveText(cut));
        Assert.Equal(2, cut.FindAll("[role='treeitem']").Count);
    }

    [Fact]
    public async Task Navigation_focuses_target_node_via_interop()
    {
        // Swap in the tracking interop (last registration wins) to observe
        // the FocusElement target id.
        var tracking = new TrackingInteropService();
        _ctx.Services.AddSingleton<Lumeo.Services.IComponentInteropService>(tracking);
        var cut = _ctx.Render<L.TreeView<string>>(p => p.Add(c => c.Items, Tree()));

        await Key(cut, "ArrowDown");

        var focusedId = Assert.Single(tracking.FocusedElementIds);
        Assert.False(string.IsNullOrEmpty(focusedId));
        Assert.Equal(Active(cut).GetAttribute("id"), focusedId);
    }

    [Fact]
    public void Nodes_expose_aria_level_posinset_setsize()
    {
        var cut = _ctx.Render<L.TreeView<string>>(p => p.Add(c => c.Items, Tree()));

        var documents = TreeItem(cut, "Documents");
        Assert.Equal("1", documents.GetAttribute("aria-level"));
        Assert.Equal("1", documents.GetAttribute("aria-posinset"));
        Assert.Equal("2", documents.GetAttribute("aria-setsize"));

        var resume = TreeItem(cut, "Resume");
        Assert.Equal("2", resume.GetAttribute("aria-level"));
        Assert.Equal("1", resume.GetAttribute("aria-posinset"));
        Assert.Equal("2", resume.GetAttribute("aria-setsize"));

        var cover = TreeItem(cut, "Cover Letter");
        Assert.Equal("2", cover.GetAttribute("aria-level"));
        Assert.Equal("2", cover.GetAttribute("aria-posinset"));

        var images = TreeItem(cut, "Images");
        Assert.Equal("1", images.GetAttribute("aria-level"));
        Assert.Equal("2", images.GetAttribute("aria-posinset"));
        Assert.Equal("2", images.GetAttribute("aria-setsize"));
    }
}
