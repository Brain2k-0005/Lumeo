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
/// PR #351 round-10 (Codex): four ordering/reanchor edge cases when a controlled/immutable consumer
/// rebuilds Items around a lazy row click. Each test is a genuine regression — it FAILS on the code
/// before its fix and passes after.
/// </summary>
public class TreeViewControlledRound10Tests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TreeViewControlledRound10Tests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static IElement TreeItem(IRenderedComponent<L.TreeView<string>> cut, string text)
        => cut.FindAll("[role='treeitem']").First(el => el.Children[0].TextContent.Contains(text));

    private static IElement Row(IRenderedComponent<L.TreeView<string>> cut, string text)
        => TreeItem(cut, text).Children[0];

    /// <summary>
    /// Finding 1: a LoadChildren that hands back an ALREADY-COMPLETED task (cached children via
    /// Task.FromResult) must not attach synchronously ahead of the row-click selection callbacks. If it
    /// did, the children would land on the clicked instance BEFORE the selection-driven controlled
    /// rebuild replaced it with a fresh (still-lazy, childless) node — stranding the visible node
    /// expanded-but-empty. The load must START after selection so ResolveExpansionTarget attaches to the
    /// rebuilt node.
    /// </summary>
    [Fact]
    public async Task Cached_lazy_row_click_attaches_children_to_the_fresh_node_after_a_controlled_rebuild()
    {
        IRenderedComponent<L.TreeView<string>>? cut = null;

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

        // The cached children landed on the CURRENT (rebuilt) node, not the discarded stale one.
        Assert.Contains("Child-A", cut.Markup);
        Assert.Equal("true", TreeItem(cut, "Music").GetAttribute("aria-expanded"));
        Assert.Equal("true", TreeItem(cut, "Music").GetAttribute("aria-selected"));
    }

    /// <summary>
    /// Finding 2: when a controlled rebuild copies the optimistic IsLoading=true onto the fresh node and
    /// the lazy load then FAILS, the rollback must clear IsLoading on the reanchored node too — the node
    /// component's finally only clears the stale instance, so otherwise the fresh row stays stuck showing
    /// the spinner.
    /// </summary>
    [Fact]
    public async Task Failed_lazy_row_click_clears_the_spinner_on_the_reanchored_node()
    {
        IRenderedComponent<L.TreeView<string>>? cut = null;

        var gate = new TaskCompletionSource<List<Item>>();
        Func<Item, Task<List<Item>>> loader = _ => gate.Task;

        // Controlled/immutable rebuild carries BOTH optimistic flags (IsExpanded AND IsLoading) forward
        // onto the fresh instance AND bumps the label — a realistic reload where the display text changes.
        // The changed Text makes the fresh node value-DISTINCT from the mutated stale one, so its @key
        // differs and Blazor DISPOSES + recreates the component around the fresh instance (rather than
        // reusing it, which would let the node's own finally clear the spinner). Value stays "music" so
        // the reanchor still resolves. This is exactly the "fresh @key'd node copied IsLoading=true" case.
        List<Item> current = null!;
        var ver = 0;
        List<Item> Build(bool expanded, bool loading) =>
        [
            new() { Text = $"Music v{ver}", Value = "music", IsLeaf = false, IsExpanded = expanded, IsLoading = loading }
        ];

        var callback = EventCallback.Factory.Create<List<string>>(_ctx, (List<string> _) =>
        {
            ver++;
            current = Build(current[0].IsExpanded, current[0].IsLoading);
            cut!.Render(p => p.Add(c => c.Items, current));
        });

        current = Build(false, false);
        cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, current)
            .Add(c => c.LoadChildren, loader)
            .Add(c => c.SelectedValues, (List<string>?)null)
            .Add(c => c.SelectedValuesChanged, callback));

        // Click → the rebuilt fresh node carries the copied spinner while the load hangs.
        await cut.InvokeAsync(() => Row(cut, "Music").Click());
        Assert.True(current[0].IsLoading, "the fresh node should carry the copied optimistic spinner");
        Assert.Contains("animate-spin", cut.Markup);
        Assert.Equal("true", TreeItem(cut, "Music").GetAttribute("aria-expanded"));

        // Load FAILS → the reanchored rollback must collapse AND clear the spinner on the fresh node
        // (the node component's finally only clears the STALE instance). Assert at the model level so
        // the check is independent of render timing.
        await cut.InvokeAsync(() => gate.SetException(new InvalidOperationException("loader boom")));
        Assert.False(current[0].IsLoading, "the reanchored rollback must clear IsLoading on the fresh node");
        Assert.False(current[0].IsExpanded, "the reanchored rollback must collapse the fresh node");
        Assert.DoesNotContain("animate-spin", cut.Markup);
        Assert.Equal("false", TreeItem(cut, "Music").GetAttribute("aria-expanded"));
    }

    /// <summary>
    /// Finding 3: two sibling PARENTS that share a Value (the null/duplicate container case #350 targets)
    /// defeat the sibling-uniqueness gate. A strict reanchor would DROP the target and the loaded children
    /// would never attach. Expansion diverges from selection's ambiguity-DROP: a same-content controlled
    /// rebuild preserves structure, so the children attach to the clicked parent BY POSITION.
    /// </summary>
    [Fact]
    public async Task Duplicate_valued_sibling_parents_attach_lazy_children_by_position_after_a_controlled_rebuild()
    {
        IRenderedComponent<L.TreeView<string>>? cut = null;

        // Tag children by the clicked node's Text so we can prove the branch landed on the RIGHT parent.
        Func<Item, Task<List<Item>>> loader = node =>
            Task.FromResult(new List<Item> { new() { Text = $"child-of-{node.Text}", Value = "leaf", IsLeaf = true } });

        List<Item> current = null!;
        List<Item> Build(bool aExp, bool bExp) =>
        [
            new() { Text = "Alpha", Value = "dup", IsLeaf = false, IsExpanded = aExp },
            new() { Text = "Beta",  Value = "dup", IsLeaf = false, IsExpanded = bExp }
        ];

        var callback = EventCallback.Factory.Create<List<string>>(_ctx, (List<string> _) =>
        {
            current = Build(current[0].IsExpanded, current[1].IsExpanded);
            cut!.Render(p => p.Add(c => c.Items, current));
        });

        current = Build(false, false);
        cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, current)
            .Add(c => c.LoadChildren, loader)
            .Add(c => c.SelectedValues, (List<string>?)null)
            .Add(c => c.SelectedValuesChanged, callback));

        // Click the SECOND same-valued parent — its children must attach to Beta (position 1), not drop.
        await cut.InvokeAsync(() => Row(cut, "Beta").Click());

        Assert.Contains("child-of-Beta", cut.Markup);
        Assert.DoesNotContain("child-of-Alpha", cut.Markup);
        Assert.Equal("true", TreeItem(cut, "Beta").GetAttribute("aria-expanded"));
    }

    /// <summary>
    /// Finding 4: a consumer's OnItemClick handler that rebuilds Items (immutable reload) runs BEFORE the
    /// selection callback. Selection must re-resolve the clicked node against the rebuilt tree, or it
    /// stores the now-stale instance and the fresh rendered row shows unselected.
    /// </summary>
    [Fact]
    public async Task Row_click_selects_the_fresh_node_when_OnItemClick_rebuilds_Items()
    {
        IRenderedComponent<L.TreeView<string>>? cut = null;

        // The reload bumps the labels so the fresh nodes are value-DISTINCT from the originals: their
        // @keys differ, so Blazor DISPOSES the clicked node's component (rather than reusing it, which
        // would keep the passed Item pointing at the fresh instance). Values stay stable so selection is
        // still resolvable by value/path.
        var ver = 0;
        List<Item> Build() =>
        [
            new() { Text = $"Documents v{ver}", Value = "docs" },
            new() { Text = $"Images v{ver}", Value = "imgs" }
        ];
        List<Item> current = Build();

        // Immutable reload INSIDE OnItemClick, then YIELD — so the reload's reanchor pass fully settles
        // (with the selection set still empty, so nothing carries) BEFORE OnSelectionChanged runs. This
        // defeats the reanchor-after-add safety net and isolates finding 4: selection must itself
        // re-resolve the clicked node against the reloaded tree.
        var onClick = EventCallback.Factory.Create<Item>(_ctx, async (Item _) =>
        {
            ver++;
            current = Build(); // fresh (value-distinct) instances replace the clicked node
            cut!.Render(p => p.Add(c => c.Items, current));
            await Task.Yield();
        });

        current = Build();
        cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, current)
            .Add(c => c.OnItemClick, onClick));

        // ClickAsync (not the fire-and-forget Click) so we AWAIT the full async handler — the reload +
        // yield + selection — before asserting, instead of observing a mid-flight state.
        await Row(cut, "Images").ClickAsync(new MouseEventArgs());

        // The clicked value's FRESH node is the selected row — selection re-resolved past the reload.
        Assert.Equal("true", TreeItem(cut, "Images").GetAttribute("aria-selected"));
        Assert.Equal("false", TreeItem(cut, "Documents").GetAttribute("aria-selected"));
    }
}
