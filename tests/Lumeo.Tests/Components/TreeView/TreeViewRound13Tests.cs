using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.TreeView;

using Item = L.TreeView<string>.TreeViewItem<string>;

/// <summary>
/// PR #351 round-13 (Codex): four findings on the canonical post-await model plus the conformance sweep.
///   1. A search-filtered display is a separate ToList() snapshot, so an in-place mutation with NO
///      selection never re-materialized it (change detection must not hinge on selection state).
///   2. WalkPath value-verified only the LEAF, so a reordered duplicate/null-valued ANCESTOR could walk
///      the path into the wrong subtree — every segment must be sibling-unique now (NodePath value chain).
///   3. A collapse WHILE a lazy load is pending was overwritten by the success path's unconditional
///      re-expand — the in-flight registry now records the supersession and the completion honors it.
///   4. Expand-all's lazy completion attached children to the STALE instance and never swept — it now
///      re-resolves via ResolveExpansionTarget and sweeps, exactly like the per-row path.
/// Each test is a genuine regression against the described defect, not a tautology.
/// </summary>
public class TreeViewRound13Tests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TreeViewRound13Tests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static IElement TreeItem(IRenderedComponent<L.TreeView<string>> cut, string text)
        => cut.FindAll("[role='treeitem']").First(el => el.Children[0].TextContent.Contains(text));

    private static IElement Row(IRenderedComponent<L.TreeView<string>> cut, string text)
        => TreeItem(cut, text).Children[0];

    // ---- Finding 1: a filtered display re-materializes on an in-place mutation with NO selection ----

    [Fact]
    public void Filtered_display_rebuilds_on_an_in_place_retitle_with_no_selection()
    {
        var items = new List<Item>
        {
            new() { Text = "Report", Value = "report" },
            new() { Text = "Readme", Value = "readme" }
        };

        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, items)
            .Add(c => c.ShowSearch, true));

        // Filter to "Re" → _displayItems becomes a filtered ToList() SNAPSHOT (not the live Items). There is
        // NO selection and NO pending state, so needsResolve stays false on the next render.
        cut.Find("input").Input("Re");
        Assert.Contains("Report", cut.Markup);

        // In-place retitle (SAME list reference): replace items[0] with a fresh instance whose label changed
        // but still matches the filter. Without an unconditional filtered rebuild the render keeps walking the
        // stale snapshot (which still references the OLD "Report" instance) and the new label never appears.
        items[0] = new() { Text = "Renamed", Value = "report" };
        cut.Render(p => p.Add(c => c.Items, items));

        Assert.Contains("Renamed", cut.Markup);       // filtered display re-materialized off the mutated Items
        Assert.DoesNotContain("Report", cut.Markup);   // the stale row is gone
    }

    // ---- Finding 2: reanchor DROPS rather than walk into a reordered duplicate-valued ancestor ----

    [Fact]
    public async Task Reanchor_drops_rather_than_walk_a_path_through_a_reordered_ambiguous_ancestor()
    {
        // Two null-valued root folders, each holding a same-valued ("shared") child. The child's value is
        // sibling-unique WITHIN its folder, so a leaf-only path check passes — but the ANCESTOR (null) is
        // ambiguous, so an in-place reorder of the roots must NOT let the old path walk into the OTHER folder.
        var items = new List<Item>
        {
            new() { Text = "FolderA", Value = null, IsExpanded = true,
                    Children = [ new() { Text = "Alpha-child", Value = "shared" } ] },
            new() { Text = "FolderB", Value = null, IsExpanded = true,
                    Children = [ new() { Text = "Beta-child", Value = "shared" } ] }
        };

        var cut = _ctx.Render<L.TreeView<string>>(p => p.Add(c => c.Items, items));

        await cut.InvokeAsync(() => Row(cut, "Alpha-child").Click());
        Assert.Equal("true", TreeItem(cut, "Alpha-child").GetAttribute("aria-selected"));

        // In-place rebuild (SAME reference): replace BOTH folders with fresh instances in SWAPPED order, each
        // with a fresh same-valued child. The selected Alpha-child instance vanishes → reanchor path-walks the
        // snapshot [0,0] with value chain [null,"shared"]. At depth 0 the new index-0 folder is null but null
        // is NOT sibling-unique (both roots null) → the WHOLE path drops (round-13 f2). "shared" is duplicated
        // → no unique-value fallback → the selection drops rather than mis-bind to the OTHER folder's child now
        // sitting at [0,0] (the leaf-only mis-anchor this fix prevents).
        items[0] = new() { Text = "FolderB", Value = null, IsExpanded = true,
                           Children = [ new() { Text = "Beta-child", Value = "shared" } ] };
        items[1] = new() { Text = "FolderA", Value = null, IsExpanded = true,
                           Children = [ new() { Text = "Alpha-child", Value = "shared" } ] };
        cut.Render(p => p.Add(c => c.Items, items));

        // No row is selected — crucially NOT the Beta-child now at [0,0].
        Assert.Empty(cut.FindAll("[role='treeitem'][aria-selected='true']"));
    }

    // ---- Finding 3: a row-click collapse WHILE loading survives the load completion ----

    [Fact]
    public async Task Row_click_collapse_during_lazy_load_is_preserved_when_the_load_completes()
    {
        var gate = new TaskCompletionSource<List<Item>>();
        Func<Item, Task<List<Item>>> loader = _ => gate.Task; // hangs until released

        var items = new List<Item> { new() { Text = "Folder", Value = "folder", IsLeaf = false } };

        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, items)
            .Add(c => c.LoadChildren, loader));

        // Expand → starts the hanging load (spinner, IsExpanded=true set before the await).
        await cut.InvokeAsync(() => Row(cut, "Folder").Click());
        Assert.True(items[0].IsExpanded);
        Assert.True(items[0].IsLoading);
        Assert.Contains("animate-spin", cut.Markup);

        // Collapse WHILE loading (second row click) → marks the in-flight op superseded.
        await cut.InvokeAsync(() => Row(cut, "Folder").Click());
        Assert.False(items[0].IsExpanded, "the collapse-during-load flips IsExpanded back");

        // Release: children attach (ChildrenLoaded) but the node MUST stay collapsed (round-13 f3).
        await cut.InvokeAsync(() => gate.SetResult([ new() { Text = "Doc", Value = "doc" } ]));

        Assert.True(items[0].ChildrenLoaded, "the children still attach");
        Assert.NotNull(items[0].Children);
        Assert.False(items[0].IsExpanded, "the completed load does NOT re-expand over the user's collapse");
        Assert.False(items[0].IsLoading);
        Assert.Equal("false", TreeItem(cut, "Folder").GetAttribute("aria-expanded"));
        Assert.DoesNotContain("Doc", cut.Markup); // collapsed → children not rendered
    }

    // ---- Finding 3 (ArrowLeft entry point): a keyboard collapse during load also survives ----

    [Fact]
    public async Task ArrowLeft_collapse_during_lazy_load_is_preserved_when_the_load_completes()
    {
        var gate = new TaskCompletionSource<List<Item>>();
        Func<Item, Task<List<Item>>> loader = _ => gate.Task;

        var items = new List<Item> { new() { Text = "Folder", Value = "folder", IsLeaf = false } };

        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, items)
            .Add(c => c.LoadChildren, loader));

        // ArrowRight expands a collapsed lazy node → starts the hanging load.
        await cut.InvokeAsync(() => TreeItem(cut, "Folder").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" }));
        Assert.True(items[0].IsLoading);
        Assert.True(items[0].IsExpanded);

        // ArrowLeft collapses WHILE loading → supersede.
        await cut.InvokeAsync(() => TreeItem(cut, "Folder").KeyDown(new KeyboardEventArgs { Key = "ArrowLeft" }));
        Assert.False(items[0].IsExpanded);

        await cut.InvokeAsync(() => gate.SetResult([ new() { Text = "Doc", Value = "doc" } ]));

        Assert.True(items[0].ChildrenLoaded);
        Assert.False(items[0].IsExpanded, "the ArrowLeft collapse survives the load completion");
        Assert.DoesNotContain("Doc", cut.Markup);
    }

    // ---- Finding 4: expand-all's lazy completion re-attaches to the FRESH node after a rebuild ----

    [Fact]
    public async Task Expand_all_lazy_completion_reattaches_to_the_fresh_node_after_an_in_place_rebuild()
    {
        var gate = new TaskCompletionSource<List<Item>>();
        Func<Item, Task<List<Item>>> loader = _ => gate.Task;

        // UNIQUE-valued lazy root, so after a controlled/immutable rebuild replaces it, ResolveExpansionTarget
        // can re-resolve the fresh instance by tree-unique value and attach the loaded children to IT.
        var original = new List<Item> { new() { Text = "Folder", Value = "folder", IsLeaf = false } };

        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, original)
            .Add(c => c.LoadChildren, loader));

        // Kick off expand-all (fire-and-forget so the hanging load doesn't block the dispatcher).
        Task expand = null!;
        await cut.InvokeAsync(() => { expand = cut.Instance.ExpandAllNodesAsync(); });
        Assert.True(original[0].IsLoading);
        Assert.Contains("animate-spin", cut.Markup);

        // Controlled/immutable rebuild MID-LOAD: hand a NEW list REFERENCE whose fresh same-valued Folder
        // copied the optimistic IsLoading/IsExpanded. The in-flight load still holds the STALE instance;
        // attaching the children to it would strand this fresh row spinning and childless (the f4 defect).
        var rebuilt = new List<Item>
        {
            new() { Text = "Folder", Value = "folder", IsLeaf = false, IsExpanded = true, IsLoading = true }
        };
        cut.Render(p => p.Add(c => c.Items, rebuilt));

        // Release: the completion re-resolves to the FRESH node (unique "folder") and attaches the children
        // there, clearing the copied spinner — the expand-all mirror of the per-row ResolveExpansionTarget.
        // The loaded child is a LEAF so expand-all's recursion stops (a non-leaf would be lazy-loaded again).
        await cut.InvokeAsync(() => gate.SetResult([ new() { Text = "Doc", Value = "doc", IsLeaf = true } ]));
        await expand;

        Assert.True(rebuilt[0].ChildrenLoaded, "children attach to the fresh re-resolved node, not the stale one");
        Assert.False(rebuilt[0].IsLoading, "no stranded spinner on the fresh node");
        Assert.Contains("Doc", cut.Markup);
        Assert.DoesNotContain("animate-spin", cut.Markup);
    }

    // ---- Sweep site: collapse-all during a per-node lazy load prevents re-expansion on completion ----

    [Fact]
    public async Task Collapse_all_during_a_per_node_lazy_load_prevents_re_expansion_on_completion()
    {
        var gate = new TaskCompletionSource<List<Item>>();
        Func<Item, Task<List<Item>>> loader = _ => gate.Task;

        var items = new List<Item> { new() { Text = "Folder", Value = "folder", IsLeaf = false } };

        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, items)
            .Add(c => c.LoadChildren, loader));

        // Row-click starts the per-node lazy load (hangs).
        await cut.InvokeAsync(() => Row(cut, "Folder").Click());
        Assert.True(items[0].IsLoading);
        Assert.True(items[0].IsExpanded);

        // Collapse-all while the per-node load is pending. It is NOT gated by _expandGeneration, so it must
        // supersede the in-flight op explicitly (round-13 f3 sweep site) — otherwise the load re-expands it.
        await cut.InvokeAsync(() => cut.Instance.CollapseAllNodes());
        Assert.False(items[0].IsExpanded);

        // Release: children attach but the node stays collapsed.
        await cut.InvokeAsync(() => gate.SetResult([ new() { Text = "Doc", Value = "doc" } ]));

        Assert.True(items[0].ChildrenLoaded);
        Assert.False(items[0].IsExpanded, "collapse-all supersedes the pending load → no re-expand");
        Assert.False(items[0].IsLoading);
        Assert.DoesNotContain("Doc", cut.Markup);
    }
}
