using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.TreeView;

using Item = L.TreeView<string>.TreeViewItem<string>;

/// <summary>
/// PR #351 Codex round-19 — overlapping expand-all passes must be idempotent (see the expand-all
/// descent in LoadAndExpandAllAsync, TreeView.razor). When ExpandAllNodesAsync is reissued (a
/// double-clicked "expand all") while an earlier expand-all's lazy load is still in flight, the
/// second pass PARKS on the first pass's LoadSettled instead of starting its own fetch. The first
/// pass then completes the load, but the newer pass's generation bump must NOT make it discard the
/// loaded children: it attaches them ONCE (gated on the LoadToken, not _expandGeneration) so the
/// parked sibling pass wakes, finds them, and descends. Gating the attach on _expandGeneration left
/// the branch expanded-but-empty until a THIRD expand attempt.
/// </summary>
public class TreeViewRound19Tests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TreeViewRound19Tests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Second_expand_all_during_an_in_flight_load_still_realizes_the_subtree()
    {
        var gate = new TaskCompletionSource<List<Item>>();
        var loaderCalls = 0;
        Func<Item, Task<List<Item>>> loader = _ =>
        {
            loaderCalls++;
            return gate.Task; // ONE gated fetch shared by whoever started it
        };

        var root = new Item { Text = "Root", Value = "root", IsLeaf = false };
        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, new List<Item> { root })
            .Add(c => c.LoadChildren, loader));

        // Pass 1 STARTS Root's lazy load (installs its LoadSettled) and parks on the gate.
        var first = cut.InvokeAsync(() => cut.Instance.ExpandAllNodesAsync());
        Assert.True(root.IsLoading);
        Assert.Equal(1, loaderCalls);

        // Pass 2 (the double-click) bumps the expand generation, meets a Root already loading, and PARKS
        // on the SAME in-flight LoadSettled — it must NOT start a second fetch.
        var second = cut.InvokeAsync(() => cut.Instance.ExpandAllNodesAsync());
        Assert.Equal(1, loaderCalls);

        // Resolve the single in-flight load with an initially-COLLAPSED nested Branch (its own Leaf
        // already loaded). Pass 1 is now generation-superseded by Pass 2, but it must still ATTACH these
        // children (not discard them on the generation check) so the parked Pass 2 can descend.
        await cut.InvokeAsync(() => gate.SetResult(new List<Item>
        {
            new()
            {
                Text = "Branch", Value = "branch", IsLeaf = false, ChildrenLoaded = true,
                Children = new List<Item> { new() { Text = "Leaf", Value = "leaf", IsLeaf = true } }
            }
        }));

        // Neither concurrent call may throw or hang (timeout guards turn a regression into a failing
        // assert, not a stuck suite).
        var firstDone = await Task.WhenAny(first, Task.Delay(Timeout));
        Assert.True(ReferenceEquals(firstDone, first), "first expand-all hung on the shared load");
        await first;
        var secondDone = await Task.WhenAny(second, Task.Delay(Timeout));
        Assert.True(ReferenceEquals(secondDone, second), "second expand-all hung parked on the shared load");
        await second;

        Assert.Equal(1, loaderCalls);          // loader invoked exactly ONCE across both passes
        Assert.False(root.IsLoading);
        Assert.True(root.IsExpanded);
        Assert.Contains("Branch", cut.Markup); // children ATTACHED — pre-fix this branch was empty
        Assert.True(root.Children![0].IsExpanded,
            "the surviving pass descended into the loaded branch (fully expanded), not left collapsed");
        Assert.Contains("Leaf", cut.Markup);   // fully expanded down to the leaf
    }
}
