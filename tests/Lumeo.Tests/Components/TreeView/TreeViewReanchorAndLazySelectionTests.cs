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
/// PATH, not first-match-by-value, so a selected node whose Value is UNIQUE among its
/// siblings stays on the same node instead of jumping to the first same-valued sibling.
///
/// Round-6 tightening — a structural path only PROVES identity when the value at the path is
/// unique among its siblings. When two siblings SHARE a (duplicate/null) Value the position is
/// NOT a reliable anchor (a reorder is indistinguishable from a same-content reload), so per the
/// ambiguity-DROP convention the selection DROPS rather than risk re-selecting the wrong
/// same-valued sibling. The cases below that used to assert "survives on the same node" for a
/// duplicate/null Value now assert the drop; sibling-UNIQUE values (even duplicated elsewhere in
/// the tree) still re-anchor by path, and a unique value that moved follows via the value fallback.
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

    // ---- Finding 6 + round-6: sibling-unique value re-anchors by path; duplicate/null DROPS ----

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
    public async Task Selection_of_a_duplicate_valued_node_drops_on_refresh_identity_is_unprovable()
    {
        var cut = _ctx.Render<L.TreeView<string>>(p => p.Add(c => c.Items, DupTree()));

        // Select the SECOND of two "same"-valued siblings.
        await cut.InvokeAsync(() => Row(cut, "Second").Click());
        Assert.Equal("true", TreeItem(cut, "Second").GetAttribute("aria-selected"));
        Assert.Equal("false", TreeItem(cut, "First").GetAttribute("aria-selected"));

        // Same-content refresh with brand-new instances. The two siblings share the Value "same",
        // so the structural position can't prove which one the user picked (a silent reorder is
        // indistinguishable from this reload). Per the ambiguity-DROP convention the selection
        // drops rather than guess — it must NOT resurrect on "First".
        cut.Render(p => p.Add(c => c.Items, DupTree()));

        Assert.Empty(cut.FindAll("[role='treeitem'][aria-selected='true']"));
    }

    // Root with ONE child valued "same", plus a SIBLING branch that ALSO holds a "same"-valued
    // node — so "same" is duplicated across the TREE but UNIQUE among each node's own siblings.
    private static List<L.TreeView<string>.TreeViewItem<string>> SiblingUniqueTree() =>
    [
        new()
        {
            Text = "Branch A", Value = "a", IsExpanded = true,
            Children = [ new() { Text = "Target", Value = "same" } ]
        },
        new()
        {
            Text = "Branch B", Value = "b", IsExpanded = true,
            Children = [ new() { Text = "Other", Value = "same" } ]
        }
    ];

    [Fact]
    public async Task Sibling_unique_value_still_reanchors_by_path_even_when_duplicated_elsewhere()
    {
        // "same" is unique among Target's siblings (Branch A has one child) though it also appears
        // under Branch B. The path stays a provable anchor, so a same-content reload keeps Target
        // selected — the drop rule must NOT over-reach to values that merely collide tree-wide
        // (a pure value fallback WOULD fail here: two "same" nodes exist).
        var cut = _ctx.Render<L.TreeView<string>>(p => p.Add(c => c.Items, SiblingUniqueTree()));

        await cut.InvokeAsync(() => Row(cut, "Target").Click());
        Assert.Equal("true", TreeItem(cut, "Target").GetAttribute("aria-selected"));

        cut.Render(p => p.Add(c => c.Items, SiblingUniqueTree()));

        var selected = cut.FindAll("[role='treeitem'][aria-selected='true']");
        Assert.Single(selected);
        Assert.Contains("Target", selected[0].Children[0].TextContent);
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
    public async Task Selection_of_a_null_valued_node_drops_on_refresh_identity_is_unprovable()
    {
        var cut = _ctx.Render<L.TreeView<string>>(p => p.Add(c => c.Items, NullValueTree()));

        await cut.InvokeAsync(() => Row(cut, "Folder B").Click());
        Assert.Equal("true", TreeItem(cut, "Folder B").GetAttribute("aria-selected"));

        // Both folders carry a null Value → the position can't prove which one was selected.
        // Ambiguity DROPS instead of re-selecting a null-valued sibling by index.
        cut.Render(p => p.Add(c => c.Items, NullValueTree()));

        Assert.Empty(cut.FindAll("[role='treeitem'][aria-selected='true']"));
    }

    // ---- Round-6: a VANISHED duplicate/null-valued node can't be re-identified when it returns ----
    // Both the carried path AND the value are ambiguous among same-valued siblings, so the carry
    // DROPS rather than re-bind an unprovable identity (a same-valued sibling by index). Only a
    // sibling-UNIQUE value survives a vanish→return (covered by the reorder-follows test below).

    [Fact]
    public async Task Duplicate_valued_node_that_vanishes_then_returns_drops_identity_is_unprovable()
    {
        var cut = _ctx.Render<L.TreeView<string>>(p => p.Add(c => c.Items, DupTree()));

        // Select the SECOND of two "same"-valued siblings.
        await cut.InvokeAsync(() => Row(cut, "Second").Click());
        Assert.Equal("true", TreeItem(cut, "Second").GetAttribute("aria-selected"));

        // The tree momentarily reloads EMPTY — the selected node vanishes into the pending
        // carry bucket (value "same" is a duplicate, so only a structural path could identify it).
        cut.Render(p => p.Add(c => c.Items, Empty()));
        Assert.Empty(cut.FindAll("[role='treeitem']"));

        // The duplicate-valued tree returns with fresh instances. Neither the carried path (two
        // siblings share "same") nor the value (two matches) can prove which node was "Second",
        // so the carry drops — nothing re-selects rather than lighting up a same-valued sibling.
        cut.Render(p => p.Add(c => c.Items, DupTree()));

        Assert.Empty(cut.FindAll("[role='treeitem'][aria-selected='true']"));
    }

    [Fact]
    public async Task Null_valued_node_that_vanishes_then_returns_drops_identity_is_unprovable()
    {
        var cut = _ctx.Render<L.TreeView<string>>(p => p.Add(c => c.Items, NullValueTree()));

        await cut.InvokeAsync(() => Row(cut, "Folder B").Click());
        Assert.Equal("true", TreeItem(cut, "Folder B").GetAttribute("aria-selected"));

        // Empty reload: the null-valued selection has no provable identity (both folders are null).
        cut.Render(p => p.Add(c => c.Items, Empty()));
        Assert.Empty(cut.FindAll("[role='treeitem']"));

        cut.Render(p => p.Add(c => c.Items, NullValueTree()));

        // Both the path and the value are ambiguous among the two null folders → the carry drops.
        Assert.Empty(cut.FindAll("[role='treeitem'][aria-selected='true']"));
    }

    // ---- Round-6 finding: a duplicate-valued sibling REORDER must drop, not rebind the wrong one ----

    [Fact]
    public async Task Duplicate_valued_siblings_reorder_drops_and_never_rebinds_the_wrong_sibling()
    {
        // Two "same"-valued siblings under an expanded root; select the SECOND (path [0,1]).
        List<L.TreeView<string>.TreeViewItem<string>> Ordered() =>
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
        // A genuine reorder swaps them: the node with Text "Second" moves to [0,0] and "First"
        // slides into [0,1]. The carried index path [0,1] now lands on the "First" row, which
        // ALSO carries "same" — the pre-fix code returned it (re-selecting the WRONG sibling).
        List<L.TreeView<string>.TreeViewItem<string>> Reordered() =>
        [
            new()
            {
                Text = "Root", Value = "root", IsExpanded = true,
                Children =
                [
                    new() { Text = "Second", Value = "same" },
                    new() { Text = "First",  Value = "same" }
                ]
            }
        ];

        var cut = _ctx.Render<L.TreeView<string>>(p => p.Add(c => c.Items, Ordered()));
        await cut.InvokeAsync(() => Row(cut, "Second").Click());
        Assert.Equal("true", TreeItem(cut, "Second").GetAttribute("aria-selected"));

        cut.Render(p => p.Add(c => c.Items, Reordered()));

        // Identity is unprovable among the two "same" siblings, so the selection DROPS — the
        // "First" row now sitting at the old path must NOT become selected.
        Assert.Empty(cut.FindAll("[role='treeitem'][aria-selected='true']"));
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

    // ---- Round-4: an ambiguous carry (path fails + value duplicated) DROPS immediately ----
    // so it can never re-bind the WRONG node when a later refresh leaves a single value match.

    [Fact]
    public async Task Ambiguous_carry_whose_path_fails_is_dropped_and_never_rebinds_a_later_single_match()
    {
        // Origin: two "same"-valued siblings under an expanded root; select the SECOND (path [0,1]).
        List<L.TreeView<string>.TreeViewItem<string>> Origin() =>
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

        // Refresh 1: the selected node vanishes; index path [0,1] now lands on a DIFFERENT value
        // (so the carried path can't verify it) while "same" appears TWICE elsewhere — an
        // ambiguous carry. Per the reanchor rule it must be DROPPED immediately, not left queued.
        List<L.TreeView<string>.TreeViewItem<string>> Shuffled() =>
        [
            new()
            {
                Text = "Root", Value = "root", IsExpanded = true,
                Children =
                [
                    new() { Text = "X", Value = "x" },
                    new() { Text = "Y", Value = "x" }
                ]
            },
            new() { Text = "Dup-A", Value = "same" },
            new() { Text = "Dup-B", Value = "same" }
        ];

        // Refresh 2: path [0,1] still fails, but now only ONE "same" remains. A carry left queued
        // by refresh 1 would re-bind it here (mis-selecting Dup-A); a dropped carry selects nothing.
        List<L.TreeView<string>.TreeViewItem<string>> Single() =>
        [
            new()
            {
                Text = "Root", Value = "root", IsExpanded = true,
                Children =
                [
                    new() { Text = "X", Value = "x" },
                    new() { Text = "Y", Value = "x" }
                ]
            },
            new() { Text = "Dup-A", Value = "same" },
            new() { Text = "Solo",  Value = "solo" }
        ];

        var cut = _ctx.Render<L.TreeView<string>>(p => p.Add(c => c.Items, Origin()));
        await cut.InvokeAsync(() => Row(cut, "Second").Click());
        Assert.Equal("true", TreeItem(cut, "Second").GetAttribute("aria-selected"));

        // The ambiguous carry is created AND dropped in this pass — nothing stays selected.
        cut.Render(p => p.Add(c => c.Items, Shuffled()));
        Assert.Empty(cut.FindAll("[role='treeitem'][aria-selected='true']"));

        // The dropped carry must NOT resurrect onto the now-unique "same" node.
        cut.Render(p => p.Add(c => c.Items, Single()));
        Assert.Empty(cut.FindAll("[role='treeitem'][aria-selected='true']"));
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

    // ---- Round-7 finding 2: a value seed that matches a VISIBLE node AND a lazy DUPLICATE ----
    // The public value contract selects EVERY node carrying a requested value. When a seed value
    // matches one already-visible node while another same-valued node is still behind LoadChildren,
    // the seed must stay pending until that branch loads — otherwise the lazy duplicate never binds.

    // "Visible" (a root leaf) carries "dup" and matches immediately; "Branch" is a lazy parent whose
    // child ALSO carries "dup" but only materializes when the branch is expanded.
    private static List<L.TreeView<string>.TreeViewItem<string>> DuplicateAcrossLazyTree() =>
    [
        new() { Text = "Visible", Value = "dup", IsLeaf = true },
        new() { Text = "Branch",  Value = "branch", IsLeaf = false }
    ];

    private static Func<L.TreeView<string>.TreeViewItem<string>, Task<List<L.TreeView<string>.TreeViewItem<string>>>>
        DupChildLoader() => _ => Task.FromResult(new List<L.TreeView<string>.TreeViewItem<string>>
    {
        new() { Text = "Lazy-Dup", Value = "dup", IsLeaf = true }
    });

    [Fact]
    public async Task Value_seed_matching_a_visible_node_still_binds_a_lazy_duplicate_when_it_loads()
    {
        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, DuplicateAcrossLazyTree())
            .Add(c => c.LoadChildren, DupChildLoader())
            .Add(c => c.SelectedValues, new List<string> { "dup" }));

        // The visible "dup" node is selected immediately; the lazy duplicate doesn't exist yet, so
        // it's the only selected row so far — and (the bug this guards) the seed must NOT be treated
        // as fully satisfied while a lazy branch could still hold another "dup".
        Assert.Equal("true", TreeItem(cut, "Visible").GetAttribute("aria-selected"));
        Assert.Single(cut.FindAll("[role='treeitem'][aria-selected='true']"));

        // Expand the lazy branch — its "dup"-valued child materializes.
        await cut.InvokeAsync(() => cut.Find("button[aria-label='Expand']").Click());

        // The value contract holds: BOTH the visible node and the freshly-loaded duplicate are now
        // selected (pre-fix, the seed had been cleared and the lazy duplicate stayed unselected).
        Assert.Equal("true", TreeItem(cut, "Visible").GetAttribute("aria-selected"));
        Assert.Equal("true", TreeItem(cut, "Lazy-Dup").GetAttribute("aria-selected"));
        Assert.Equal(2, cut.FindAll("[role='treeitem'][aria-selected='true']").Count);
    }
}
