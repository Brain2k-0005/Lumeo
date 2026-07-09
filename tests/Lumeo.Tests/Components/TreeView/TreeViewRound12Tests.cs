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
/// PR #351 round-12 (Codex): four findings on the canonical post-await model, all rooted in ONE gap —
/// a controlled/immutable rebuild COPIES optimistic in-flight flags (IsExpanded/IsLoading) onto fresh
/// @key'd instances that no code path owns, and the old paths a same-reference mutation reanchors
/// against are wrong. The model now carries explicit IN-FLIGHT OWNERSHIP (a registry of live loads +
/// an orphan sweep, pillar 5) and a SELECTION-PATH SNAPSHOT (pillar 6). Each test is a genuine
/// regression: it fails on the canonical-model commit (7bd34cb7) and passes after the round-12 fix.
/// </summary>
public class TreeViewRound12Tests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TreeViewRound12Tests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static IElement TreeItem(IRenderedComponent<L.TreeView<string>> cut, string text)
        => cut.FindAll("[role='treeitem']").First(el => el.Children[0].TextContent.Contains(text));

    private static IElement Row(IRenderedComponent<L.TreeView<string>> cut, string text)
        => TreeItem(cut, text).Children[0];

    // ---- Finding 1: dropping an ambiguous lazy load must SWEEP the copied spinner off the fresh node ----

    [Fact]
    public async Task Dropped_ambiguous_lazy_load_sweeps_the_orphaned_spinner_off_the_fresh_node()
    {
        IRenderedComponent<L.TreeView<string>>? cut = null;

        var gate = new TaskCompletionSource<List<Item>>();
        Func<Item, Task<List<Item>>> loader = _ => gate.Task; // hangs until released

        List<Item> current = null!;
        var ver = 0;
        // Two DUPLICATE-valued lazy parents. The controlled rebuild copies BOTH optimistic flags
        // (IsExpanded + IsLoading) from the clicked stale node onto the fresh index-0 instance and bumps
        // the LABEL — so the fresh record is value-DISTINCT from the mutated stale one, its @key differs,
        // and Blazor DISPOSES + recreates the component (rather than reusing it, which would let the node's
        // own finally clear the spinner and attach the load). Values stay "dup" so identity is UNPROVABLE.
        List<Item> Build(bool exp0, bool load0) =>
        [
            new() { Text = $"Alpha v{ver}", Value = "dup", IsLeaf = false, IsExpanded = exp0, IsLoading = load0 },
            new() { Text = $"Beta v{ver}",  Value = "dup", IsLeaf = false, IsExpanded = false }
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

        // Click Alpha → starts the hanging load; the rebuild copies the spinner onto the fresh Alpha.
        await cut.InvokeAsync(() => Row(cut, "Alpha").Click());
        Assert.True(current[0].IsLoading, "the fresh node carries the copied optimistic spinner while the load hangs");
        Assert.Contains("animate-spin", cut.Markup);

        // Release the load. Alpha's identity among the two "dup" siblings is UNPROVABLE, so the resolver
        // DROPS the children — and the orphan sweep must clear the copied spinner off the fresh node (its
        // finally only cleared the STALE instance), returning it to a retryable chevron.
        await cut.InvokeAsync(() =>
            gate.SetResult(new List<Item> { new() { Text = "child", Value = "leaf", IsLeaf = true } }));

        Assert.DoesNotContain("child", cut.Markup);                 // dropped, not attached
        Assert.False(current[0].IsLoading, "the orphan sweep clears the copied spinner off the fresh node");
        Assert.False(current[0].IsExpanded, "the orphan sweep collapses the optimistic expansion (retryable)");
        Assert.DoesNotContain("animate-spin", cut.Markup);
        Assert.Equal("false", TreeItem(cut, "Alpha").GetAttribute("aria-expanded"));
    }

    // ---- Orphan-sweep, isolated: a copied in-flight flag with NO live operation is reaped ----

    [Fact]
    public async Task Orphan_sweep_clears_a_copied_in_flight_flag_with_no_live_operation()
    {
        var child = new Item { Text = "Child", Value = "child" };
        var items = new List<Item>
        {
            new() { Text = "Root", Value = "root", IsExpanded = true, Children = [ child ] }
        };

        var cut = _ctx.Render<L.TreeView<string>>(p => p.Add(c => c.Items, items));
        await cut.InvokeAsync(() => Row(cut, "Child").Click());     // establish a selection → needsResolve later
        Assert.Equal("true", TreeItem(cut, "Child").GetAttribute("aria-selected"));

        // In-place mutation (SAME list reference): replace Root with a fresh instance that (a) rebuilds its
        // children so the selected Child vanishes → the reanchor/resolution pass runs, and (b) carries a
        // stray IsLoading=true that NO in-flight operation registered — the residue a rebuild copies off an
        // already-resolved optimistic node.
        items[0] = new() { Text = "Root", Value = "root", IsExpanded = true, IsLoading = true,
                           Children = [ new() { Text = "Child", Value = "child" } ] };
        cut.Render(p => p.Add(c => c.Items, items));

        Assert.Equal("true", TreeItem(cut, "Child").GetAttribute("aria-selected")); // reanchored
        Assert.False(items[0].IsLoading, "the orphan sweep clears an in-flight flag no live operation owns");
        Assert.DoesNotContain("animate-spin", cut.Markup);
    }

    // ---- Finding 2: an in-place swap reanchors a DUPLICATE-valued node via the path snapshot ----

    [Fact]
    public async Task In_place_swap_reanchors_a_duplicate_valued_node_via_the_path_snapshot()
    {
        var a = new Item { Text = "A", Value = "shared" };
        var items = new List<Item>
        {
            new() { Text = "Root", Value = "root", IsExpanded = true,
                    Children = [ a, new() { Text = "B", Value = "b" } ] },
            new() { Text = "Root2", Value = "root2", IsExpanded = true,
                    Children = [ new() { Text = "C", Value = "shared" } ] }
        };

        var cut = _ctx.Render<L.TreeView<string>>(p => p.Add(c => c.Items, items));

        // Select A ([0,0], value "shared" — duplicated at [1,0] but sibling-unique under Root).
        await cut.InvokeAsync(() => Row(cut, "A").Click());
        Assert.Equal("true", TreeItem(cut, "A").GetAttribute("aria-selected"));

        // In-place mutation (SAME items reference): replace A with a fresh same-valued instance at the same
        // slot. _previousItems is now this SAME mutated list, so the old A is no longer walkable there and
        // the unique-value fallback fails ("shared" is duplicated). Only the PATH SNAPSHOT captured while A
        // was present can reanchor — the value-verified path [0,0] is sibling-unique (round-12 finding 2).
        items[0].Children![0] = new() { Text = "A", Value = "shared" };
        cut.Render(p => p.Add(c => c.Items, items));

        var selected = cut.FindAll("[role='treeitem'][aria-selected='true']");
        Assert.Single(selected);
        Assert.Contains("A", selected[0].Children[0].TextContent);
    }

    // ---- Finding 3: a non-lazy row-click expand re-resolves the fresh node after a rebuild ----

    [Fact]
    public async Task Row_click_expands_an_already_loaded_parent_after_a_controlled_rebuild()
    {
        IRenderedComponent<L.TreeView<string>>? cut = null;

        List<Item> current = null!;
        // Already-loaded parent (children present, NO LoadChildren). The controlled rebuild produces a
        // FRESH parent instance that does NOT copy the optimistic IsExpanded (collapsed by default) — the
        // non-lazy expand tail must re-resolve and flip IsExpanded on the fresh node (round-12 finding 3).
        List<Item> Build() =>
        [
            new() { Text = "Folder", Value = "folder", IsExpanded = false,
                    Children = [ new() { Text = "Doc", Value = "doc" } ] }
        ];

        var callback = EventCallback.Factory.Create<List<string>>(_ctx, (List<string> _) =>
        {
            current = Build(); // fresh collapsed parent — does NOT carry the click's IsExpanded
            cut!.Render(p => p.Add(c => c.Items, current));
        });

        current = Build();
        cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, current)
            .Add(c => c.SelectedValues, (List<string>?)null)
            .Add(c => c.SelectedValuesChanged, callback));

        // Row-click the collapsed parent: it should SELECT and EXPAND. The selection rebuild discards the
        // optimistic IsExpanded; the deferred non-lazy expand tail re-resolves the fresh node and flips it.
        await cut.InvokeAsync(() => Row(cut, "Folder").Click());

        Assert.Equal("true", TreeItem(cut, "Folder").GetAttribute("aria-expanded"));
        Assert.Equal("true", TreeItem(cut, "Folder").GetAttribute("aria-selected"));
        Assert.Contains("Doc", cut.Markup);
        Assert.True(current[0].IsExpanded, "the non-lazy expand tail flips IsExpanded on the fresh node");
    }

    // ---- Finding 4: an in-place swap while FILTERING rebuilds the filtered display ----

    [Fact]
    public async Task In_place_swap_while_filtering_rebuilds_the_filtered_display()
    {
        var items = new List<Item>
        {
            new() { Text = "Report", Value = "report" },
            new() { Text = "Readme", Value = "readme" }
        };

        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, items)
            .Add(c => c.ShowSearch, true));

        // Filter to "Re" → _displayItems becomes a filtered ToList() snapshot (NOT the live Items).
        cut.Find("input").Input("Re");
        await cut.InvokeAsync(() => Row(cut, "Report").Click());
        Assert.Equal("true", TreeItem(cut, "Report").GetAttribute("aria-selected"));

        // In-place swap (SAME reference): fresh instance with a CHANGED label but the SAME value. The value
        // reanchors the selection, but the render still walks the stale filtered _displayItems — only if it
        // is REBUILT off the mutated Items will the new label (and the reanchored selection) appear.
        items[0] = new() { Text = "Report v2", Value = "report" };
        cut.Render(p => p.Add(c => c.Items, items));

        Assert.Contains("Report v2", cut.Markup); // filtered display re-materialized off the mutated Items
        var selected = cut.FindAll("[role='treeitem'][aria-selected='true']");
        Assert.Single(selected);
        Assert.Contains("Report v2", selected[0].Children[0].TextContent);
    }
}
