using Bunit;
using AngleSharp.Dom;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.TreeView;

using Item = L.TreeView<string>.TreeViewItem<string>;

/// <summary>
/// PR #351 Codex round-15 — SEED vs STORE vs LOAD-COMPLETION precedence for the tree-owned
/// Children / ChildrenLoaded / Expanded fields (all three findings are the same contract gap:
/// the ownership rebuild diffed the incoming record against our own MIRROR write-back, so it could
/// not tell 'consumer changed it' from 'consumer never set it', and a stale loader could clobber
/// consumer-authoritative data). The fix: a FRESH instance is diffed against the last INCOMING
/// consumer SEED (never our mirror); a REFERENCE-matched instance against the store; a load
/// completion carries a LoadToken that a consumer supersession invalidates.
///
///   Finding 1  — CONSUMER LAZY-BRANCH RESET: presenting a lazy-loaded node as unloaded
///                (ChildrenLoaded=false + no children) on the same identity clears the tree-owned
///                children and collapses to the pristine unloaded lazy state.
///   Finding 2  — EXPAND INTENT ACROSS DOMAIN-PURE REBUILDS: a pending lazy expand survives a
///                controlled rebuild whose fresh record carries a pristine IsExpanded=false.
///   Finding 3  — AUTHORITATIVE CHILDREN VS IN-FLIGHT LOAD: consumer-supplied children win; the
///                later completion of the superseded loader is discarded, not applied.
///
/// Plus the positive counterparts that pin the tie-breakers: the normal lazy flow is unaffected
/// (a domain-pure rebuild PRESERVES tree-loaded children), and the mirror write-back does NOT
/// self-trigger as a seed change (a tree expand survives an unrelated re-render then a domain rebuild).
/// </summary>
public class TreeViewSeedPrecedenceTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TreeViewSeedPrecedenceTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static IElement TreeItem(IRenderedComponent<L.TreeView<string>> cut, string text)
        => cut.FindAll("[role='treeitem']").First(el => el.Children[0].TextContent.Contains(text));

    private static IElement Row(IRenderedComponent<L.TreeView<string>> cut, string text)
        => TreeItem(cut, text).Children[0];

    // ── Finding 1: a consumer reset of a lazy-loaded branch unloads + collapses it ───────────────

    [Fact]
    public async Task Consumer_reset_of_a_lazy_loaded_branch_unloads_and_collapses_it()
    {
        var loaded = new List<Item> { new() { Text = "Doc", Value = "doc" } };
        Func<Item, Task<List<Item>>> loader = _ => Task.FromResult(loaded);

        var folder = new Item { Text = "Folder", Value = "folder", IsLeaf = false };
        var items = new List<Item> { folder };
        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, items)
            .Add(c => c.LoadChildren, loader));

        await cut.InvokeAsync(() => Row(cut, "Folder").Click()); // expand + lazy load
        Assert.True(folder.ChildrenLoaded);
        Assert.Contains("Doc", cut.Markup);

        // The consumer refreshes the SAME identity back to an unloaded record (children cleared,
        // ChildrenLoaded=false) — an intentional reset. Even though it still asks to stay expanded,
        // an unloaded lazy node has no defined content, so the reset collapses it.
        folder.Children = null;
        folder.ChildrenLoaded = false;
        folder.IsExpanded = true;
        cut.Render(p => p.Add(c => c.Items, items));

        Assert.False(folder.ChildrenLoaded, "the store dropped its materialized children");
        Assert.False(folder.IsExpanded, "reset collapses back to the pristine unloaded lazy state");
        Assert.DoesNotContain("Doc", cut.Markup);
    }

    // ── Positive counterpart: a domain-pure rebuild PRESERVES tree-loaded children (normal flow) ─

    [Fact]
    public async Task Domain_pure_rebuild_of_a_lazy_loaded_branch_keeps_its_children()
    {
        var loaded = new List<Item> { new() { Text = "Doc", Value = "doc" } };
        Func<Item, Task<List<Item>>> loader = _ => Task.FromResult(loaded);

        var items = new List<Item> { new() { Text = "Folder", Value = "folder", IsLeaf = false } };
        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, items)
            .Add(c => c.LoadChildren, loader));

        await cut.InvokeAsync(() => Row(cut, "Folder").Click());
        Assert.Contains("Doc", cut.Markup);

        // A controlled consumer rebuilds from domain data: a FRESH unloaded instance (uniquely valued,
        // so the entry follows it). It never acknowledged the lazy load, so this is the normal lazy
        // flow, NOT a reset — the tree-owned children must survive.
        var rebuilt = new List<Item> { new() { Text = "Folder", Value = "folder", IsLeaf = false } };
        cut.Render(p => p.Add(c => c.Items, rebuilt));

        Assert.True(rebuilt[0].ChildrenLoaded, "the tree re-attached its materialized children");
        Assert.Contains("Doc", cut.Markup);
    }

    // ── Finding 2: a pending lazy expand survives a domain-pure rebuild that drops the flag ──────

    [Fact]
    public async Task Pending_lazy_expand_survives_a_domain_pure_rebuild_that_drops_the_expand_flag()
    {
        var gate = new TaskCompletionSource<List<Item>>();
        Func<Item, Task<List<Item>>> loader = _ => gate.Task;

        var items = new List<Item> { new() { Text = "Folder", Value = "folder", IsLeaf = false } };
        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, items)
            .Add(c => c.LoadChildren, loader));

        await cut.InvokeAsync(() => Row(cut, "Folder").Click()); // expand → load hangs
        Assert.True(items[0].IsExpanded);

        // While the load is pending, the consumer rebuilds from domain data: a FRESH instance whose
        // IsExpanded is a pristine false (the domain model doesn't track UI expansion). This must NOT
        // read as a consumer collapse — the pending expand intent survives to the completion.
        var rebuilt = new List<Item>
            { new() { Text = "Folder", Value = "folder", IsLeaf = false, IsExpanded = false } };
        cut.Render(p => p.Add(c => c.Items, rebuilt));

        await cut.InvokeAsync(() => gate.SetResult(new List<Item> { new() { Text = "Doc", Value = "doc" } }));

        Assert.True(rebuilt[0].ChildrenLoaded, "the children attached to the rebuilt instance");
        Assert.True(rebuilt[0].IsExpanded, "the pending expand intent survived the domain-pure rebuild");
        Assert.Contains("Doc", cut.Markup);
    }

    // ── Finding 3: consumer-authoritative children win over a later in-flight load completion ────

    [Fact]
    public async Task Consumer_supplied_children_win_over_a_later_in_flight_load_completion()
    {
        var gate = new TaskCompletionSource<List<Item>>();
        Func<Item, Task<List<Item>>> loader = _ => gate.Task;

        var items = new List<Item> { new() { Text = "Folder", Value = "folder", IsLeaf = false } };
        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, items)
            .Add(c => c.LoadChildren, loader));

        await cut.InvokeAsync(() => Row(cut, "Folder").Click()); // expand → load hangs
        Assert.True(items[0].IsLoading);

        // Before the load resolves, the consumer refreshes the SAME identity with AUTHORITATIVE
        // children (a fresh instance carrying ChildrenLoaded=true + real children, e.g. a newer
        // server snapshot). Consumer data supersedes the store AND the in-flight loader.
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
        Assert.False(authoritative[0].IsLoading, "the superseded load's spinner cleared");

        // The STALE loader now resolves — it must NOT overwrite the consumer's authoritative children.
        await cut.InvokeAsync(() =>
            gate.SetResult(new List<Item> { new() { Text = "StaleDoc", Value = "stale" } }));

        Assert.Contains("FreshDoc", cut.Markup);
        Assert.DoesNotContain("StaleDoc", cut.Markup);
        Assert.Equal("FreshDoc", authoritative[0].Children![0].Text);
    }

    // ── Positive counterpart: the mirror write-back does NOT self-trigger as a seed change ───────

    [Fact]
    public async Task Tree_expand_survives_an_unrelated_rerender_then_a_domain_pure_rebuild()
    {
        var items = new List<Item>
        {
            new()
            {
                Text = "Parent", Value = "parent",
                Children = new List<Item> { new() { Text = "Leaf", Value = "leaf" } }
            }
        };
        var cut = _ctx.Render<L.TreeView<string>>(p => p.Add(c => c.Items, items));

        // Expand the (non-lazy) parent via its chevron — a pure tree operation; no consumer flag set.
        await cut.InvokeAsync(() => cut.Find("button[aria-label='Expand']").Click());
        Assert.True(items[0].IsExpanded);
        Assert.Contains("Leaf", cut.Markup);

        // (i) An unrelated same-reference re-render: the mirror wrote IsExpanded=true onto the SAME
        // instance, and reading it back must NOT be recorded as a consumer seed (the self-trigger
        // regression) — otherwise the next domain rebuild's false would look like a collapse.
        cut.Render(p => p.Add(c => c.Class, "noop"));
        Assert.True(items[0].IsExpanded);
        Assert.Contains("Leaf", cut.Markup);

        // (ii) A domain-pure FRESH rebuild whose IsExpanded is a pristine false: the consumer never
        // actually changed the seed, so the tree-owned expansion survives.
        var rebuilt = new List<Item>
        {
            new()
            {
                Text = "Parent", Value = "parent",
                Children = new List<Item> { new() { Text = "Leaf", Value = "leaf" } }
            }
        };
        cut.Render(p => p.Add(c => c.Items, rebuilt));

        Assert.True(rebuilt[0].IsExpanded, "tree expansion survived the domain-pure rebuild (no self-trigger)");
        Assert.Contains("Leaf", cut.Markup);
    }
}
