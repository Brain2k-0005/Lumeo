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
/// PR #351 Codex round-20 — a lazy expand-all load that resolves AFTER its generation was superseded
/// (here by CollapseAllNodes) must still recompute cascade check state when it attaches the loaded
/// children (see the superseded-attach block in LoadAndExpandAllAsync, TreeView.razor). Round-19 made
/// the superseded pass ATTACH the fetched children (gated on the LoadToken, not _expandGeneration);
/// round-20 makes it also DERIVE the parent's tri-state from those children before returning on the
/// generation check.
///
/// The bug: the attach ran, then the pass returned BEFORE the single final UpdateAncestorStates in
/// ExpandAllNodesAsync (also skipped, because that method returns on the same generation mismatch).
/// So a parent that was NOT already fully checked — whose freshly materialized children arrive with
/// seeded checked/indeterminate state — kept a stale unchecked checkbox until some later interaction,
/// unlike the per-node lazy load path which recomputes immediately after materialization. The fix
/// mirrors that per-node recompute in the expand-all attach: only the descent/expansion is skipped.
/// </summary>
public class TreeViewRound20Tests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TreeViewRound20Tests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task CollapseAll_during_expand_all_load_still_derives_parent_check_state_from_seeded_children()
    {
        var gate = new TaskCompletionSource<List<Item>>();
        var loaderCalls = 0;
        Func<Item, Task<List<Item>>> loader = _ =>
        {
            loaderCalls++;
            return gate.Task;
        };

        // Root is NOT fully checked (unchecked, not indeterminate). This is the case the fix targets:
        // a fully-checked parent would push its check DOWN onto the loaded children instead of deriving
        // UP from them, so it could never expose the stale-checkbox bug.
        var root = new Item { Text = "Root", Value = "root", IsLeaf = false };
        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, new List<Item> { root })
            .Add(c => c.ShowCheckboxes, true)   // CascadeCheck defaults to true
            .Add(c => c.LoadChildren, loader));

        // Expand-all STARTS Root's lazy load (generation 1) and parks on the gate.
        var expand = cut.InvokeAsync(() => cut.Instance.ExpandAllNodesAsync());
        Assert.True(root.IsLoading);
        Assert.Equal(1, loaderCalls);

        // Collapse-all mid-load: bumps _expandGeneration, so the in-flight expand-all pass is now
        // SUPERSEDED. When its load resolves it will attach the children then return on the generation
        // check — the path that used to skip the cascade recompute.
        await cut.InvokeAsync(() => cut.Instance.CollapseAllNodes());

        // Resolve the load with children carrying SEEDED check state: one checked, one not. Deriving the
        // parent from these yields INDETERMINATE — a state neither the pre-load parent (unchecked) nor a
        // push-down (all-checked) could produce, so it can ONLY come from the on-attach recompute.
        await cut.InvokeAsync(() => gate.SetResult(new List<Item>
        {
            new() { Text = "Checked",   Value = "c1", IsLeaf = true, IsChecked = true },
            new() { Text = "Unchecked", Value = "c2", IsLeaf = true, IsChecked = false },
        }));

        var done = await Task.WhenAny(expand, Task.Delay(Timeout));
        Assert.True(ReferenceEquals(done, expand), "expand-all hung on the superseded load");
        await expand;

        Assert.Equal(1, loaderCalls);
        Assert.False(root.IsLoading);

        // Round-19 contract still holds: the superseded pass ATTACHED the fetched children...
        Assert.NotNull(root.Children);
        Assert.Equal(2, root.Children!.Count);
        // ...and left their seeded check state untouched (parent was not fully checked → no push-down).
        Assert.True(root.Children[0].IsChecked);
        Assert.False(root.Children[1].IsChecked);

        // Round-20 fix: the parent's tri-state was DERIVED from the seeded children on attach. Pre-fix
        // it stayed unchecked+not-indeterminate (the stale checkbox) because the pass returned before
        // any UpdateAncestorStates ran.
        Assert.True(root.IsIndeterminate);
        Assert.False(root.IsChecked);

        // Collapse-all still won the race: the superseded pass skipped the descent/expansion — only the
        // cascade recompute was added, not a re-expand of the tree the user just collapsed.
        Assert.False(root.IsExpanded);
    }
}
