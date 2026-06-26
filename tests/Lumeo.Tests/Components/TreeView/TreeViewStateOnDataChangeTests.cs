using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.TreeView;

/// <summary>
/// state-on-data-change regressions: internal UI state (roving keyboard focus
/// and internally-clicked selection) must SURVIVE a same-content Items refresh
/// or an unrelated [Parameter] change.
///
/// #13: roving tabindex (_activeItem) was held by object reference, so an Items
/// refresh with new (value-equal but reference-distinct) instances dropped the
/// active node out of the flat list and reset focus to the first node.
///
/// #14: SelectedValues was copied into _selectedValues on EVERY OnParametersSet,
/// so any unrelated re-render wiped an internally-clicked selection and re-seeded
/// the stale parameter snapshot.
/// </summary>
public class TreeViewStateOnDataChangeTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TreeViewStateOnDataChangeTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // Fresh instances each call: a refresh of equal CONTENT but distinct REFERENCES,
    // exactly the async-reload shape the bugs are about.
    private static List<L.TreeView<string>.TreeViewItem<string>> Tree() =>
    [
        new()
        {
            Text = "Documents", Value = "docs", IsExpanded = true,
            Children =
            [
                new() { Text = "Resume", Value = "resume" },
                new() { Text = "Cover Letter", Value = "cover" }
            ]
        },
        new() { Text = "Images", Value = "imgs" }
    ];

    private static IElement TreeItem(IRenderedComponent<L.TreeView<string>> cut, string text)
        => cut.FindAll("[role='treeitem']").First(el => el.Children[0].TextContent.Contains(text));

    private static IElement Row(IRenderedComponent<L.TreeView<string>> cut, string text)
        => TreeItem(cut, text).Children[0];

    /// <summary>The single node currently holding the roving tabindex.</summary>
    private static string ActiveText(IRenderedComponent<L.TreeView<string>> cut)
        => cut.Find("[role='treeitem'][tabindex='0']").Children[0].TextContent;

    private static async Task Key(IRenderedComponent<L.TreeView<string>> cut, string key)
        => await cut.InvokeAsync(() =>
            cut.Find("[role='treeitem'][tabindex='0']").KeyDown(new KeyboardEventArgs { Key = key }));

    // ---- #13: roving keyboard focus survives a same-content Items refresh ----

    [Fact]
    public async Task RovingFocus_survives_same_content_Items_refresh()
    {
        var cut = _ctx.Render<L.TreeView<string>>(p => p.Add(c => c.Items, Tree()));

        // Move the active node off the first node.
        await Key(cut, "ArrowDown");
        Assert.Contains("Resume", ActiveText(cut));

        // Parent re-supplies an equal-content tree with brand-new instances
        // (e.g. an async reload). The active node must stay on "Resume".
        cut.Render(p => p.Add(c => c.Items, Tree()));

        Assert.Contains("Resume", ActiveText(cut));
        Assert.Single(cut.FindAll("[role='treeitem'][tabindex='0']"));
    }

    // ---- #14: internally-clicked selection survives an unrelated re-render ----

    [Fact]
    public async Task Internal_selection_survives_unrelated_parameter_change()
    {
        // SelectedValues supplied once (initial seed), then never kept in sync.
        var seed = new List<string> { "docs" };
        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, Tree())
            .Add(c => c.SelectedValues, seed));

        // User clicks a different node — internal selection moves to "Images".
        await cut.InvokeAsync(() => Row(cut, "Images").Click());
        Assert.Equal("true", TreeItem(cut, "Images").GetAttribute("aria-selected"));
        Assert.Equal("false", TreeItem(cut, "Documents").GetAttribute("aria-selected"));

        // An UNRELATED parameter change re-renders the tree without touching
        // SelectedValues (same reference). The user's selection must persist
        // and NOT snap back to the stale "docs" seed.
        cut.Render(p => p
            .Add(c => c.Items, Tree())
            .Add(c => c.SelectedValues, seed)
            .Add(c => c.ShowSearch, true));

        var selected = cut.FindAll("[role='treeitem'][aria-selected='true']");
        Assert.Single(selected);
        Assert.Contains("Images", selected[0].Children[0].TextContent);
    }

    [Fact]
    public async Task New_SelectedValues_reference_still_reseeds_selection()
    {
        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, Tree())
            .Add(c => c.SelectedValues, new List<string> { "docs" }));

        await cut.InvokeAsync(() => Row(cut, "Images").Click());
        Assert.Equal("true", TreeItem(cut, "Images").GetAttribute("aria-selected"));

        // A genuinely NEW SelectedValues reference is still honored (controlled
        // override is not broken by the #14 reference-compare guard).
        cut.Render(p => p.Add(c => c.SelectedValues, new List<string> { "resume" }));

        Assert.Equal("true", TreeItem(cut, "Resume").GetAttribute("aria-selected"));
        Assert.Equal("false", TreeItem(cut, "Images").GetAttribute("aria-selected"));
    }
}
