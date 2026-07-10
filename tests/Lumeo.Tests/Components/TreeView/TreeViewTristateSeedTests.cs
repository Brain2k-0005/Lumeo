using AngleSharp.Dom;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.TreeView;

/// <summary>
/// Tri-state DERIVE-ON-SEED regression: a tree that arrives with child nodes already
/// <c>IsChecked=true</c> must render its parents indeterminate / checked on the FIRST render
/// (and after an async Items reassignment or a lazy branch load) — not only after an
/// interactive check. Parent state is derived from children via the SAME recompute the
/// interactive path uses. Derivation is a CASCADE feature: with <c>CascadeCheck=false</c>
/// parent and child checks are independent, so no derivation runs.
/// </summary>
public class TreeViewTristateSeedTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TreeViewTristateSeedTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static IElement TreeItem(IRenderedComponent<L.TreeView<string>> cut, string text)
        => cut.FindAll("[role='treeitem']").First(el => el.Children[0].TextContent.Contains(text));

    /// <summary>The checkbox button inside a node's own label row (not a descendant's).</summary>
    private static IElement CheckboxOf(IRenderedComponent<L.TreeView<string>> cut, string text)
        => TreeItem(cut, text).Children[0].QuerySelector("button[role='checkbox']")!;

    private static string CheckState(IRenderedComponent<L.TreeView<string>> cut, string text)
        => CheckboxOf(cut, text).GetAttribute("aria-checked")!;

    // A parent (value-less, like a real folder) with one seeded-checked and one unchecked child.
    private static List<L.TreeView<string>.TreeViewItem<string>> PartiallyChecked() =>
    [
        new()
        {
            Text = "Frontend", IsExpanded = true,
            Children =
            [
                new() { Text = "React", Value = "react" },
                new() { Text = "Blazor", Value = "blazor", IsChecked = true }
            ]
        }
    ];

    // ── FIRST-RENDER derivation ───────────────────────────────────────────────

    [Fact]
    public void Seeded_child_check_makes_parent_indeterminate_on_first_render()
    {
        var items = PartiallyChecked();
        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, items)
            .Add(c => c.ShowCheckboxes, true));

        // Derived on the FIRST render — no interaction.
        Assert.Equal("mixed", CheckState(cut, "Frontend"));
        Assert.True(items[0].IsIndeterminate);
        Assert.False(items[0].IsChecked);
        // Children keep their seeded values; the leaf's box is unchanged.
        Assert.Equal("true", CheckState(cut, "Blazor"));
        Assert.Equal("false", CheckState(cut, "React"));
    }

    [Fact]
    public void All_children_seeded_checked_makes_parent_checked_on_first_render()
    {
        var items = new List<L.TreeView<string>.TreeViewItem<string>>
        {
            new()
            {
                Text = "Frontend", IsExpanded = true,
                Children =
                [
                    new() { Text = "React", Value = "react", IsChecked = true },
                    new() { Text = "Blazor", Value = "blazor", IsChecked = true }
                ]
            }
        };

        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, items)
            .Add(c => c.ShowCheckboxes, true));

        Assert.Equal("true", CheckState(cut, "Frontend"));
        Assert.True(items[0].IsChecked);
        Assert.False(items[0].IsIndeterminate);
    }

    [Fact]
    public void Nested_seeded_check_bubbles_through_every_ancestor_level()
    {
        // A deep grandchild carries the only seeded check; BOTH ancestors must go mixed.
        var items = new List<L.TreeView<string>.TreeViewItem<string>>
        {
            new()
            {
                Text = "Root", IsExpanded = true,
                Children =
                [
                    new()
                    {
                        Text = "Branch", IsExpanded = true,
                        Children =
                        [
                            new() { Text = "Leaf", Value = "leaf", IsChecked = true },
                            new() { Text = "Other", Value = "other" }
                        ]
                    }
                ]
            }
        };

        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, items)
            .Add(c => c.ShowCheckboxes, true));

        Assert.Equal("mixed", CheckState(cut, "Branch"));
        Assert.Equal("mixed", CheckState(cut, "Root"));
    }

    // ── CascadeCheck=false contract: NO derivation ────────────────────────────

    [Fact]
    public void CascadeCheck_false_leaves_parent_unchanged_from_seeded_children()
    {
        var items = PartiallyChecked();
        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, items)
            .Add(c => c.ShowCheckboxes, true)
            .Add(c => c.CascadeCheck, false));

        // Independent checks: the parent stays exactly as seeded (unchecked), NOT derived.
        Assert.Equal("false", CheckState(cut, "Frontend"));
        Assert.False(items[0].IsIndeterminate);
        Assert.False(items[0].IsChecked);
        // The seeded child is untouched too.
        Assert.Equal("true", CheckState(cut, "Blazor"));
    }

    // ── async Items reassignment (data arrives after first render) ─────────────

    [Fact]
    public void Parent_derives_when_seeded_items_are_assigned_after_first_render()
    {
        // First render with no data (async load shape).
        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.ShowCheckboxes, true));
        Assert.Empty(cut.FindAll("[role='treeitem']"));

        var items = PartiallyChecked();
        cut.Render(p => p
            .Add(c => c.Items, items)
            .Add(c => c.ShowCheckboxes, true));

        Assert.Equal("mixed", CheckState(cut, "Frontend"));
        Assert.True(items[0].IsIndeterminate);
    }

    // ── lazy branch loads with checked children → parent updates ───────────────

    [Fact]
    public async Task Lazy_branch_with_seeded_checked_children_updates_parent_on_load()
    {
        var items = new List<L.TreeView<string>.TreeViewItem<string>>
        {
            new() { Text = "Group", Value = "group", IsLeaf = false }
        };
        Func<L.TreeView<string>.TreeViewItem<string>, Task<List<L.TreeView<string>.TreeViewItem<string>>>> loader = _ =>
            Task.FromResult(new List<L.TreeView<string>.TreeViewItem<string>>
            {
                new() { Text = "Child-A", Value = "a", IsLeaf = true, IsChecked = true },
                new() { Text = "Child-B", Value = "b", IsLeaf = true }
            });

        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, items)
            .Add(c => c.ShowCheckboxes, true)
            .Add(c => c.LoadChildren, loader));

        // Nothing loaded yet → the empty lazy parent is plainly unchecked.
        Assert.Equal("false", CheckState(cut, "Group"));

        // Expand via the chevron → children load (A checked, B not) → parent goes mixed.
        await cut.InvokeAsync(() => cut.Find("button[aria-label='Expand']").Click());

        Assert.True(items[0].ChildrenLoaded);
        Assert.Equal("mixed", CheckState(cut, "Group"));
        Assert.True(items[0].IsIndeterminate);
    }
}
