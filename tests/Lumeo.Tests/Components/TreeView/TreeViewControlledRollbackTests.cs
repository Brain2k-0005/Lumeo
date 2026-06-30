using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.TreeView;

/// <summary>
/// Regression: when a controlled parent vetoes a selection (re-renders with the
/// same bound SelectedValues it held before the user's click), the component must
/// roll the UI back to the parent-owned selection instead of keeping the
/// optimistically-clicked (rejected) selection.
///
/// Bug: OnParametersSet only re-seeded _selectedValues when the incoming
/// SelectedValues reference differed from _previousSelectedValues (the last
/// reference the parent SUPPLIED). When a controlled parent vetoes by
/// re-rendering with the SAME SelectedValues reference it held before the click,
/// the comparison saw "no change" and skipped the resync, leaving the rejected
/// click's selection visible.
///
/// Fix: track _lastPushedSelectedValues (the list reference WE emitted via
/// SelectedValuesChanged). A controlled re-render that supplies anything other
/// than that exact reference is authoritative (veto / normalization / reset) and
/// must win; a re-render that echoes the pushed reference is a benign round-trip
/// and is ignored (keeps the in-flight selection).
/// </summary>
public class TreeViewControlledRollbackTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TreeViewControlledRollbackTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static List<L.TreeView<string>.TreeViewItem<string>> Tree() =>
    [
        new() { Text = "Documents", Value = "docs" },
        new() { Text = "Images", Value = "imgs" }
    ];

    /// <summary>The treeitem element whose own label row contains <paramref name="text"/>.</summary>
    private static IElement TreeItem(IRenderedComponent<L.TreeView<string>> cut, string text)
        => cut.FindAll("[role='treeitem']").First(el => el.Children[0].TextContent.Contains(text));

    /// <summary>The clickable label row of a node.</summary>
    private static IElement Row(IRenderedComponent<L.TreeView<string>> cut, string text)
        => TreeItem(cut, text).Children[0];

    [Fact]
    public async Task Controlled_Veto_From_Null_Rolls_Back_To_Nothing_Selected()
    {
        // Controlled parent holds SelectedValues = null and vetoes every change
        // (the callback never updates its own bound state).
        IRenderedComponent<L.TreeView<string>>? cut = null;

        var callback = EventCallback.Factory.Create<List<string>>(_ctx, (List<string> _) =>
        {
            // Veto: re-render with the SAME null SelectedValues.
            cut!.Render(p =>
            {
                p.Add(c => c.SelectedValues, (List<string>?)null);
                p.Add(c => c.SelectedValuesChanged, EventCallback.Factory.Create<List<string>>(_ctx, (List<string> _2) => { }));
            });
        });

        cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, Tree())
            .Add(c => c.SelectedValues, (List<string>?)null)
            .Add(c => c.SelectedValuesChanged, callback));

        Assert.Empty(cut.FindAll("[role='treeitem'][aria-selected='true']"));

        // User clicks "Images" — HandleSelectionChanged optimistically selects it
        // and pushes ["imgs"] via SelectedValuesChanged; the parent vetoes.
        await cut.InvokeAsync(() => Row(cut, "Images").Click());

        // After the veto the UI must roll back — nothing selected.
        Assert.Empty(cut.FindAll("[role='treeitem'][aria-selected='true']"));
    }

    [Fact]
    public async Task Controlled_Veto_From_Selected_Rolls_Back_To_Original_Selection()
    {
        // Controlled parent holds SelectedValues = ["docs"] and vetoes any change.
        // Reusing the same list reference simulates a parent that keeps its old binding.
        var originalSelection = new List<string> { "docs" };
        IRenderedComponent<L.TreeView<string>>? cut = null;

        var callback = EventCallback.Factory.Create<List<string>>(_ctx, (List<string> _) =>
        {
            cut!.Render(p =>
            {
                p.Add(c => c.SelectedValues, originalSelection);
                p.Add(c => c.SelectedValuesChanged, EventCallback.Factory.Create<List<string>>(_ctx, (List<string> _2) => { }));
            });
        });

        cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, Tree())
            .Add(c => c.SelectedValues, originalSelection)
            .Add(c => c.SelectedValuesChanged, callback));

        Assert.Equal("true", TreeItem(cut, "Documents").GetAttribute("aria-selected"));

        // User clicks "Images" — selection optimistically moves to "Images" and
        // pushes ["imgs"]; the parent vetoes and re-renders with ["docs"] again.
        await cut.InvokeAsync(() => Row(cut, "Images").Click());

        // "Documents" must snap back to selected; "Images" must NOT stay selected.
        Assert.Equal("true", TreeItem(cut, "Documents").GetAttribute("aria-selected"));
        Assert.Equal("false", TreeItem(cut, "Images").GetAttribute("aria-selected"));
    }

    [Fact]
    public async Task Controlled_Accepted_Selection_Keeps_New_Value()
    {
        // Guard against over-correction: when the parent ACCEPTS the click and
        // re-renders with the new SelectedValues we pushed, the new selection
        // must be shown, not rolled back.
        List<string>? boundSelection = null;
        IRenderedComponent<L.TreeView<string>>? cut = null;

        var callback = EventCallback.Factory.Create<List<string>>(_ctx, (List<string> incoming) =>
        {
            boundSelection = incoming;
            cut!.Render(p =>
            {
                p.Add(c => c.SelectedValues, boundSelection);
                p.Add(c => c.SelectedValuesChanged, EventCallback.Factory.Create<List<string>>(_ctx, (List<string> v) =>
                {
                    boundSelection = v;
                }));
            });
        });

        cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, Tree())
            .Add(c => c.SelectedValues, boundSelection)
            .Add(c => c.SelectedValuesChanged, callback));

        await cut.InvokeAsync(() => Row(cut, "Images").Click());

        Assert.Equal("true", TreeItem(cut, "Images").GetAttribute("aria-selected"));
        Assert.Equal("false", TreeItem(cut, "Documents").GetAttribute("aria-selected"));
    }
}
