using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.TreeView;

using Item = L.TreeView<string>.TreeViewItem<string>;

/// <summary>
/// PR #351 round-4, finding 2: on a row click that both selects AND expands, the expansion must
/// be applied BEFORE the selection callbacks run. A controlled/immutable consumer rebuilds Items
/// inside SelectedValuesChanged; because each node is <c>@key</c>'d by its instance, that rebuild
/// disposes the clicked node's component. A ToggleExpand deferred until after the awaited callbacks
/// would mutate the stale/disposed instance and the new node would never expand.
/// </summary>
public class TreeViewControlledRowExpandTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TreeViewControlledRowExpandTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static IElement TreeItem(IRenderedComponent<L.TreeView<string>> cut, string text)
        => cut.FindAll("[role='treeitem']").First(el => el.Children[0].TextContent.Contains(text));

    private static IElement Row(IRenderedComponent<L.TreeView<string>> cut, string text)
        => TreeItem(cut, text).Children[0];

    [Fact]
    public async Task Row_click_expands_the_new_node_when_the_parent_rebuilds_Items_in_SelectedValuesChanged()
    {
        IRenderedComponent<L.TreeView<string>>? cut = null;

        // The controlled parent's source of truth. On every selection change it REBUILDS Items
        // with fresh node instances (a new @key each → the clicked node's component is disposed),
        // carrying each node's expansion forward by copying IsExpanded from the live tree. If the
        // row click toggled expansion only AFTER awaiting SelectedValuesChanged, this rebuild
        // would snapshot the STILL-collapsed node and the deferred toggle would land on the
        // disposed instance — so the new node would never expand.
        List<Item> current = null!;
        List<Item> Build(bool expanded) =>
        [
            new()
            {
                Text = "Music", Value = "music", IsExpanded = expanded,
                Children = [new() { Text = "playlist.m3u", Value = "playlist" }]
            }
        ];

        var callback = EventCallback.Factory.Create<List<string>>(_ctx, (List<string> _) =>
        {
            // Immutable rebuild: fresh instances, expansion carried forward from the live node.
            current = Build(current[0].IsExpanded);
            cut!.Render(p => p.Add(c => c.Items, current));
        });

        current = Build(false);
        cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, current)
            .Add(c => c.SelectedValues, (List<string>?)null)
            .Add(c => c.SelectedValuesChanged, callback));

        Assert.Equal("false", TreeItem(cut, "Music").GetAttribute("aria-expanded"));
        Assert.DoesNotContain("playlist.m3u", cut.Markup);

        await cut.InvokeAsync(() => Row(cut, "Music").Click());

        // The rebuilt node is both selected and expanded — the toggle ran before the rebuild.
        Assert.Equal("true", TreeItem(cut, "Music").GetAttribute("aria-selected"));
        Assert.Equal("true", TreeItem(cut, "Music").GetAttribute("aria-expanded"));
        Assert.Contains("playlist.m3u", cut.Markup);
    }

    /// <summary>
    /// PR #351 round-8: a row click on a LAZY parent both selects it (→ the controlled parent
    /// rebuilds Items in SelectedValuesChanged, carrying the optimistic IsExpanded onto a FRESH
    /// @key'd instance) and starts the lazy load. If that load then FAILS — after the rebuild has
    /// already replaced the clicked instance — the round-7 rollback collapsed only the STALE
    /// instance, so the visible fresh node stayed aria-expanded=true over an empty branch and the
    /// next click collapsed it instead of retrying. The rollback must reach the CURRENT rendered
    /// node: the row collapses, and because ChildrenLoaded stayed false a second click retries.
    /// </summary>
    [Fact]
    public async Task Failed_lazy_row_click_rolls_back_the_fresh_node_after_a_controlled_rebuild()
    {
        IRenderedComponent<L.TreeView<string>>? cut = null;

        // First lazy load hangs then fails (resolved AFTER the controlled rebuild); the retry wins.
        var gate = new TaskCompletionSource<List<Item>>();
        var attempts = 0;
        Func<Item, Task<List<Item>>> loader = _ =>
        {
            attempts++;
            return attempts == 1
                ? gate.Task
                : Task.FromResult(new List<Item> { new() { Text = "Child-A", Value = "a", IsLeaf = true } });
        };

        // Controlled/immutable source of truth: a single LAZY parent (no children yet). Every
        // selection change rebuilds Items with a FRESH instance, carrying IsExpanded forward from
        // the live node — so the optimistic expansion lands on the fresh @key'd node.
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

        // Row click selects (→ rebuild carries IsExpanded=true onto the fresh node) and starts the
        // lazy load, which hangs. The rebuilt node is optimistically expanded over an empty branch.
        await cut.InvokeAsync(() => Row(cut, "Music").Click());
        Assert.Equal("true", TreeItem(cut, "Music").GetAttribute("aria-expanded"));

        // The loader now FAILS — after the rebuild already replaced the clicked instance. The
        // rollback must collapse the CURRENT rendered node, not the stale one.
        await cut.InvokeAsync(() => gate.SetException(new InvalidOperationException("loader boom")));
        Assert.Equal("false", TreeItem(cut, "Music").GetAttribute("aria-expanded"));
        Assert.DoesNotContain("Child-A", cut.Markup);

        // Retry: ChildrenLoaded stayed false, so a second click re-runs the loader (now succeeds).
        await cut.InvokeAsync(() => Row(cut, "Music").Click());
        Assert.Equal(2, attempts);
        Assert.Contains("Child-A", cut.Markup);
        Assert.Equal("true", TreeItem(cut, "Music").GetAttribute("aria-expanded"));
    }

    /// <summary>
    /// PR #351 round-9 finding 1 (the SUCCESS mirror of the round-8 rollback above): a row click on a
    /// LAZY parent starts <c>LoadChildren</c> BEFORE the selection callbacks run. A controlled/
    /// immutable parent rebuilds Items inside SelectedValuesChanged — carrying the optimistic
    /// IsExpanded onto a FRESH <c>@key</c>'d instance — while the loader is still pending. When the
    /// load then RESOLVES, assigning the children to the STALE instance leaves the visible fresh node
    /// expanded-but-empty (no success path re-resolved it the way the failure rollback does). The load
    /// must re-resolve the CURRENT node by its saved path/value and attach the children to THAT, so the
    /// rendered node fills in.
    /// </summary>
    [Fact]
    public async Task Slow_lazy_row_click_attaches_children_to_the_fresh_node_after_a_controlled_rebuild()
    {
        IRenderedComponent<L.TreeView<string>>? cut = null;

        // The load HANGS until we release the gate — well after the controlled rebuild has already
        // replaced the clicked instance with a fresh @key'd node.
        var gate = new TaskCompletionSource<List<Item>>();
        Func<Item, Task<List<Item>>> loader = _ => gate.Task;

        // Controlled/immutable source of truth: a single LAZY parent. Every selection change rebuilds
        // Items with a FRESH instance, carrying IsExpanded forward from the live node.
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

        // Row click selects (→ rebuild carries IsExpanded=true onto the fresh node) and starts the
        // hanging load. The rebuilt node is optimistically expanded over an empty branch.
        await cut.InvokeAsync(() => Row(cut, "Music").Click());
        Assert.Equal("true", TreeItem(cut, "Music").GetAttribute("aria-expanded"));
        Assert.DoesNotContain("Child-A", cut.Markup);

        // The loader completes AFTER the rebuild replaced the clicked instance. The children must
        // attach to the CURRENT rendered (fresh) node — not the stale one — so they become visible.
        await cut.InvokeAsync(() =>
            gate.SetResult(new List<Item> { new() { Text = "Child-A", Value = "a", IsLeaf = true } }));

        Assert.Contains("Child-A", cut.Markup);
        Assert.Equal("true", TreeItem(cut, "Music").GetAttribute("aria-expanded"));
        // The parent (Music) is still the selected row after the whole sequence.
        Assert.Equal("true", TreeItem(cut, "Music").GetAttribute("aria-selected"));
    }
}
