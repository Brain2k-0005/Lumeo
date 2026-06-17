using AngleSharp.Dom;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.TreeView;

/// <summary>
/// Regression tests for check-state integrity under an active search filter —
/// the filtered view must mutate the REAL items (previously it rendered clones,
/// so checks were lost when the search cleared) — and for <c>ExpandAll</c>
/// semantics with asynchronously-assigned <c>Items</c> / unrelated re-renders.
/// </summary>
public class TreeViewCheckAndExpandTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TreeViewCheckAndExpandTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static List<L.TreeView<string>.TreeViewItem<string>> Tree() =>
    [
        new()
        {
            Text = "Root", Value = "root",
            Children =
            [
                new() { Text = "Alpha", Value = "alpha" },
                new() { Text = "Beta",  Value = "beta"  }
            ]
        }
    ];

    private static IElement TreeItem(IRenderedComponent<L.TreeView<string>> cut, string text)
        => cut.FindAll("[role='treeitem']").First(el => el.Children[0].TextContent.Contains(text));

    /// <summary>The checkbox button inside a node's own label row.</summary>
    private static IElement CheckboxOf(IRenderedComponent<L.TreeView<string>> cut, string text)
        => TreeItem(cut, text).Children[0].QuerySelector("button[role='checkbox']")!;

    // ---------------------------------------------------------------------
    // Check state under an active search filter
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Check_in_filtered_view_mutates_real_item_and_reports_fresh_values()
    {
        var items = Tree();
        List<string?>? lastChecked = null;
        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, items)
            .Add(c => c.ShowSearch, true)
            .Add(c => c.ShowCheckboxes, true)
            .Add(c => c.CheckedValuesChanged, v => lastChecked = v));

        cut.Find("input[type='text']").Input("Alpha");
        Assert.Contains("Alpha", cut.Markup);
        Assert.DoesNotContain("Beta", cut.Markup);

        await cut.InvokeAsync(() => CheckboxOf(cut, "Alpha").Click());

        // The real item was mutated and the callback saw the fresh state.
        Assert.True(items[0].Children![0].IsChecked);
        Assert.NotNull(lastChecked);
        Assert.Contains("alpha", lastChecked);
        // Root reflects its real children: one of two checked → indeterminate.
        Assert.Equal("mixed", CheckboxOf(cut, "Root").GetAttribute("aria-checked"));
    }

    [Fact]
    public async Task Check_in_filtered_view_survives_clearing_the_search()
    {
        var items = Tree();
        List<string?>? lastChecked = null;
        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, items)
            .Add(c => c.ShowSearch, true)
            .Add(c => c.ShowCheckboxes, true)
            .Add(c => c.CheckedValuesChanged, v => lastChecked = v));

        cut.Find("input[type='text']").Input("Alpha");
        await cut.InvokeAsync(() => CheckboxOf(cut, "Alpha").Click());

        // Clear the search — the check must survive.
        cut.Find("input[type='text']").Input("");

        Assert.True(items[0].Children![0].IsChecked);
        Assert.Equal("mixed", CheckboxOf(cut, "Root").GetAttribute("aria-checked"));
        Assert.Equal(new[] { "alpha" }, lastChecked);

        // Expand Root and confirm Alpha renders checked.
        await cut.InvokeAsync(() => cut.Find("button[aria-label='Expand']").Click());
        Assert.Equal("true", CheckboxOf(cut, "Alpha").GetAttribute("aria-checked"));
        Assert.Equal("false", CheckboxOf(cut, "Beta").GetAttribute("aria-checked"));
    }

    [Fact]
    public async Task Checking_parent_in_filtered_view_cascades_to_real_children()
    {
        var items = Tree();
        List<string?>? lastChecked = null;
        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, items)
            .Add(c => c.ShowSearch, true)
            .Add(c => c.ShowCheckboxes, true)
            .Add(c => c.CheckedValuesChanged, v => lastChecked = v));

        // Filter so only Alpha is visible under Root, then check Root.
        cut.Find("input[type='text']").Input("Alpha");
        await cut.InvokeAsync(() => CheckboxOf(cut, "Root").Click());

        // Cascade hit the REAL children — including filtered-out Beta.
        Assert.True(items[0].IsChecked);
        Assert.True(items[0].Children![0].IsChecked);
        Assert.True(items[0].Children![1].IsChecked);
        Assert.NotNull(lastChecked);
        Assert.Equal(new HashSet<string?> { "root", "alpha", "beta" }, lastChecked.ToHashSet());
    }

    // ---------------------------------------------------------------------
    // ExpandAll semantics
    // ---------------------------------------------------------------------

    [Fact]
    public void ExpandAll_applies_to_items_assigned_after_first_render()
    {
        // Async data: the tree renders before Items arrive.
        var cut = _ctx.Render<L.TreeView<string>>(p => p.Add(c => c.ExpandAll, true));
        Assert.Empty(cut.FindAll("[role='treeitem']"));

        var items = Tree();
        cut.Render(p => p.Add(c => c.Items, items));

        Assert.True(items[0].IsExpanded);
        Assert.Equal("true", TreeItem(cut, "Root").GetAttribute("aria-expanded"));
        Assert.Contains("Alpha", cut.Markup);
        Assert.Contains("Beta", cut.Markup);
    }

    [Fact]
    public async Task ExpandAll_does_not_stomp_manual_collapse_on_unrelated_rerender()
    {
        var items = Tree();
        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, items)
            .Add(c => c.ExpandAll, true));
        Assert.True(items[0].IsExpanded);

        // User collapses Root via its chevron.
        await cut.InvokeAsync(() => cut.Find("button[aria-label='Collapse']").Click());
        Assert.False(items[0].IsExpanded);

        // An unrelated re-render (same Items reference, same ExpandAll) must
        // not re-apply ExpandAll over the user's manual collapse.
        cut.Render(p => p.Add(c => c.Class, "noop"));

        Assert.False(items[0].IsExpanded);
        Assert.Equal("false", TreeItem(cut, "Root").GetAttribute("aria-expanded"));
    }

    // ---------------------------------------------------------------------
    // #201: ExpandAll honors lazy-loading (LoadChildren)
    // ---------------------------------------------------------------------

    // A single lazy root that resolves two children on first expand.
    private static List<L.TreeView<string>.TreeViewItem<string>> LazyRoot() =>
    [
        new() { Text = "Lazy", Value = "lazy", IsLeaf = false }
    ];

    private static Func<L.TreeView<string>.TreeViewItem<string>, Task<List<L.TreeView<string>.TreeViewItem<string>>>>
        Loader(Action? onCall = null) => _ =>
    {
        onCall?.Invoke();
        return Task.FromResult(new List<L.TreeView<string>.TreeViewItem<string>>
        {
            new() { Text = "Child-A", Value = "a", IsLeaf = true },
            new() { Text = "Child-B", Value = "b", IsLeaf = true }
        });
    };

    [Fact]
    public async Task ExpandAllNodesAsync_loads_unloaded_lazy_children()
    {
        var items = LazyRoot();
        var calls = 0;
        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, items)
            .Add(c => c.LoadChildren, Loader(() => calls++)));

        // Nothing loaded yet.
        Assert.False(items[0].ChildrenLoaded);

        var tree = cut.Instance;
        await cut.InvokeAsync(() => tree.ExpandAllNodesAsync());

        Assert.Equal(1, calls);
        Assert.True(items[0].ChildrenLoaded);
        Assert.True(items[0].IsExpanded);
        Assert.Equal(2, items[0].Children!.Count);
        Assert.Contains("Child-A", cut.Markup);
        Assert.Contains("Child-B", cut.Markup);
    }

    [Fact]
    public void Declarative_ExpandAll_with_LoadChildren_loads_lazy_branch()
    {
        var items = LazyRoot();
        var calls = 0;

        // ExpandAll set declaratively at first render with a lazy loader.
        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, items)
            .Add(c => c.ExpandAll, true)
            .Add(c => c.LoadChildren, Loader(() => calls++)));

        // OnParametersSetAsync runs the lazy load during render.
        Assert.Equal(1, calls);
        Assert.True(items[0].ChildrenLoaded);
        Assert.Contains("Child-A", cut.Markup);
    }

    [Fact]
    public async Task ExpandAllNodesAsync_does_not_reload_already_loaded_children()
    {
        var items = LazyRoot();
        var calls = 0;
        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, items)
            .Add(c => c.LoadChildren, Loader(() => calls++)));

        var tree = cut.Instance;
        await cut.InvokeAsync(() => tree.ExpandAllNodesAsync());
        Assert.Equal(1, calls);

        // Second expand-all must not hit the loader again — already loaded.
        await cut.InvokeAsync(() => tree.ExpandAllNodesAsync());
        Assert.Equal(1, calls);
    }
}
