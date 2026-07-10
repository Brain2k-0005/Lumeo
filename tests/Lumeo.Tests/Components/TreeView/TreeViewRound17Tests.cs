using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Bunit;
using AngleSharp.Dom;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.TreeView;

using Item = L.TreeView<string>.TreeViewItem<string>;

/// <summary>
/// PR #351 Codex round-17 — four more tree-owned state gaps (see the CHILDREN/CHILDRENLOADED
/// precedence + OPERATION SEMANTICS blocks in TreeView.razor). Each test is the failing-then-fixed
/// repro of one finding:
///
///   #1  EXPAND-ALL VS ALREADY-LOADING — a branch already loading from a prior row/chevron expand must
///       NOT be skipped by ExpandAllNodesAsync; it awaits that in-flight load (via the entry's
///       LoadSettled signal) and then descends once children materialize.
///   #2  SUPERSEDED EXPAND-ALL FAILURE — a superseded/dropped expand-all load that later FAILS is
///       swallowed as silently as a superseded SUCCESS (same contract as round-16 f2); it must not
///       fault an awaited ExpandAllNodesAsync.
///   #3  NON-NULL EMPTY CHILDREN ARE AUTHORITATIVE — a refresh that replaces a loaded branch with a
///       NON-null empty list (ChildrenLoaded left false) is honored as authoritative-empty; the stale
///       subtree is not reattached. A plain null-children leaf is NOT disturbed (leaf-vs-lazy guard).
///   #4  DROPPED ROW-CLICK DEFERRAL — when the row-click selection callback removes the clicked node
///       before the DEFERRED lazy tail runs, the tail skips LoadChildren entirely (no wasted fetch).
/// </summary>
public class TreeViewRound17Tests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TreeViewRound17Tests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static IElement TreeItem(IRenderedComponent<L.TreeView<string>> cut, string text)
        => cut.FindAll("[role='treeitem']").First(el => el.Children[0].TextContent.Contains(text));

    private static IElement Row(IRenderedComponent<L.TreeView<string>> cut, string text)
        => TreeItem(cut, text).Children[0];

    // ── #1: expand-all AWAITS a branch already loading from a prior expand, then descends ──────────

    [Fact]
    public async Task Expand_all_awaits_a_branch_already_loading_and_then_descends_into_it()
    {
        var gate = new TaskCompletionSource<List<Item>>();
        Func<Item, Task<List<Item>>> loader = _ => gate.Task;

        var root = new Item { Text = "Root", Value = "root", IsLeaf = false };
        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, new List<Item> { root })
            .Add(c => c.LoadChildren, loader));

        // A per-node CHEVRON expand starts Root's lazy load; it hangs on the gate.
        await cut.InvokeAsync(() => cut.Find("button[aria-label='Expand']").Click());
        Assert.True(root.IsLoading);

        // Expand-all now meets a Root that is ALREADY loading. It must NOT skip the subtree — it awaits
        // the in-flight load rather than returning early (round-17 #1). Not awaited yet: it parks on the
        // entry's LoadSettled signal.
        var expandAll = cut.InvokeAsync(() => cut.Instance.ExpandAllNodesAsync());

        // Resolve the single in-flight load with a nested, initially-COLLAPSED Branch (its own Leaf
        // already loaded). The per-node load only materializes Root's children; it does not expand
        // Branch — that is expand-all's job once it resumes and descends.
        await cut.InvokeAsync(() => gate.SetResult(new List<Item>
        {
            new()
            {
                Text = "Branch", Value = "branch", IsLeaf = false, ChildrenLoaded = true,
                Children = new List<Item> { new() { Text = "Leaf", Value = "leaf", IsLeaf = true } }
            }
        }));
        await expandAll;

        Assert.Contains("Branch", cut.Markup);
        Assert.True(root.Children![0].IsExpanded, "expand-all descended into the branch a prior expand loaded");
        Assert.Contains("Leaf", cut.Markup);
    }

    // ── #2: a superseded expand-all load that FAILS is swallowed (no fault surfaces) ───────────────

    [Fact]
    public async Task Superseded_expand_all_load_failure_is_swallowed()
    {
        var gate = new TaskCompletionSource<List<Item>>();
        Func<Item, Task<List<Item>>> loader = _ => gate.Task;

        var root = new Item { Text = "Root", Value = "root", IsLeaf = false };
        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, new List<Item> { root })
            .Add(c => c.LoadChildren, loader));

        // Expand-all STARTS Root's load and blocks on it.
        var expandAll = cut.InvokeAsync(() => cut.Instance.ExpandAllNodesAsync());
        Assert.True(root.IsLoading);

        // The consumer supersedes the in-flight expand-all load with AUTHORITATIVE children on the SAME
        // live Root (bumps the LoadToken).
        root.Children = new List<Item> { new() { Text = "FreshBranch", Value = "fresh", IsLeaf = true } };
        root.ChildrenLoaded = true;
        cut.Render(p => p.Add(c => c.Items, new List<Item> { root }));
        Assert.Contains("FreshBranch", cut.Markup);

        // The stale expand-all loader now FAILS. A dead loader's REJECTION must be discarded as SILENTLY
        // as a superseded SUCCESS — awaiting ExpandAllNodesAsync must NOT throw and the consumer's branch
        // must NOT be rolled back (round-17 #2).
        await cut.InvokeAsync(() => gate.SetException(new InvalidOperationException("stale expand-all loader failed")));
        await expandAll; // must complete without throwing

        // A follow-up render rethrows any unhandled exception bUnit captured from the faulted handler.
        cut.Render(p => p.Add(c => c.Class, "settled"));
        Assert.Contains("FreshBranch", cut.Markup);
        Assert.False(root.IsLoading);
    }

    // ── #3: a NON-null empty children list (ChildrenLoaded left false) is authoritative-empty ──────

    [Fact]
    public async Task Non_null_empty_children_replace_a_lazily_loaded_subtree()
    {
        var loaded = new List<Item> { new() { Text = "OldDoc", Value = "old" } };
        Func<Item, Task<List<Item>>> loader = _ => Task.FromResult(loaded);

        var folder = new Item { Text = "Folder", Value = "folder", IsLeaf = false };
        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, new List<Item> { folder })
            .Add(c => c.LoadChildren, loader));

        await cut.InvokeAsync(() => Row(cut, "Folder").Click()); // lazy load → store owns OldDoc, seed stays false
        Assert.Contains("OldDoc", cut.Markup);

        // The consumer refreshes the SAME identity with a FRESH instance carrying a NON-null EMPTY list
        // and — critically — leaves ChildrenLoaded at its default false. Per round-17 #3 a non-null list
        // (even empty) is authoritative-empty, NOT an unloaded reset: the stale OldDoc must NOT be
        // reattached, and the branch must not fall back to a re-fetchable lazy state.
        var refreshed = new List<Item>
        {
            new() { Text = "Folder", Value = "folder", IsLeaf = false, Children = new List<Item>() }
        };
        cut.Render(p => p.Add(c => c.Items, refreshed));

        Assert.DoesNotContain("OldDoc", cut.Markup);
    }

    // ── #3 (regression guard): a plain null-children leaf stays a leaf; lazy nodes stay lazy ───────

    [Fact]
    public async Task Plain_leaf_stays_a_leaf_while_null_child_branch_stays_lazy()
    {
        var loaderCalls = 0;
        Func<Item, Task<List<Item>>> loader = _ =>
        {
            loaderCalls++;
            return Task.FromResult(new List<Item> { new() { Text = "Kid", Value = "kid", IsLeaf = true } });
        };

        var items = new List<Item>
        {
            new() { Text = "Leaf", Value = "leaf", IsLeaf = true },                                  // genuine leaf
            new() { Text = "EmptyBranch", Value = "empty", IsLeaf = false, Children = new List<Item>() }, // authoritative-empty
            new() { Text = "Lazy", Value = "lazy", IsLeaf = false }                                  // unloaded lazy (null children)
        };
        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, items)
            .Add(c => c.LoadChildren, loader));

        // Exactly ONE expand chevron — the null-children Lazy branch. The genuine leaf has none, and the
        // NON-null empty branch is authoritative-empty (round-17 #3), so it renders as a leaf too. Nothing
        // has been loaded yet.
        Assert.Single(cut.FindAll("button[aria-label='Expand']"));
        Assert.Equal(0, loaderCalls);

        // The lazy (null-children) branch still lazily loads on expand — the leaf/lazy distinction held.
        await cut.InvokeAsync(() => cut.Find("button[aria-label='Expand']").Click());
        Assert.Equal(1, loaderCalls);
        Assert.Contains("Kid", cut.Markup);
    }

    // ── #4: a deferred row-click load is SKIPPED when the click callback drops the node ────────────

    [Fact]
    public async Task Deferred_row_click_load_is_skipped_when_the_click_removes_the_node()
    {
        IRenderedComponent<L.TreeView<string>>? cut = null;
        var loaderCalls = 0;
        Func<Item, Task<List<Item>>> loader = _ =>
        {
            loaderCalls++;
            return Task.FromResult(new List<Item> { new() { Text = "Kid", Value = "kid" } });
        };

        // OnItemClick REMOVES the clicked node. On the row-click path the lazy tail is deferred past the
        // selection callbacks, so by the time it would run the node is DROPPED — the tail must skip the
        // LoadChildren call entirely rather than fetch-then-discard (round-17 #4).
        var onClick = EventCallback.Factory.Create<Item>(_ctx, (Item _) =>
            cut!.Render(p => p.Add(c => c.Items, new List<Item>())));

        var folder = new Item { Text = "Folder", Value = "folder", IsLeaf = false };
        cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, new List<Item> { folder })
            .Add(c => c.LoadChildren, loader)
            .Add(c => c.OnItemClick, onClick));

        await Row(cut, "Folder").ClickAsync(new MouseEventArgs());

        Assert.Equal(0, loaderCalls); // the fetch never ran for the dropped branch
        Assert.DoesNotContain("Folder", cut.Markup);
    }
}
