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
/// PR #351 STRUCTURAL REBUILD — UI state (Expanded / Loading / ChildrenLoaded / materialized lazy
/// Children / search-collapse override) moved OUT of the consumer's TreeViewItem record and INTO the
/// tree, keyed by a tree-owned identity resolved ONCE per parameter-set. An async operation captures
/// the state ENTRY (a stable handle) — no rebuild can invalidate it — so the four round-14 findings
/// that the old in-flight-registry / orphan-sweep / supersession machinery kept re-opening now
/// dissolve as plain state writes:
///
///   1. A moved, uniquely-valued node keeps loading (its entry follows it) and a collapse AFTER the
///      move is honored — no old/new path bridging (the supersession-by-path hole, TreeView.razor:1381).
///   2. Collapsing a spinning row and clicking it again starts NO second load — the re-click reads
///      Loading=true off the entry (the duplicate-load race, TreeViewNode.razor:267).
///   3. A collapse-during-load completion still runs its materialization side effects — binds a
///      seeded SelectedValues and recomputes checkbox tri-state — even though the branch stays
///      collapsed (the silent-superseded-completion gap, TreeViewNode.razor:373).
///   4. A manual collapse of a search-force-expanded ancestor survives a same-reference re-render —
///      the override lives in the entry, not in the recomputed _autoExpanded set (TreeView.razor:493).
///
/// EVENT CONTRACT (decided + documented): TreeView exposes NO expand event. Expansion is observable
/// only through the mirrored item.IsExpanded, and a lazy completion ALWAYS materializes (binds pending
/// SelectedValues, recomputes tri-state) regardless of the branch ending expanded or collapsed — so a
/// superseded completion attaching "silently" is correct: there is no event to mis-fire, and the
/// side effects are not gated on expansion.
///
/// Plus a rebuild-torture test: a controlled/immutable consumer rebuilds Items at EVERY await point of
/// a lazy row click (OnItemClick, SelectedValuesChanged, and while the loader hangs); a uniquely-valued
/// node's identity follows every rebuild and the children land on the FINAL instance.
/// </summary>
public class TreeViewOwnershipRebuildTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TreeViewOwnershipRebuildTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static IElement TreeItem(IRenderedComponent<L.TreeView<string>> cut, string text)
        => cut.FindAll("[role='treeitem']").First(el => el.Children[0].TextContent.Contains(text));

    private static IElement Row(IRenderedComponent<L.TreeView<string>> cut, string text)
        => TreeItem(cut, text).Children[0];

    // ── Finding 1: moved in-flight node keeps loading; a collapse after the move is honored ──────

    [Fact]
    public async Task Moved_node_keeps_loading_and_a_collapse_after_the_move_is_honored()
    {
        var gate = new TaskCompletionSource<List<Item>>();
        Func<Item, Task<List<Item>>> loader = _ => gate.Task;

        // A uniquely-valued lazy "Folder" after a sibling.
        var initial = new List<Item>
        {
            new() { Text = "Other", Value = "other" },
            new() { Text = "Folder", Value = "folder", IsLeaf = false }
        };
        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, initial)
            .Add(c => c.LoadChildren, loader));

        await cut.InvokeAsync(() => Row(cut, "Folder").Click()); // expand → load hangs
        Assert.True(initial[1].IsExpanded);
        Assert.Contains("animate-spin", cut.Markup);

        // MOVE Folder to index 0 via a fresh-instance rebuild, carrying its (mirrored) IsExpanded
        // forward the way a controlled consumer does. Its value "folder" is UNIQUE, so the state entry
        // follows it — the in-flight load's spinner comes along, no path bridging.
        var moved = new List<Item>
        {
            new() { Text = "Folder", Value = "folder", IsLeaf = false, IsExpanded = true },
            new() { Text = "Other", Value = "other" }
        };
        cut.Render(p => p.Add(c => c.Items, moved));
        Assert.True(moved[0].IsLoading, "the load followed the moved node");
        Assert.Contains("animate-spin", cut.Markup);

        // Collapse the MOVED node while the load is still pending — a plain write to its entry.
        await cut.InvokeAsync(() => Row(cut, "Folder").Click());
        Assert.False(moved[0].IsExpanded, "the collapse-after-move flips the entry closed");

        // Release: children attach but the node MUST stay collapsed (the collapse is the latest intent),
        // even though the entry's path changed when the node moved.
        await cut.InvokeAsync(() => gate.SetResult(new List<Item> { new() { Text = "Doc", Value = "doc" } }));

        Assert.True(moved[0].ChildrenLoaded, "the children still attach to the moved entry");
        Assert.False(moved[0].IsExpanded, "the collapse-after-move survives the completion (no re-expand)");
        Assert.DoesNotContain("Doc", cut.Markup);
    }

    // ── Finding 2: collapse a spinning row, re-click it → no duplicate LoadChildren ──────────────

    [Fact]
    public async Task Collapse_then_reclick_a_spinning_row_starts_no_second_load()
    {
        var gate = new TaskCompletionSource<List<Item>>();
        var calls = 0;
        Func<Item, Task<List<Item>>> loader = _ => { calls++; return gate.Task; };

        var items = new List<Item> { new() { Text = "Folder", Value = "folder", IsLeaf = false } };
        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, items)
            .Add(c => c.LoadChildren, loader));

        await cut.InvokeAsync(() => Row(cut, "Folder").Click()); // expand → first load starts
        Assert.Equal(1, calls);
        Assert.True(items[0].IsLoading);

        await cut.InvokeAsync(() => Row(cut, "Folder").Click()); // collapse WHILE loading
        Assert.False(items[0].IsExpanded);
        Assert.True(items[0].IsLoading, "the first load is still in flight");

        await cut.InvokeAsync(() => Row(cut, "Folder").Click()); // re-click the still-spinning row
        Assert.Equal(1, calls); // the entry's Loading=true suppresses a duplicate LoadChildren
        Assert.True(items[0].IsExpanded, "re-expanded, awaiting the ORIGINAL load");

        await cut.InvokeAsync(() => gate.SetResult(new List<Item> { new() { Text = "Doc", Value = "doc" } }));
        Assert.Equal(1, calls);
        Assert.True(items[0].ChildrenLoaded);
        Assert.True(items[0].IsExpanded);
        Assert.Contains("Doc", cut.Markup);
    }

    // ── Finding 3: a superseded (collapsed) completion still binds seed selection + tri-state ────

    [Fact]
    public async Task Collapse_during_lazy_load_still_binds_seeded_selection_and_checkbox_ancestors()
    {
        var gate = new TaskCompletionSource<List<Item>>();
        Func<Item, Task<List<Item>>> loader = _ => gate.Task;

        var items = new List<Item> { new() { Text = "Group", Value = "group", IsLeaf = false } };
        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, items)
            .Add(c => c.LoadChildren, loader)
            .Add(c => c.ShowCheckboxes, true)
            .Add(c => c.SelectedValues, new List<string> { "a" })); // seed selects the lazy child "a"

        // Expand + collapse via the KEYBOARD (ArrowRight/ArrowLeft) so the collapse doesn't also
        // SELECT the parent — a single-select row click would take selection ownership and void the
        // pending seed, hiding the very materialization this test pins.
        await cut.InvokeAsync(() => TreeItem(cut, "Group").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" })); // expand → load hangs
        await cut.InvokeAsync(() => TreeItem(cut, "Group").KeyDown(new KeyboardEventArgs { Key = "ArrowLeft" }));  // collapse WHILE loading
        Assert.False(items[0].IsExpanded);

        // Release: the child arrives already checked. Even though the branch stays COLLAPSED, the
        // completion must bind the seeded selection AND recompute the parent's tri-state.
        await cut.InvokeAsync(() => gate.SetResult(new List<Item>
        {
            new() { Text = "Child-A", Value = "a", IsChecked = true },
            new() { Text = "Child-B", Value = "b" }
        }));

        Assert.True(items[0].ChildrenLoaded);
        Assert.False(items[0].IsExpanded, "stays collapsed — the completion does not re-open it");
        Assert.True(items[0].Children![0].IsChecked, "the seeded child check survives");
        Assert.True(items[0].IsIndeterminate, "parent tri-state recomputed from the loaded checked child (collapsed)");

        // The seeded SelectedValues "a" bound to the loaded child during the collapsed completion:
        // re-expanding (already loaded, via keyboard so selection is untouched) reveals it already
        // selected, with no extra interaction needed.
        await cut.InvokeAsync(() => TreeItem(cut, "Group").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" }));
        Assert.Equal("true", TreeItem(cut, "Child-A").GetAttribute("aria-selected"));
    }

    // ── Finding 4: a manual collapse of a search-forced ancestor survives a same-reference render ─

    [Fact]
    public async Task Manual_collapse_of_a_search_forced_ancestor_survives_a_same_reference_rerender()
    {
        var items = new List<Item>
        {
            new() { Text = "Docs", Value = "docs", Children = [ new() { Text = "report", Value = "report" } ] }
        };
        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, items)
            .Add(c => c.ShowSearch, true));

        cut.Find("input").Input("report"); // Docs is search-auto-expanded → its child becomes visible
        Assert.Equal(2, cut.FindAll("[role='treeitem']").Count);

        // Manually collapse the search-forced ancestor via its chevron → records the entry override.
        await cut.InvokeAsync(() => cut.Find("button[aria-label='Collapse']").Click());
        Assert.Single(cut.FindAll("[role='treeitem']"));

        // A same-reference, unrelated re-render re-materializes the filtered display (RebuildDisplay),
        // but the entry's SearchCollapse override keeps Docs closed instead of re-forcing it open.
        cut.Render(p => p.Add(c => c.Class, "noop"));

        Assert.Single(cut.FindAll("[role='treeitem']"));
        Assert.Equal("false", TreeItem(cut, "Docs").GetAttribute("aria-expanded"));
    }

    // ── Rebuild torture: a controlled rebuild at EVERY await point of a lazy row click ───────────

    [Fact]
    public async Task Lazy_row_click_survives_a_controlled_rebuild_at_every_await_point()
    {
        IRenderedComponent<L.TreeView<string>>? cut = null;
        var gate = new TaskCompletionSource<List<Item>>();
        Func<Item, Task<List<Item>>> loader = _ => gate.Task;

        var ver = 0;
        List<Item> current = null!;
        // A uniquely-valued lazy node whose LABEL is bumped every rebuild (distinct @key → the
        // component is disposed + recreated), carrying the mirrored flags forward. Its value "music"
        // is UNIQUE, so the tree-owned entry follows every rebuild.
        List<Item> Build() =>
        [
            new()
            {
                Text = $"Music v{ver}", Value = "music", IsLeaf = false,
                IsExpanded = current is { Count: > 0 } && current[0].IsExpanded,
                IsLoading = current is { Count: > 0 } && current[0].IsLoading
            }
        ];

        var onClick = EventCallback.Factory.Create<Item>(_ctx, (Item _) =>
        {
            ver++; current = Build(); cut!.Render(p => p.Add(c => c.Items, current));
        });
        var onSel = EventCallback.Factory.Create<List<string>>(_ctx, (List<string> _) =>
        {
            ver++; current = Build(); cut!.Render(p => p.Add(c => c.Items, current));
        });

        current = Build();
        cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, current)
            .Add(c => c.LoadChildren, loader)
            .Add(c => c.OnItemClick, onClick)
            .Add(c => c.SelectedValues, (List<string>?)null)
            .Add(c => c.SelectedValuesChanged, onSel));

        // Click: OnItemClick rebuilds (await point 1), SelectedValuesChanged rebuilds (await point 2),
        // then the load starts and hangs (await point 3).
        await cut.InvokeAsync(() => Row(cut, "Music").Click());
        Assert.True(current[0].IsLoading, "the entry's spinner tracked the node across two rebuilds");
        Assert.Contains("animate-spin", cut.Markup);

        // A THIRD rebuild while the loader is still pending (await point 3).
        ver++; current = Build(); cut.Render(p => p.Add(c => c.Items, current));
        Assert.True(current[0].IsLoading);

        // Release: the children attach to the FINAL fresh instance and it renders expanded.
        await cut.InvokeAsync(() =>
            gate.SetResult(new List<Item> { new() { Text = "Child-A", Value = "a", IsLeaf = true } }));

        Assert.True(current[0].ChildrenLoaded, "children land on the final instance the identity maps to");
        Assert.False(current[0].IsLoading, "no stranded spinner");
        Assert.Contains("Child-A", cut.Markup);
        Assert.Equal("true", TreeItem(cut, "Music").GetAttribute("aria-expanded"));
    }
}
