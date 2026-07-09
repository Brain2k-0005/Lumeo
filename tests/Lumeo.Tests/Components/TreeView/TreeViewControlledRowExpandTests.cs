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
}
