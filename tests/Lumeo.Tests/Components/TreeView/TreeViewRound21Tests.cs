using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AngleSharp.Dom;
using Microsoft.AspNetCore.Components;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.TreeView;

using Item = L.TreeView<string>.TreeViewItem<string>;

/// <summary>
/// PR #351 Codex round-21.
///
/// Finding 1 — FRESH CHILD STATE WITHOUT PATH: a freshly lazy-loaded child was registered with no
/// structural <c>Path</c>. If a controlled rebuild swaps in fresh instances BEFORE the next parameter
/// pass indexes that child, <c>ClaimState</c> can neither reference-match it nor tree-unique-value-match
/// it when its Value is duplicated elsewhere — only its value-verified sibling-unique path can anchor
/// it, so its tree-owned state (e.g. an expanded lazy child's expansion) was dropped. The fix seeds the
/// path at registration (parent path + sibling index), so it reanchors on the rebuild.
///
/// Finding 2 — IMMUTABLE SELECTION ECHOES: a controlled parent that accepts SelectedValuesChanged by
/// COPYING the pushed list into a value-equal new instance was misread as an authoritative new seed and
/// re-resolved by value — converting the interactive IDENTITY selection back into the value contract and
/// lighting up every same-valued node. A value-equal copy of the last push is now recognized as an echo.
/// </summary>
public class TreeViewRound21Tests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TreeViewRound21Tests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private static IElement TreeItem(IRenderedComponent<L.TreeView<string>> cut, string text)
        => cut.FindAll("[role='treeitem']").First(el => el.Children[0].TextContent.Contains(text));

    private static IElement Row(IRenderedComponent<L.TreeView<string>> cut, string text)
        => TreeItem(cut, text).Children[0];

    private static IReadOnlyList<IElement> Selected(IRenderedComponent<L.TreeView<string>> cut)
        => cut.FindAll("[role='treeitem'][aria-selected='true']");

    // ── Finding 1 ──────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Duplicate_valued_lazy_child_keeps_expansion_across_controlled_rebuild()
    {
        // R1 is a lazy parent; its loaded child's Value ("shared") is DUPLICATED elsewhere (D under R2),
        // so a tree-unique-value claim is impossible. The child is expanded (tree-owned state). A
        // controlled rebuild then re-materializes R1's subtree as FRESH instances before any parameter
        // pass indexed the child — only its sibling-unique path can reanchor it and carry the expansion.
        var gate = new TaskCompletionSource<List<Item>>();
        Func<Item, Task<List<Item>>> loader = _ => gate.Task;

        List<Item> Initial() =>
        [
            new() { Text = "R1", Value = "r1", IsLeaf = false },
            new() { Text = "R2", Value = "r2", Children = [ new() { Text = "OtherShared", Value = "shared" } ] },
        ];

        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, Initial())
            .Add(c => c.LoadChildren, loader));

        // Expand R1 → load the "shared" child (which carries a static grandchild).
        var chevron1 = Row(cut, "R1").QuerySelector("button");
        Assert.NotNull(chevron1);
        var expand = cut.InvokeAsync(() => chevron1!.Click());
        await cut.InvokeAsync(() => gate.SetResult(
        [
            new()
            {
                Text = "SharedChild", Value = "shared",
                Children = [ new() { Text = "Grandchild", Value = "gc" } ]
            }
        ]));
        var expandDone = await Task.WhenAny(expand, Task.Delay(Timeout));
        Assert.True(ReferenceEquals(expandDone, expand), "lazy expand hung on the gated load");
        await expand;

        // Expand the loaded child so its expansion becomes tree-owned state (grandchild rendered).
        var chevron2 = Row(cut, "SharedChild").QuerySelector("button");
        Assert.NotNull(chevron2);
        await cut.InvokeAsync(() => chevron2!.Click());
        Assert.Equal("true", TreeItem(cut, "SharedChild").GetAttribute("aria-expanded"));
        Assert.Contains("Grandchild", cut.Markup);

        // Controlled rebuild: consumer re-supplies R1's loaded subtree as FRESH instances (so the
        // reattach-by-reference path is skipped), with "shared" still tree-wide duplicated. No parameter
        // pass has indexed the lazy child yet — its seeded path is the only remaining anchor.
        List<Item> Rebuilt() =>
        [
            new()
            {
                Text = "R1", Value = "r1", IsLeaf = false, ChildrenLoaded = true,
                Children =
                [
                    new()
                    {
                        Text = "SharedChild", Value = "shared",
                        Children = [ new() { Text = "Grandchild", Value = "gc" } ]
                    }
                ]
            },
            new() { Text = "R2", Value = "r2", Children = [ new() { Text = "OtherShared", Value = "shared" } ] },
        ];
        cut.Render(p => p
            .Add(c => c.Items, Rebuilt())
            .Add(c => c.LoadChildren, loader));

        // Reanchored by path → the tree-owned expansion carried onto the fresh instance. Pre-fix, the
        // fresh child minted a collapsed state and the grandchild vanished.
        Assert.Equal("true", TreeItem(cut, "SharedChild").GetAttribute("aria-expanded"));
        Assert.Contains("Grandchild", cut.Markup);
    }

    // ── Finding 2 ──────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Controlled_value_equal_echo_copy_keeps_single_identity_selection()
    {
        // Two nodes share a Value ("dup"). A controlled parent ACCEPTS the click but stores a value-equal
        // COPY (new List<>(incoming)) instead of the very reference — the idiomatic "keep my own mutable
        // list" pattern. That copy is an ECHO of the accepted selection, not a new authoritative seed:
        // re-resolving ["dup"] by value would select BOTH nodes. Only the clicked identity stays selected.
        var tree = new List<Item>
        {
            new() { Text = "Alpha", Value = "dup" },
            new() { Text = "Beta", Value = "dup" },
        };
        List<string>? bound = null;
        IRenderedComponent<L.TreeView<string>>? cut = null;

        var callback = EventCallback.Factory.Create<List<string>>(_ctx, (List<string> incoming) =>
        {
            bound = new List<string>(incoming); // COPY: fresh reference, identical values (the finding)
            cut!.Render(p =>
            {
                p.Add(c => c.SelectedValues, bound);
                p.Add(c => c.SelectedValuesChanged, EventCallback.Factory.Create<List<string>>(
                    _ctx, (List<string> v) => { bound = new List<string>(v); }));
            });
        });

        cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, tree)
            .Add(c => c.SelectedValues, bound)
            .Add(c => c.SelectedValuesChanged, callback));

        await cut.InvokeAsync(() => Row(cut, "Alpha").Click());

        var selected = Selected(cut);
        Assert.Single(selected);
        Assert.Contains("Alpha", selected[0].Children[0].TextContent);
        Assert.Equal("false", TreeItem(cut, "Beta").GetAttribute("aria-selected"));
    }

    // Counter-case guard: a value-DIFFERENT controlled list is still an authoritative re-seed, so the
    // echo relaxation does not swallow a genuine parent override.
    [Fact]
    public async Task Controlled_value_different_list_still_reseeds()
    {
        var tree = new List<Item>
        {
            new() { Text = "Alpha", Value = "a" },
            new() { Text = "Beta", Value = "b" },
        };
        List<string>? bound = null;
        IRenderedComponent<L.TreeView<string>>? cut = null;

        var callback = EventCallback.Factory.Create<List<string>>(_ctx, (List<string> _) =>
        {
            bound = new List<string> { "b" }; // parent VETOES the click and redirects to Beta
            cut!.Render(p =>
            {
                p.Add(c => c.SelectedValues, bound);
                p.Add(c => c.SelectedValuesChanged, EventCallback.Factory.Create<List<string>>(
                    _ctx, (List<string> _2) => { }));
            });
        });

        cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, tree)
            .Add(c => c.SelectedValues, bound)
            .Add(c => c.SelectedValuesChanged, callback));

        await cut.InvokeAsync(() => Row(cut, "Alpha").Click());

        var selected = Selected(cut);
        Assert.Single(selected);
        Assert.Contains("Beta", selected[0].Children[0].TextContent);
    }
}
