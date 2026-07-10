using AngleSharp.Dom;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.TreeView;

/// <summary>
/// PR #351 round-2: the TreeView SELECTION LIFECYCLE (seed → pending → resolved →
/// superseded) consolidated into one model, covered end-to-end. Each test pins one of
/// the four transitions the accreted spot-fixes missed:
///
///  (a) a seed value with no matching node yet stays pending and resolves on ANY later
///      Items refresh — not only a lazy-load callback.
///  (b) an empty/null Items reload carries the selection forward as pending instead of
///      dropping it, so it returns when a non-empty tree arrives.
///  (c) an interactive selection supersedes a stale pending lazy seed — single-select
///      clears it (no resurrection + co-select); multi-select keeps the seed's other values.
///  (d) an Items reload that unloads a branch while SelectedValues keeps the SAME list
///      reference still repopulates pending (resolution isn't gated on reference identity).
/// </summary>
public class TreeViewSelectionLifecycleTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TreeViewSelectionLifecycleTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static IElement TreeItem(IRenderedComponent<L.TreeView<string>> cut, string text)
        => cut.FindAll("[role='treeitem']").First(el => el.Children[0].TextContent.Contains(text));

    private static IElement Row(IRenderedComponent<L.TreeView<string>> cut, string text)
        => TreeItem(cut, text).Children[0];

    private static List<L.TreeView<string>.TreeViewItem<string>> Empty() => [];

    // Root (expanded) → one child "Node-X" carrying value "x". Fresh instances per call.
    private static List<L.TreeView<string>.TreeViewItem<string>> TreeWithX() =>
    [
        new()
        {
            Text = "Root", Value = "root", IsExpanded = true,
            Children = [ new() { Text = "Node-X", Value = "x" } ]
        }
    ];

    private static List<L.TreeView<string>.TreeViewItem<string>> SingleLeaf(string value, string text) =>
        [ new() { Text = text, Value = value } ];

    private static List<L.TreeView<string>.TreeViewItem<string>> LazyRoot(bool withEagerSibling = false)
    {
        var items = new List<L.TreeView<string>.TreeViewItem<string>>
        {
            new() { Text = "Lazy", Value = "lazy", IsLeaf = false }
        };
        if (withEagerSibling)
            items.Add(new() { Text = "Other", Value = "other", IsLeaf = true });
        return items;
    }

    private static Func<L.TreeView<string>.TreeViewItem<string>, Task<List<L.TreeView<string>.TreeViewItem<string>>>>
        Loader() => _ => Task.FromResult(new List<L.TreeView<string>.TreeViewItem<string>>
    {
        new() { Text = "Child-A", Value = "a", IsLeaf = true },
        new() { Text = "Child-B", Value = "b", IsLeaf = true }
    });

    // ---- (a) seed absent on first render resolves on a later plain Items refresh ----

    [Fact]
    public void Seed_for_a_value_absent_on_first_render_resolves_on_a_later_items_refresh()
    {
        // Same list instance across both renders — proves resolution does not depend on a
        // SelectedValues reference change (the seed was set while Items was empty).
        var seed = new List<string> { "x" };

        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, Empty())
            .Add(c => c.SelectedValues, seed));

        // Nothing to select yet — the tree is empty on first render.
        Assert.Empty(cut.FindAll("[role='treeitem'][aria-selected='true']"));

        // A plain (non-lazy) Items refresh brings in the node the seed named.
        cut.Render(p => p
            .Add(c => c.Items, TreeWithX())
            .Add(c => c.SelectedValues, seed));

        var selected = cut.FindAll("[role='treeitem'][aria-selected='true']");
        Assert.Single(selected);
        Assert.Contains("Node-X", selected[0].Children[0].TextContent);
    }

    // ---- (b) empty/null Items reload must not wipe the selection permanently ----

    [Fact]
    public async Task Empty_items_reload_does_not_wipe_selection_permanently()
    {
        var cut = _ctx.Render<L.TreeView<string>>(p => p.Add(c => c.Items, SingleLeaf("keep", "Keep")));

        await cut.InvokeAsync(() => Row(cut, "Keep").Click());
        Assert.Equal("true", TreeItem(cut, "Keep").GetAttribute("aria-selected"));

        // An async refetch momentarily returns nothing — the tree reloads empty.
        cut.Render(p => p.Add(c => c.Items, Empty()));
        Assert.Empty(cut.FindAll("[role='treeitem']"));

        // The real tree returns (fresh instances). The selection must come back, not be gone.
        cut.Render(p => p.Add(c => c.Items, SingleLeaf("keep", "Keep")));

        var selected = cut.FindAll("[role='treeitem'][aria-selected='true']");
        Assert.Single(selected);
        Assert.Contains("Keep", selected[0].Children[0].TextContent);
    }

    // ---- (c) interactive selection supersedes a stale pending lazy seed ----

    [Fact]
    public async Task Single_select_click_supersedes_a_pending_lazy_seed_no_resurrection()
    {
        // Seed names "a", a lazy child that isn't loaded yet → it sits pending.
        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, LazyRoot(withEagerSibling: true))
            .Add(c => c.LoadChildren, Loader())
            .Add(c => c.SelectedValues, new List<string> { "a" }));

        Assert.Empty(cut.FindAll("[role='treeitem'][aria-selected='true']"));

        // The user makes an authoritative single-select choice on a DIFFERENT (loaded) node.
        await cut.InvokeAsync(() => Row(cut, "Other").Click());
        Assert.Equal("true", TreeItem(cut, "Other").GetAttribute("aria-selected"));

        // Now expand the lazy branch, materializing Child-A. The superseded seed must NOT
        // resurrect and co-select it (single-select = exactly one selected node).
        await cut.InvokeAsync(() => cut.Find("button[aria-label='Expand']").Click());

        Assert.Equal("false", TreeItem(cut, "Child-A").GetAttribute("aria-selected"));
        var selected = cut.FindAll("[role='treeitem'][aria-selected='true']");
        Assert.Single(selected);
        Assert.Contains("Other", selected[0].Children[0].TextContent);
    }

    [Fact]
    public async Task Multi_select_click_keeps_the_seeds_other_pending_values()
    {
        // Multi-select rule: a click is a delta on the set, so the seed's OTHER pending
        // values remain wanted and still resolve when their branch loads.
        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, LazyRoot(withEagerSibling: true))
            .Add(c => c.LoadChildren, Loader())
            .Add(c => c.MultiSelect, true)
            .Add(c => c.SelectedValues, new List<string> { "a", "b" }));

        await cut.InvokeAsync(() => Row(cut, "Other").Click());

        await cut.InvokeAsync(() => cut.Find("button[aria-label='Expand']").Click());

        // The user's pick AND both seed values are selected: {Other, Child-A, Child-B}.
        Assert.Equal("true", TreeItem(cut, "Other").GetAttribute("aria-selected"));
        Assert.Equal("true", TreeItem(cut, "Child-A").GetAttribute("aria-selected"));
        Assert.Equal("true", TreeItem(cut, "Child-B").GetAttribute("aria-selected"));
        Assert.Equal(3, cut.FindAll("[role='treeitem'][aria-selected='true']").Count);
    }

    // ---- (d) same-reference RESET to unloaded repopulates pending ----

    [Fact]
    public async Task Lazy_branch_reset_to_unloaded_with_same_reference_repopulates_pending()
    {
        // ONE SelectedValues instance reused across every render, so OnParametersSet's
        // reference check skips ResolveSelectedNodes on the reset — the lifecycle must not
        // depend on that check to keep the seed alive.
        //
        // REWRITTEN (Codex round-15, seed-precedence fix): the prior form triggered the unload with a
        // FRESH unloaded lazy rebuild and asserted the branch collapsed. That collapse was the
        // finding-2 ARTIFACT — a domain-pure rebuild's pristine IsExpanded=false was mis-read as a
        // consumer collapse. Under the corrected seed model a domain-pure rebuild PRESERVES tree-owned
        // expansion + lazy children (the normal lazy flow; see TreeViewSeedPrecedenceTests), so that
        // vehicle no longer unloads. The genuine unload channel is now the finding-1 RESET: the consumer
        // presents the SAME node identity as unloaded (Children cleared, ChildrenLoaded=false). This
        // still pins the original intent — pending repopulation is not gated on the SelectedValues
        // reference.
        var seed = new List<string> { "a" };

        var lazy = new L.TreeView<string>.TreeViewItem<string> { Text = "Lazy", Value = "lazy", IsLeaf = false };
        var items = new List<L.TreeView<string>.TreeViewItem<string>> { lazy };

        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, items)
            .Add(c => c.LoadChildren, Loader())
            .Add(c => c.SelectedValues, seed));

        // Expand → Child-A materializes and the seed resolves onto it.
        await cut.InvokeAsync(() => cut.Find("button[aria-label='Expand']").Click());
        Assert.Equal("true", TreeItem(cut, "Child-A").GetAttribute("aria-selected"));

        // RESET the SAME identity back to unloaded (finding 1) with the SAME SelectedValues reference:
        // the branch collapses + unloads, Child-A vanishes, its value carries back to pending.
        lazy.Children = null;
        lazy.ChildrenLoaded = false;
        cut.Render(p => p
            .Add(c => c.Items, items)
            .Add(c => c.LoadChildren, Loader())
            .Add(c => c.SelectedValues, seed));
        Assert.Empty(cut.FindAll("[role='treeitem'][aria-selected='true']"));

        // Re-expand → Child-A reloads and the carried-pending seed re-binds to it.
        await cut.InvokeAsync(() => cut.Find("button[aria-label='Expand']").Click());
        Assert.Equal("true", TreeItem(cut, "Child-A").GetAttribute("aria-selected"));
    }
}
