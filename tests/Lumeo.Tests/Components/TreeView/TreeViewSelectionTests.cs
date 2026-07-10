using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.TreeView;

/// <summary>
/// Regression tests for the <c>SelectedValues</c>/<c>SelectedValuesChanged</c>
/// two-way binding: clicking (or pressing Enter on) a node must mutate the
/// selection and raise <c>SelectedValuesChanged</c> — single-select replaces,
/// <c>MultiSelect</c> toggles. Previously the selection pipeline was inert.
/// </summary>
public class TreeViewSelectionTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TreeViewSelectionTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

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

    /// <summary>The treeitem element whose own label row contains <paramref name="text"/>.</summary>
    private static IElement TreeItem(IRenderedComponent<L.TreeView<string>> cut, string text)
        => cut.FindAll("[role='treeitem']").First(el => el.Children[0].TextContent.Contains(text));

    /// <summary>The clickable label row of a node.</summary>
    private static IElement Row(IRenderedComponent<L.TreeView<string>> cut, string text)
        => TreeItem(cut, text).Children[0];

    [Fact]
    public async Task Click_fires_SelectedValuesChanged_and_marks_node_selected()
    {
        List<string>? captured = null;
        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, Tree())
            .Add(c => c.SelectedValuesChanged, v => captured = v));

        await cut.InvokeAsync(() => Row(cut, "Images").Click());

        Assert.NotNull(captured);
        Assert.Equal(new[] { "imgs" }, captured);

        var selected = cut.FindAll("[role='treeitem'][aria-selected='true']");
        Assert.Single(selected);
        Assert.Contains("Images", selected[0].Children[0].TextContent);
    }

    [Fact]
    public async Task SingleSelect_click_replaces_previous_selection()
    {
        var events = new List<List<string>>();
        // Opt out of row-click expansion so clicking the pre-expanded "Documents" parent does not
        // collapse it (which would hide "Resume"); this test targets the replace-selection
        // semantic, exercised identically in both modes.
        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, Tree())
            .Add(c => c.ExpandOnRowClick, false)
            .Add(c => c.SelectedValuesChanged, v => events.Add(v)));

        await cut.InvokeAsync(() => Row(cut, "Documents").Click());
        await cut.InvokeAsync(() => Row(cut, "Resume").Click());

        Assert.Equal(2, events.Count);
        Assert.Equal(new[] { "docs" }, events[0]);
        Assert.Equal(new[] { "resume" }, events[1]);
        Assert.Single(cut.FindAll("[role='treeitem'][aria-selected='true']"));
    }

    [Fact]
    public async Task SingleSelect_reclicking_selected_node_does_not_refire()
    {
        var events = new List<List<string>>();
        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, Tree())
            .Add(c => c.SelectedValuesChanged, v => events.Add(v)));

        await cut.InvokeAsync(() => Row(cut, "Images").Click());
        await cut.InvokeAsync(() => Row(cut, "Images").Click());

        Assert.Single(events);
        Assert.Single(cut.FindAll("[role='treeitem'][aria-selected='true']"));
    }

    [Fact]
    public async Task MultiSelect_click_toggles_membership()
    {
        var events = new List<List<string>>();
        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, Tree())
            .Add(c => c.MultiSelect, true)
            .Add(c => c.SelectedValuesChanged, v => events.Add(v)));

        await cut.InvokeAsync(() => Row(cut, "Documents").Click());
        await cut.InvokeAsync(() => Row(cut, "Images").Click());

        Assert.Equal(2, events.Count);
        Assert.Equal(new[] { "docs" }, events[0]);
        Assert.Equal(new HashSet<string> { "docs", "imgs" }, events[1].ToHashSet());
        Assert.Equal(2, cut.FindAll("[role='treeitem'][aria-selected='true']").Count);

        // Clicking a selected node deselects it — and still fires the callback.
        await cut.InvokeAsync(() => Row(cut, "Documents").Click());
        Assert.Equal(3, events.Count);
        Assert.Equal(new[] { "imgs" }, events[2]);
        Assert.Single(cut.FindAll("[role='treeitem'][aria-selected='true']"));
    }

    [Fact]
    public async Task Enter_selects_node_when_checkboxes_are_off()
    {
        List<string>? captured = null;
        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, Tree())
            .Add(c => c.SelectedValuesChanged, v => captured = v));

        await cut.InvokeAsync(() => TreeItem(cut, "Images").KeyDown(new KeyboardEventArgs { Key = "Enter" }));

        Assert.Equal(new[] { "imgs" }, captured);
        Assert.Equal("true", TreeItem(cut, "Images").GetAttribute("aria-selected"));
    }

    [Fact]
    public async Task Click_still_fires_OnItemClick_exactly_once()
    {
        var clicks = new List<string?>();
        List<string>? captured = null;
        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, Tree())
            .Add(c => c.OnItemClick, (L.TreeView<string>.TreeViewItem<string> i) => clicks.Add(i.Value))
            .Add(c => c.SelectedValuesChanged, v => captured = v));

        await cut.InvokeAsync(() => Row(cut, "Images").Click());

        Assert.Equal(new string?[] { "imgs" }, clicks);
        Assert.Equal(new[] { "imgs" }, captured);
    }
}
