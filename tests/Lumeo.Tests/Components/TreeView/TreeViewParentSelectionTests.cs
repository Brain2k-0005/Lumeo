using AngleSharp.Dom;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.TreeView;

/// <summary>
/// GH #350 regression: "parent node selection behaves incorrectly." Container/parent
/// nodes routinely carry no domain Value (default(T) == null) or a duplicate Value across
/// branches. Selection used to be keyed by Value, so clicking ONE value-less parent lit up
/// EVERY value-less node — including a nested one that rendered as a stray dark bar among
/// another parent's children. Selection must be keyed by node IDENTITY: a click selects
/// exactly the clicked node, its background never bleeds onto children, and selection is
/// independent of expansion.
/// </summary>
public class TreeViewParentSelectionTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TreeViewParentSelectionTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static IElement TreeItem(IRenderedComponent<L.TreeView<string>> cut, string text)
        => cut.FindAll("[role='treeitem']").First(el => el.Children[0].TextContent.Contains(text));

    private static IElement Row(IRenderedComponent<L.TreeView<string>> cut, string text)
        => TreeItem(cut, text).Children[0];

    private static IReadOnlyList<IElement> Selected(IRenderedComponent<L.TreeView<string>> cut)
        => cut.FindAll("[role='treeitem'][aria-selected='true']");

    // Two parents with NO Value (both default to null) plus one value-less nested group.
    // Under the old value-keyed selection, clicking "PhotosFolder" selected PhotosFolder,
    // MusicFolder and the nested "Vacation" group all at once (three null values collide).
    private static List<L.TreeView<string>.TreeViewItem<string>> ValuelessTree() =>
    [
        new()
        {
            Text = "PhotosFolder", IsExpanded = true,
            Children =
            [
                new() { Text = "Vacation" }, // value-less nested group (null)
                new() { Text = "profile.png", Value = "profile" }
            ]
        },
        new() { Text = "MusicFolder" } // value-less sibling (null)
    ];

    // (a) A single click on a value-less parent selects EXACTLY that parent row.
    [Fact]
    public async Task Clicking_valueless_parent_selects_only_that_parent()
    {
        var cut = _ctx.Render<L.TreeView<string>>(p => p.Add(c => c.Items, ValuelessTree()));

        await cut.InvokeAsync(() => Row(cut, "PhotosFolder").Click());

        var selected = Selected(cut);
        Assert.Single(selected);
        Assert.Contains("PhotosFolder", selected[0].Children[0].TextContent);
        Assert.Equal("false", TreeItem(cut, "MusicFolder").GetAttribute("aria-selected"));

        // aria-selected sits on the treeitem; the highlight class is scoped to the label ROW.
        // Use text-accent-foreground as the marker — it is unique to the selected state,
        // whereas "bg-accent" also appears in every row's "hover:bg-accent/50".
        Assert.Contains("text-accent-foreground", Row(cut, "PhotosFolder").ClassName);
        Assert.DoesNotContain("text-accent-foreground", Row(cut, "MusicFolder").ClassName);
    }

    // (b) The selected parent's background must NOT be inherited by its children — including
    // a nested child that shares the parent's (null) Value.
    [Fact]
    public async Task Selected_parent_background_not_inherited_by_children()
    {
        // Opt out of row-click expansion (default) so the pre-expanded parent stays open and its
        // children remain rendered — the bleed-through this asserts is a styling concern
        // independent of the expand-on-click behavior.
        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, ValuelessTree())
            .Add(c => c.ExpandOnRowClick, false));

        await cut.InvokeAsync(() => Row(cut, "PhotosFolder").Click());

        // The value-less nested "Vacation" group must NOT be selected or highlighted.
        Assert.Equal("false", TreeItem(cut, "Vacation").GetAttribute("aria-selected"));
        Assert.DoesNotContain("text-accent-foreground", Row(cut, "Vacation").ClassName);
        Assert.Equal("false", TreeItem(cut, "profile.png").GetAttribute("aria-selected"));

        // The children group element that lives inside the selected parent's treeitem must
        // not carry the selected background (the highlight is a sibling of the group, not a
        // wrapper around it).
        var parentItem = TreeItem(cut, "PhotosFolder");
        var group = parentItem.Children.FirstOrDefault(c => c.GetAttribute("role") == "group");
        Assert.NotNull(group);
        Assert.DoesNotContain("text-accent-foreground", group!.ClassName);
    }

    // Duplicate NON-null values across two sibling nodes: identity still distinguishes them.
    [Fact]
    public async Task Clicking_one_of_two_duplicate_valued_nodes_selects_only_that_node()
    {
        var tree = new List<L.TreeView<string>.TreeViewItem<string>>
        {
            new() { Text = "Alpha", Value = "dup" },
            new() { Text = "Beta", Value = "dup" }
        };
        var cut = _ctx.Render<L.TreeView<string>>(p => p.Add(c => c.Items, tree));

        await cut.InvokeAsync(() => Row(cut, "Alpha").Click());

        var selected = Selected(cut);
        Assert.Single(selected);
        Assert.Contains("Alpha", selected[0].Children[0].TextContent);
        Assert.Equal("false", TreeItem(cut, "Beta").GetAttribute("aria-selected"));
    }

    // (c) In chevron-only mode (ExpandOnRowClick=false) selection and expansion are fully
    // independent: selecting a collapsed parent does not expand it, and toggling expansion (via
    // the chevron) does not disturb the selection. (The DEFAULT row-click-also-expands behavior
    // is covered in TreeViewRowClickExpandTests.)
    [Fact]
    public async Task Selection_and_expansion_are_independent()
    {
        var tree = new List<L.TreeView<string>.TreeViewItem<string>>
        {
            new()
            {
                Text = "Docs", IsExpanded = false,
                Children = [new() { Text = "file", Value = "file" }]
            }
        };
        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, tree)
            .Add(c => c.ExpandOnRowClick, false));

        // Clicking the label row selects the parent but leaves it collapsed.
        await cut.InvokeAsync(() => Row(cut, "Docs").Click());
        Assert.Equal("true", TreeItem(cut, "Docs").GetAttribute("aria-selected"));
        Assert.Equal("false", TreeItem(cut, "Docs").GetAttribute("aria-expanded"));

        // Toggling the chevron expands the node WITHOUT changing the selection.
        var chevron = Row(cut, "Docs").QuerySelector("button");
        Assert.NotNull(chevron);
        await cut.InvokeAsync(() => chevron!.Click());

        Assert.Equal("true", TreeItem(cut, "Docs").GetAttribute("aria-expanded"));
        Assert.Equal("true", TreeItem(cut, "Docs").GetAttribute("aria-selected"));
        Assert.Single(Selected(cut));
    }

    // Identity-tracked selection must survive a same-content Items refresh (new instances) —
    // re-anchored by stable Value so an async reload keeps the value-less parent highlighted.
    [Fact]
    public async Task Selection_survives_same_content_refresh_for_valued_node()
    {
        var cut = _ctx.Render<L.TreeView<string>>(p => p.Add(c => c.Items, ValuelessTree()));

        await cut.InvokeAsync(() => Row(cut, "profile.png").Click());
        Assert.Equal("true", TreeItem(cut, "profile.png").GetAttribute("aria-selected"));

        // Fresh instances, same content (async reload shape).
        cut.Render(p => p.Add(c => c.Items, ValuelessTree()));

        var selected = Selected(cut);
        Assert.Single(selected);
        Assert.Contains("profile.png", selected[0].Children[0].TextContent);
    }
}
