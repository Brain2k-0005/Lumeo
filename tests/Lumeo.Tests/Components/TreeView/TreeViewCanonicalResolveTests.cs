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
/// PR #351 round-11 consolidation: the ORDERING MATRIX for the single canonical post-await
/// re-resolver (ResolveNode). One resolver, one deferral contract, one change-detection rule — these
/// tests pin the four round-11 findings plus the adjacent edges the model has to keep sound:
///   a) selection stays STRICT — a duplicate-valued sibling reorder during OnItemClick DROPS rather
///      than select the wrong sibling now sitting at the old index (round-11 a);
///   b) a lazy expansion whose siblings REORDER while the load is in flight DROPS the children rather
///      than graft them onto a duplicate-valued sibling (round-11 b);
///   c) a search-auto-expanded ancestor collapses on row click even after the selection callbacks
///      rebuilt Items and RebuildDisplay re-registered the fresh node in _autoExpanded (round-11 c);
///   d) an IN-PLACE mutation of the same Items list reference still reanchors the selection onto the
///      fresh instance (round-11 d);
///   e) the positive counterpoint — a UNIQUE-valued lazy parent still resolves and attaches a cached
///      (completed) load through the same deferral, so the drop rule doesn't over-reach.
/// Each is a genuine regression: it fails on the pre-consolidation code and passes after.
/// </summary>
public class TreeViewCanonicalResolveTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TreeViewCanonicalResolveTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static IElement TreeItem(IRenderedComponent<L.TreeView<string>> cut, string text)
        => cut.FindAll("[role='treeitem']").First(el => el.Children[0].TextContent.Contains(text));

    private static IElement Row(IRenderedComponent<L.TreeView<string>> cut, string text)
        => TreeItem(cut, text).Children[0];

    // ---- (a) round-11 a: selection is STRICT — reorder during OnItemClick drops ----

    [Fact]
    public async Task Selection_drops_when_OnItemClick_reorders_duplicate_valued_siblings()
    {
        IRenderedComponent<L.TreeView<string>>? cut = null;

        // Two duplicate-valued sibling leaves under an expanded root; the SECOND sits at path [0,1].
        List<Item> Ordered() =>
        [
            new()
            {
                Text = "Root", Value = "root", IsExpanded = true,
                Children = [ new() { Text = "First", Value = "dup" }, new() { Text = "Second", Value = "dup" } ]
            }
        ];
        // OnItemClick swaps the two same-valued siblings, so path [0,1] now holds "First" (also "dup").
        List<Item> Reordered() =>
        [
            new()
            {
                Text = "Root", Value = "root", IsExpanded = true,
                Children = [ new() { Text = "Second", Value = "dup" }, new() { Text = "First", Value = "dup" } ]
            }
        ];

        List<Item> current = Ordered();
        var onClick = EventCallback.Factory.Create<Item>(_ctx, async (Item _) =>
        {
            current = Reordered(); // reorder the duplicate-valued siblings mid-click
            cut!.Render(p => p.Add(c => c.Items, current));
            await Task.Yield();
        });

        current = Ordered();
        cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, current)
            .Add(c => c.OnItemClick, onClick));

        await Row(cut, "Second").ClickAsync(new MouseEventArgs());

        // The clicked "Second" is gone and its old index now carries "First" (same "dup" value). The
        // strict resolver can't prove identity, so it DROPS — the relaxed positional fallback would have
        // selected the wrong "First" row. Nothing is selected.
        Assert.Empty(cut.FindAll("[role='treeitem'][aria-selected='true']"));
    }

    // ---- (b) round-11 b: lazy expansion drops on a reorder during the load ----

    [Fact]
    public async Task Reorder_during_lazy_load_drops_children_and_never_misattaches()
    {
        IRenderedComponent<L.TreeView<string>>? cut = null;

        var gate = new TaskCompletionSource<List<Item>>();
        Func<Item, Task<List<Item>>> loader = _ => gate.Task; // hangs until released after the reorder

        List<Item> current = null!;
        // The controlled rebuild in SelectedValuesChanged SWAPS the two duplicate-valued lazy parents —
        // a genuine reorder — while the loader is still pending.
        List<Item> Initial() =>
        [
            new() { Text = "Alpha", Value = "dup", IsLeaf = false, IsExpanded = false },
            new() { Text = "Beta",  Value = "dup", IsLeaf = false, IsExpanded = false }
        ];
        List<Item> Swapped() =>
        [
            new() { Text = "Beta",  Value = "dup", IsLeaf = false, IsExpanded = false },
            new() { Text = "Alpha", Value = "dup", IsLeaf = false, IsExpanded = true }
        ];

        var callback = EventCallback.Factory.Create<List<string>>(_ctx, (List<string> _) =>
        {
            current = Swapped();
            cut!.Render(p => p.Add(c => c.Items, current));
        });

        current = Initial();
        cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, current)
            .Add(c => c.LoadChildren, loader)
            .Add(c => c.SelectedValues, (List<string>?)null)
            .Add(c => c.SelectedValuesChanged, callback));

        // Click Alpha (index 0) → starts the hanging load; the rebuild then swaps Alpha to index 1.
        await cut.InvokeAsync(() => Row(cut, "Alpha").Click());

        // Release the load AFTER the reorder. Alpha's clicked instance is gone and index 0 now holds
        // Beta (also "dup"): identity is unprovable, so the fetched children DROP rather than graft onto
        // Beta — duplicate container values don't guarantee equivalent children.
        await cut.InvokeAsync(() =>
            gate.SetResult(new List<Item> { new() { Text = "loaded-child", Value = "leaf", IsLeaf = true } }));

        Assert.DoesNotContain("loaded-child", cut.Markup);
    }

    // ---- (c) round-11 c: search auto-expanded ancestor collapses via row click after a rebuild ----

    [Fact]
    public async Task Search_auto_expanded_ancestor_collapses_on_row_click_after_a_controlled_rebuild()
    {
        IRenderedComponent<L.TreeView<string>>? cut = null;

        List<Item> current = null!;
        // "Docs" only matches the filter through its child "report", so a filter forces it auto-expanded.
        List<Item> Build() =>
        [
            new() { Text = "Docs", Value = "docs", Children = [ new() { Text = "report", Value = "report" } ] }
        ];

        var callback = EventCallback.Factory.Create<List<string>>(_ctx, (List<string> _) =>
        {
            current = Build(); // fresh instances on selection → the clicked Docs is disposed
            cut!.Render(p => p.Add(c => c.Items, current));
        });

        current = Build();
        cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, current)
            .Add(c => c.ShowSearch, true)
            .Add(c => c.SelectedValues, (List<string>?)null)
            .Add(c => c.SelectedValuesChanged, callback));

        // Filter to "report" → Docs is search-auto-expanded and its child becomes visible (2 treeitems).
        cut.Find("input").Input("report");
        Assert.Equal(2, cut.FindAll("[role='treeitem']").Count);

        // Row-click Docs to collapse. The selection callback rebuilds Items (fresh instances) and
        // RebuildDisplay re-auto-expands the FRESH Docs; the DEFERRED collapse must re-resolve through
        // the canonical resolver and clear auto-expansion on THAT node, not the disposed reference.
        await cut.InvokeAsync(() => Row(cut, "Docs").Click());

        Assert.Equal("false", TreeItem(cut, "Docs").GetAttribute("aria-expanded"));
        // The child row is hidden even though it still matches the filter — only Docs remains.
        Assert.Single(cut.FindAll("[role='treeitem']"));
    }

    // ---- (d) round-11 d: in-place mutation of the same list reference still reanchors selection ----

    [Fact]
    public async Task In_place_items_mutation_reanchors_selection_to_the_fresh_instance()
    {
        var items = new List<Item>
        {
            new() { Text = "Documents", Value = "docs" },
            new() { Text = "Images", Value = "imgs" }
        };

        var cut = _ctx.Render<L.TreeView<string>>(p => p.Add(c => c.Items, items));

        await cut.InvokeAsync(() => Row(cut, "Images").Click());
        Assert.Equal("true", TreeItem(cut, "Images").GetAttribute("aria-selected"));

        // Mutate the SAME list in place: replace Images with a fresh same-valued instance, then re-render
        // with the UNCHANGED list reference. The reanchor must run despite the identical reference so the
        // selection follows to the new instance (reference-gated code left it on the disposed one).
        items[1] = new() { Text = "Images", Value = "imgs" };
        cut.Render(p => p.Add(c => c.Items, items));

        var selected = cut.FindAll("[role='treeitem'][aria-selected='true']");
        Assert.Single(selected);
        Assert.Contains("Images", selected[0].Children[0].TextContent);
        Assert.Equal("false", TreeItem(cut, "Documents").GetAttribute("aria-selected"));
    }

    // ---- (e) positive counterpoint: a UNIQUE-valued cached load still attaches through the deferral ----

    [Fact]
    public async Task Cached_load_on_a_unique_valued_parent_attaches_to_the_fresh_node_after_a_rebuild()
    {
        IRenderedComponent<L.TreeView<string>>? cut = null;

        // A cached (already-completed) loader — the ordering edge round-10 hardened — still resolves and
        // attaches when the parent's value is UNIQUE, so the round-11 duplicate-DROP doesn't over-reach.
        Func<Item, Task<List<Item>>> loader = _ =>
            Task.FromResult(new List<Item> { new() { Text = "Child-A", Value = "a", IsLeaf = true } });

        List<Item> current = null!;
        List<Item> Build(bool expanded) =>
        [
            new() { Text = "Music", Value = "music", IsLeaf = false, IsExpanded = expanded }
        ];

        var callback = EventCallback.Factory.Create<List<string>>(_ctx, (List<string> _) =>
        {
            current = Build(current[0].IsExpanded);
            cut!.Render(p => p.Add(c => c.Items, current));
        });

        current = Build(false);
        cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, current)
            .Add(c => c.LoadChildren, loader)
            .Add(c => c.SelectedValues, (List<string>?)null)
            .Add(c => c.SelectedValuesChanged, callback));

        await cut.InvokeAsync(() => Row(cut, "Music").Click());

        Assert.Contains("Child-A", cut.Markup);
        Assert.Equal("true", TreeItem(cut, "Music").GetAttribute("aria-expanded"));
        Assert.Equal("true", TreeItem(cut, "Music").GetAttribute("aria-selected"));
    }
}
