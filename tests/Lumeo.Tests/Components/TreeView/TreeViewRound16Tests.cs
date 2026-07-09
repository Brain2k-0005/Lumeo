using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bunit;
using AngleSharp.Dom;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.TreeView;

using Item = L.TreeView<string>.TreeViewItem<string>;

/// <summary>
/// PR #351 Codex round-16 — four seed/store/load-token precedence gaps in the tree-owned
/// Children / ChildrenLoaded contract (see the CHILDREN/CHILDRENLOADED precedence table in
/// TreeView.razor). Each test is the failing-then-fixed repro of one finding:
///
///   f1  EMPTY AUTHORITATIVE LIST — a loaded refresh whose Children regresses to an EMPTY list is a
///       genuine 'this branch is now empty' replacement, not a no-op; the stale subtree must not be
///       reattached.
///   f2  SUPERSEDED LOAD FAILURE — once a lazy load is superseded (consumer took the branch over), its
///       later REJECTION is discarded as silently as a superseded SUCCESS: no error surfaces, no
///       rollback of the branch it no longer owns.
///   f3  SEED ADVANCE ON LIVE REPLACEMENT — adopting a new authoritative list on the SAME live instance
///       must advance the loaded-seed, so a later fresh reset is diffed against a true basis and honored
///       instead of being misread as a domain-pure rebuild.
///   f4  EXPAND-ALL DESCENT AFTER SUPERSESSION — an in-flight expand-all whose lazy branch is superseded
///       by consumer-authoritative children must still descend into and expand those new children.
/// </summary>
public class TreeViewRound16Tests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TreeViewRound16Tests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static IElement TreeItem(IRenderedComponent<L.TreeView<string>> cut, string text)
        => cut.FindAll("[role='treeitem']").First(el => el.Children[0].TextContent.Contains(text));

    private static IElement Row(IRenderedComponent<L.TreeView<string>> cut, string text)
        => TreeItem(cut, text).Children[0];

    // ── f1: an empty authoritative children list REPLACES the previously loaded subtree ───────────

    [Fact]
    public void Empty_authoritative_children_replace_a_previously_loaded_subtree()
    {
        // Initial: a loaded, expanded branch with one child (the consumer's own authoritative data, so
        // the loaded-SEED is true — the precondition that makes an unchanged loaded flag look like a
        // no-op in the buggy path).
        var folder = new Item
        {
            Text = "Folder", Value = "folder", IsLeaf = false, IsExpanded = true, ChildrenLoaded = true,
            Children = new List<Item> { new() { Text = "Doc", Value = "doc" } }
        };
        var cut = _ctx.Render<L.TreeView<string>>(p => p.Add(c => c.Items, new List<Item> { folder }));
        Assert.Contains("Doc", cut.Markup);

        // The consumer refreshes the SAME identity: still loaded + expanded, but the authoritative
        // children list is now EMPTY (the server says the branch no longer has children). A fresh
        // instance whose ChildrenLoaded stays true while Children regresses to an EMPTY list is an
        // authoritative REPLACEMENT, not a no-op — the stale "Doc" must NOT be reattached (round-16 f1).
        var refreshed = new List<Item>
        {
            new()
            {
                Text = "Folder", Value = "folder", IsLeaf = false, IsExpanded = true, ChildrenLoaded = true,
                Children = new List<Item>()
            }
        };
        cut.Render(p => p.Add(c => c.Items, refreshed));

        Assert.True(refreshed[0].ChildrenLoaded, "the branch stays loaded");
        Assert.Empty(refreshed[0].Children!);
        Assert.DoesNotContain("Doc", cut.Markup);
    }

    // ── f2: a superseded lazy load that FAILS is discarded silently (like a superseded success) ───

    [Fact]
    public async Task Superseded_lazy_load_failure_is_swallowed_like_a_superseded_success()
    {
        var gate = new TaskCompletionSource<List<Item>>();
        Func<Item, Task<List<Item>>> loader = _ => gate.Task;

        var items = new List<Item> { new() { Text = "Folder", Value = "folder", IsLeaf = false } };
        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, items)
            .Add(c => c.LoadChildren, loader));

        // Expand via the CHEVRON — the path that SURFACES a live loader failure (the row click absorbs
        // it via DrainExpansionIsolated). The lazy load hangs.
        await cut.InvokeAsync(() => cut.Find("button[aria-label='Expand']").Click());
        Assert.True(items[0].IsLoading);

        // The consumer supersedes the in-flight load with AUTHORITATIVE children (bumps the LoadToken).
        var authoritative = new List<Item>
        {
            new()
            {
                Text = "Folder", Value = "folder", IsLeaf = false, IsExpanded = true, ChildrenLoaded = true,
                Children = new List<Item> { new() { Text = "FreshDoc", Value = "fresh" } }
            }
        };
        cut.Render(p => p.Add(c => c.Items, authoritative));
        Assert.Contains("FreshDoc", cut.Markup);

        // The superseded loader now FAILS. A dead operation's failure must be discarded as SILENTLY as a
        // superseded SUCCESS: no exception may surface (even on the chevron path), and the consumer's
        // authoritative branch must NOT be rolled back to collapsed (round-16 f2).
        await cut.InvokeAsync(() => gate.SetException(new InvalidOperationException("stale loader failed")));

        // A follow-up render rethrows any unhandled exception bUnit captured from the faulted handler.
        cut.Render(p => p.Add(c => c.Class, "settled"));
        Assert.Contains("FreshDoc", cut.Markup);
        Assert.Equal("true", TreeItem(cut, "Folder").GetAttribute("aria-expanded"));
        Assert.False(authoritative[0].IsLoading);
    }

    // ── f3: adopting a live child replacement advances the seed, so a later reset is honored ──────

    [Fact]
    public async Task Live_child_replacement_advances_the_seed_so_a_later_reset_is_honored()
    {
        var loaded = new List<Item> { new() { Text = "OldDoc", Value = "old" } };
        Func<Item, Task<List<Item>>> loader = _ => Task.FromResult(loaded);

        var folder = new Item { Text = "Folder", Value = "folder", IsLeaf = false };
        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, new List<Item> { folder })
            .Add(c => c.LoadChildren, loader));

        await cut.InvokeAsync(() => Row(cut, "Folder").Click()); // lazy load → store loaded, seed stays false
        Assert.Contains("OldDoc", cut.Markup);

        // The consumer replaces Children on the SAME live instance (a reference match) with a new
        // authoritative list, keeping ChildrenLoaded=true. The tree adopts them — and MUST advance the
        // loaded-seed to record the consumer's loaded-ness, even though the loaded FLAG did not change
        // (round-16 f3). A NEW list wrapper (same folder instance) forces the display to rebuild.
        folder.Children = new List<Item> { new() { Text = "NewDoc", Value = "new" } };
        cut.Render(p => p.Add(c => c.Items, new List<Item> { folder }));
        Assert.Contains("NewDoc", cut.Markup);
        Assert.DoesNotContain("OldDoc", cut.Markup);

        // The consumer now RESETS the branch via a FRESH unloaded instance (ChildrenLoaded=false, no
        // children). With the seed correctly advanced to true this regression reads as a reset and is
        // honored; without the fix the stale false seed makes it look domain-pure and the prior children
        // are wrongly kept.
        var reset = new List<Item> { new() { Text = "Folder", Value = "folder", IsLeaf = false } };
        cut.Render(p => p.Add(c => c.Items, reset));

        Assert.False(reset[0].ChildrenLoaded, "the reset cleared the tree-owned children");
        Assert.DoesNotContain("NewDoc", cut.Markup);
    }

    // ── f4: expand-all keeps expanding consumer-supplied children after a mid-pass supersession ───

    [Fact]
    public async Task Expand_all_keeps_expanding_consumer_supplied_children_after_a_supersession()
    {
        var gate = new TaskCompletionSource<List<Item>>();
        Func<Item, Task<List<Item>>> loader = _ => gate.Task;

        var root = new Item { Text = "Root", Value = "root", IsLeaf = false };
        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, new List<Item> { root })
            .Add(c => c.LoadChildren, loader));

        var tree = cut.Instance;
        // Expand-all begins and BLOCKS on Root's lazy load.
        var expandAll = cut.InvokeAsync(() => tree.ExpandAllNodesAsync());
        Assert.True(root.IsLoading);

        // Before the stale loader resolves, the consumer supplies AUTHORITATIVE children for the SAME
        // live Root: a nested Branch that itself has a Leaf, initially collapsed. This bumps Root's
        // LoadToken (supersession).
        root.Children = new List<Item>
        {
            new()
            {
                Text = "Branch", Value = "branch", IsLeaf = false, ChildrenLoaded = true,
                Children = new List<Item> { new() { Text = "Leaf", Value = "leaf", IsLeaf = true } }
            }
        };
        root.ChildrenLoaded = true;
        cut.Render(p => p.Add(c => c.Items, new List<Item> { root }));
        Assert.Contains("Branch", cut.Markup);
        Assert.DoesNotContain("Leaf", cut.Markup); // Branch is not expanded yet

        // The stale loader resolves; its result is discarded on the token mismatch, but the still-running
        // expand-all must DESCEND into the consumer's authoritative children and expand them, so Branch
        // opens and Leaf becomes visible (round-16 f4).
        await cut.InvokeAsync(() =>
            gate.SetResult(new List<Item> { new() { Text = "Stale", Value = "stale" } }));
        await expandAll;

        Assert.DoesNotContain("Stale", cut.Markup);
        Assert.True(root.Children![0].IsExpanded, "expand-all descended into the consumer's authoritative Branch");
        Assert.Contains("Leaf", cut.Markup);
    }
}
