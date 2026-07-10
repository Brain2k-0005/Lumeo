using System;
using System.Collections.Generic;
using System.Linq;
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
/// PR #351 Codex round-18 — three more tree-owned lazy-load gaps (see the LoadSettled field doc,
/// the round-17 #1 note in the OPERATION SEMANTICS block, and the LOADED-NESS BASIS registration
/// note in TreeView.razor). Each test is the failing-then-fixed repro of one finding:
///
///   #1  EXPAND-ALL FROM A DEFERRED ROW CLICK MUST NOT DEADLOCK — a row-click lazy tail is DEFERRED
///       past the selection callbacks, so BeginToggle sets Loading with NO loader running yet. When
///       ExpandAllNodesAsync is invoked reentrantly from that click's OnItemClick, it must NOT await
///       the not-yet-started load (which would hang) — it loads the branch itself and supersedes the
///       tail. Guarded by a timeout so a regression surfaces as a failure, not a hung suite.
///   #2  SUPERSESSION RELEASES A PARKED WAITER — an ExpandAllNodesAsync parked on a genuinely
///       in-flight (chevron-started) load must be RELEASED the moment the consumer supersedes that
///       load with authoritative children, even if the stale loader never completes (hangs).
///   #3  LOADED-EMPTY REGISTRATION — a lazy loader returning a NON-null EMPTY sub-branch (ChildrenLoaded
///       left default-false) is authoritative-loaded-empty; on registration its loaded-ness is mirrored
///       onto the record, so it renders as a leaf (no chevron) rather than a re-expandable lazy node.
/// </summary>
public class TreeViewRound18Tests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TreeViewRound18Tests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private static IElement TreeItem(IRenderedComponent<L.TreeView<string>> cut, string text)
        => cut.FindAll("[role='treeitem']").First(el => el.Children[0].TextContent.Contains(text));

    private static IElement Row(IRenderedComponent<L.TreeView<string>> cut, string text)
        => TreeItem(cut, text).Children[0];

    // ── #1: a reentrant expand-all fired from the row click's OnItemClick must not hang ────────────

    [Fact]
    public async Task Expand_all_invoked_from_a_lazy_row_click_does_not_deadlock()
    {
        IRenderedComponent<L.TreeView<string>>? cut = null;
        var loaderCalls = 0;
        Func<Item, Task<List<Item>>> loader = _ =>
        {
            loaderCalls++;
            return Task.FromResult(new List<Item> { new() { Text = "Kid", Value = "kid", IsLeaf = true } });
        };

        // OnItemClick reentrantly AWAITS ExpandAllNodesAsync. On the row-click path the lazy tail is
        // DEFERRED past this very callback, so BeginToggle has set Loading but the loader has NOT
        // started. Expand-all must not await that not-yet-started load (deadlock) — it loads the branch
        // itself and supersedes the deferred tail (round-18 #1).
        var onClick = EventCallback.Factory.Create<Item>(_ctx, async (Item _) =>
            await cut!.Instance.ExpandAllNodesAsync());

        var folder = new Item { Text = "Folder", Value = "folder", IsLeaf = false };
        cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, new List<Item> { folder })
            .Add(c => c.LoadChildren, loader)
            .Add(c => c.OnItemClick, onClick));

        // The full handler — select + reentrant expand-all + deferred tail — must complete without
        // hanging. A timeout guard turns a regression into a failing assert instead of a stuck suite.
        var click = Row(cut, "Folder").ClickAsync(new MouseEventArgs());
        var finished = await Task.WhenAny(click, Task.Delay(Timeout));
        Assert.True(ReferenceEquals(finished, click),
            "row click + reentrant ExpandAllNodesAsync deadlocked on the deferred (not-yet-started) load");
        await click; // observe / rethrow

        Assert.Equal(1, loaderCalls);        // exactly one fetch — expand-all loaded it; the tail skipped
        Assert.True(folder.IsExpanded);
        Assert.Contains("Kid", cut.Markup);  // subtree realized, not left collapsed
    }

    // ── #2: a supersession RELEASES an expand-all parked on the stale (hung) in-flight load ────────

    [Fact]
    public async Task Expand_all_parked_on_an_in_flight_load_is_released_by_a_consumer_supersession()
    {
        var gate = new TaskCompletionSource<List<Item>>(); // NEVER completed — the stale loader "hangs"
        Func<Item, Task<List<Item>>> loader = _ => gate.Task;

        var root = new Item { Text = "Root", Value = "root", IsLeaf = false };
        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, new List<Item> { root })
            .Add(c => c.LoadChildren, loader));

        // A CHEVRON expand starts Root's lazy load; it hangs on the gate (its LoadSettled signal live).
        await cut.InvokeAsync(() => cut.Find("button[aria-label='Expand']").Click());
        Assert.True(root.IsLoading);

        // Expand-all meets a Root whose loader is ACTUALLY RUNNING → it parks on the entry's LoadSettled.
        var expandAll = cut.InvokeAsync(() => cut.Instance.ExpandAllNodesAsync());

        // The consumer supersedes the hung load with AUTHORITATIVE children on the SAME live Root (bumps
        // the LoadToken). The parked expand-all must be RELEASED at this supersession — it must NOT keep
        // waiting on the gate, which never completes (round-18 #2).
        root.Children = new List<Item> { new() { Text = "FreshBranch", Value = "fresh", IsLeaf = true } };
        root.ChildrenLoaded = true;
        cut.Render(p => p.Add(c => c.Items, new List<Item> { root }));
        Assert.Contains("FreshBranch", cut.Markup);

        var finished = await Task.WhenAny(expandAll, Task.Delay(Timeout));
        Assert.True(ReferenceEquals(finished, expandAll),
            "expand-all was not released by the supersession and hung on the stale loader");
        await expandAll; // must complete without throwing

        Assert.True(root.IsExpanded);
        Assert.False(root.IsLoading);
    }

    // ── #3: a lazily-loaded NON-null empty branch renders as a leaf, not a re-expandable lazy node ──

    [Fact]
    public async Task A_lazily_loaded_empty_branch_does_not_render_as_re_expandable()
    {
        var loaderCalls = 0;
        Func<Item, Task<List<Item>>> loader = _ =>
        {
            loaderCalls++;
            // Root's sole child is an authoritative-EMPTY sub-branch: a NON-null empty list with
            // ChildrenLoaded left default-false. Per the non-null-list rule it is loaded-empty, NOT a
            // re-expandable lazy node.
            return Task.FromResult(new List<Item>
            {
                new() { Text = "EmptyChild", Value = "empty", IsLeaf = false, Children = new List<Item>() }
            });
        };

        var root = new Item { Text = "Root", Value = "root", IsLeaf = false };
        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, new List<Item> { root })
            .Add(c => c.LoadChildren, loader));

        // Expand Root (lazy). Its children materialize via RegisterSubtree → GetState, which must mirror
        // the computed loaded-ness onto EmptyChild (round-18 #3) — BEFORE any later parameter set — so
        // it renders WITHOUT a chevron.
        await cut.InvokeAsync(() => cut.Find("button[aria-label='Expand']").Click());
        Assert.Contains("EmptyChild", cut.Markup);
        Assert.Equal(1, loaderCalls);

        // Root is now expanded (its chevron reads Collapse). EmptyChild is authoritative-loaded-empty, so
        // it must contribute NO expand chevron — exactly ONE chevron total (Root's). Without the mirror
        // EmptyChild would show a second Expand chevron and re-fetch on click.
        var chevrons = cut.FindAll("button[aria-label='Expand'], button[aria-label='Collapse']");
        Assert.Single(chevrons);
        Assert.Equal("Collapse", chevrons[0].GetAttribute("aria-label"));
        Assert.Equal(1, loaderCalls);
    }
}
