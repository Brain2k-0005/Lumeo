using AngleSharp.Dom;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.TreeView;

/// <summary>
/// PR #351 TreeView selection hardening:
///
/// Finding 6 — a same-content <c>Items</c> refresh re-anchors the selection by STRUCTURAL
/// PATH, not first-match-by-value, so a selected node whose Value is duplicated (or null)
/// stays on the same node instead of jumping to the first same-valued sibling.
///
/// Finding 7 — a controlled/seed <c>SelectedValues</c> that names a child which only
/// materializes later via <c>LoadChildren</c> is resolved when that branch loads, so the
/// child shows selected after its parent is expanded (previously the resolver only ran on
/// a parameter set, so it never bound).
/// </summary>
public class TreeViewReanchorAndLazySelectionTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TreeViewReanchorAndLazySelectionTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static IElement TreeItem(IRenderedComponent<L.TreeView<string>> cut, string text)
        => cut.FindAll("[role='treeitem']").First(el => el.Children[0].TextContent.Contains(text));

    private static IElement Row(IRenderedComponent<L.TreeView<string>> cut, string text)
        => TreeItem(cut, text).Children[0];

    private static List<L.TreeView<string>.TreeViewItem<string>> Empty() => [];

    // ---- Finding 6: duplicate / null value re-anchor by path ----

    // Two sibling leaves that SHARE a Value ("same") but have distinct text, under an
    // expanded root. Fresh instances each call = the async-reload shape reanchor handles.
    private static List<L.TreeView<string>.TreeViewItem<string>> DupTree() =>
    [
        new()
        {
            Text = "Root", Value = "root", IsExpanded = true,
            Children =
            [
                new() { Text = "First",  Value = "same" },
                new() { Text = "Second", Value = "same" }
            ]
        }
    ];

    [Fact]
    public async Task Selection_of_a_duplicate_valued_node_survives_refresh_on_the_same_node()
    {
        var cut = _ctx.Render<L.TreeView<string>>(p => p.Add(c => c.Items, DupTree()));

        // Select the SECOND of two "same"-valued siblings.
        await cut.InvokeAsync(() => Row(cut, "Second").Click());
        Assert.Equal("true", TreeItem(cut, "Second").GetAttribute("aria-selected"));
        Assert.Equal("false", TreeItem(cut, "First").GetAttribute("aria-selected"));

        // Same-content refresh with brand-new instances. A first-match-by-value reanchor
        // would jump the selection to "First"; path-based reanchor keeps it on "Second".
        cut.Render(p => p.Add(c => c.Items, DupTree()));

        var selected = cut.FindAll("[role='treeitem'][aria-selected='true']");
        Assert.Single(selected);
        Assert.Contains("Second", selected[0].Children[0].TextContent);
    }

    // A selected node whose Value is null, with a null-valued sibling too.
    private static List<L.TreeView<string>.TreeViewItem<string>> NullValueTree() =>
    [
        new()
        {
            Text = "Root", Value = "root", IsExpanded = true,
            Children =
            [
                new() { Text = "Folder A", Value = null },
                new() { Text = "Folder B", Value = null }
            ]
        }
    ];

    [Fact]
    public async Task Selection_of_a_null_valued_node_survives_refresh_on_the_same_node()
    {
        var cut = _ctx.Render<L.TreeView<string>>(p => p.Add(c => c.Items, NullValueTree()));

        await cut.InvokeAsync(() => Row(cut, "Folder B").Click());
        Assert.Equal("true", TreeItem(cut, "Folder B").GetAttribute("aria-selected"));

        cut.Render(p => p.Add(c => c.Items, NullValueTree()));

        var selected = cut.FindAll("[role='treeitem'][aria-selected='true']");
        Assert.Single(selected);
        Assert.Contains("Folder B", selected[0].Children[0].TextContent);
    }

    // ---- Round-3: a VANISHED duplicate/null-valued node carries its structural identity ----
    // (empty reload → the node's node re-materializes) so it re-binds the SAME node, not the
    // first value match. Carrying only the value would light up every same-valued sibling.

    [Fact]
    public async Task Duplicate_valued_node_that_vanishes_then_returns_rebinds_the_same_node_not_the_first_match()
    {
        var cut = _ctx.Render<L.TreeView<string>>(p => p.Add(c => c.Items, DupTree()));

        // Select the SECOND of two "same"-valued siblings.
        await cut.InvokeAsync(() => Row(cut, "Second").Click());
        Assert.Equal("true", TreeItem(cut, "Second").GetAttribute("aria-selected"));

        // The tree momentarily reloads EMPTY — the selected node vanishes into the pending
        // carry bucket (value "same" is a duplicate, so only its structural path can identify it).
        cut.Render(p => p.Add(c => c.Items, Empty()));
        Assert.Empty(cut.FindAll("[role='treeitem']"));

        // The real duplicate-valued tree returns with fresh instances. Carrying only the value
        // would re-select BOTH "same" nodes; the carried path re-binds exactly "Second".
        cut.Render(p => p.Add(c => c.Items, DupTree()));

        var selected = cut.FindAll("[role='treeitem'][aria-selected='true']");
        Assert.Single(selected);
        Assert.Contains("Second", selected[0].Children[0].TextContent);
    }

    [Fact]
    public async Task Null_valued_node_that_vanishes_then_returns_rebinds_the_same_node_not_the_first_match()
    {
        var cut = _ctx.Render<L.TreeView<string>>(p => p.Add(c => c.Items, NullValueTree()));

        await cut.InvokeAsync(() => Row(cut, "Folder B").Click());
        Assert.Equal("true", TreeItem(cut, "Folder B").GetAttribute("aria-selected"));

        // Empty reload: the null-valued selection can only be re-identified by its path.
        cut.Render(p => p.Add(c => c.Items, Empty()));
        Assert.Empty(cut.FindAll("[role='treeitem']"));

        cut.Render(p => p.Add(c => c.Items, NullValueTree()));

        // A value-based re-bind would match BOTH null-valued folders; the path picks Folder B.
        var selected = cut.FindAll("[role='treeitem'][aria-selected='true']");
        Assert.Single(selected);
        Assert.Contains("Folder B", selected[0].Children[0].TextContent);
    }

    [Fact]
    public async Task Reorder_that_makes_value_unique_follows_the_node_by_value_fallback()
    {
        // Structural path won't line up after a reorder; the value is unique here, so the
        // fallback follows it to the moved node instead of dropping the selection.
        List<L.TreeView<string>.TreeViewItem<string>> Ordered() =>
        [
            new()
            {
                Text = "Root", Value = "root", IsExpanded = true,
                Children =
                [
                    new() { Text = "Apple",  Value = "apple"  },
                    new() { Text = "Banana", Value = "banana" }
                ]
            }
        ];
        List<L.TreeView<string>.TreeViewItem<string>> Reordered() =>
        [
            new()
            {
                Text = "Root", Value = "root", IsExpanded = true,
                Children =
                [
                    new() { Text = "Banana", Value = "banana" },
                    new() { Text = "Apple",  Value = "apple"  }
                ]
            }
        ];

        var cut = _ctx.Render<L.TreeView<string>>(p => p.Add(c => c.Items, Ordered()));
        await cut.InvokeAsync(() => Row(cut, "Apple").Click());

        cut.Render(p => p.Add(c => c.Items, Reordered()));

        var selected = cut.FindAll("[role='treeitem'][aria-selected='true']");
        Assert.Single(selected);
        Assert.Contains("Apple", selected[0].Children[0].TextContent);
    }

    // ---- Finding 7: lazy-loaded child named by SelectedValues ----

    private static List<L.TreeView<string>.TreeViewItem<string>> LazyRoot() =>
    [
        new() { Text = "Lazy", Value = "lazy", IsLeaf = false }
    ];

    private static Func<L.TreeView<string>.TreeViewItem<string>, Task<List<L.TreeView<string>.TreeViewItem<string>>>>
        Loader() => _ => Task.FromResult(new List<L.TreeView<string>.TreeViewItem<string>>
    {
        new() { Text = "Child-A", Value = "a", IsLeaf = true },
        new() { Text = "Child-B", Value = "b", IsLeaf = true }
    });

    [Fact]
    public async Task Controlled_SelectedValues_for_a_lazy_child_binds_when_the_branch_loads()
    {
        List<string>? pushed = null;
        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, LazyRoot())
            .Add(c => c.LoadChildren, Loader())
            .Add(c => c.SelectedValues, new List<string> { "a" })
            .Add(c => c.SelectedValuesChanged, v => pushed = v));

        // The child doesn't exist yet, so nothing is selected on first render.
        Assert.Empty(cut.FindAll("[role='treeitem'][aria-selected='true']"));

        // Expand the lazy parent — LoadChildren materializes Child-A / Child-B.
        await cut.InvokeAsync(() => cut.Find("button[aria-label='Expand']").Click());

        // The pending "a" now binds to the freshly-loaded Child-A.
        Assert.Equal("true", TreeItem(cut, "Child-A").GetAttribute("aria-selected"));
        Assert.Equal("false", TreeItem(cut, "Child-B").GetAttribute("aria-selected"));
        // Resolving a value already in the parent's list must NOT echo a change back up.
        Assert.Null(pushed);
    }

    [Fact]
    public async Task Lazy_child_selection_also_binds_via_ExpandAllNodesAsync()
    {
        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, LazyRoot())
            .Add(c => c.LoadChildren, Loader())
            .Add(c => c.SelectedValues, new List<string> { "b" }));

        Assert.Empty(cut.FindAll("[role='treeitem'][aria-selected='true']"));

        await cut.InvokeAsync(() => cut.Instance.ExpandAllNodesAsync());

        Assert.Equal("true", TreeItem(cut, "Child-B").GetAttribute("aria-selected"));
        Assert.Equal("false", TreeItem(cut, "Child-A").GetAttribute("aria-selected"));
    }
}
