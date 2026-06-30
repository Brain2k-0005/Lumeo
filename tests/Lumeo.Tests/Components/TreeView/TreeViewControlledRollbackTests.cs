using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.TreeView;

/// <summary>
/// Regression: a controlled parent must be able to override TreeView's selection, AND an
/// observer-only consumer (binds SelectedValuesChanged purely to react, without echoing
/// SelectedValues back) must not have its local click immediately snapped back by its own
/// re-render — the established Lumeo convention also tested for PickList's #38 and
/// SortableList's #144: a re-render that supplies the SAME reference the parent held BEFORE
/// the interaction is indistinguishable from "hasn't propagated yet", so it stays a no-op.
/// Only a value that differs from BOTH our own push AND that pre-interaction snapshot is an
/// authoritative, distinguishable decision (a genuine veto / normalization / reset).
///
/// Fix shape: track _lastPushedSelectedValues (what WE emitted) AND consult
/// _previousSelectedValues (what the parent held immediately before this interaction) in the
/// CONTROLLED branch — an echo of either is a no-op; anything else wins.
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
    public async Task Controlled_Unchanged_From_Null_Keeps_Local_Click_Selected()
    {
        // Controlled parent holds SelectedValues = null and never echoes it back — an
        // observer that reacts to selection without owning it (re-renders with the SAME
        // null it always had, not a deliberate decision).
        IRenderedComponent<L.TreeView<string>>? cut = null;

        var callback = EventCallback.Factory.Create<List<string>>(_ctx, (List<string> _) =>
        {
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

        await cut.InvokeAsync(() => Row(cut, "Images").Click());

        // The unchanged-from-before re-render must NOT clear the optimistic click.
        Assert.Equal("true", TreeItem(cut, "Images").GetAttribute("aria-selected"));
    }

    [Fact]
    public async Task Controlled_Unchanged_From_Selected_Keeps_Local_Click_Selected()
    {
        // Same as above, but the parent's unchanging baseline is a real (non-null) selection.
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

        await cut.InvokeAsync(() => Row(cut, "Images").Click());

        // The unchanged-from-before re-render must NOT snap the click back to Documents.
        Assert.Equal("true", TreeItem(cut, "Images").GetAttribute("aria-selected"));
    }

    [Fact]
    public async Task Controlled_Veto_With_Distinct_Selection_Rolls_Back_To_That_Selection()
    {
        // A GENUINE, distinguishable veto: the parent's handler explicitly asserts a DIFFERENT
        // selection than both what the user clicked AND what it held before — e.g. a server-side
        // validation rule that redirects selection elsewhere. Unlike the pre-interaction-baseline
        // case above, this is unambiguous and must win.
        var before = new List<string> { "docs" };
        var redirected = new List<string> { "imgs" }; // a NEW reference, distinct from `before`
        IRenderedComponent<L.TreeView<string>>? cut = null;

        // Click target is "Documents" so the optimistic push differs from BOTH `before` (docs) and
        // the veto's redirect target... use a tree with a third node so all three values differ.
        var threeNode = new List<L.TreeView<string>.TreeViewItem<string>>
        {
            new() { Text = "Documents", Value = "docs" },
            new() { Text = "Images", Value = "imgs" },
            new() { Text = "Videos", Value = "vids" },
        };

        var callback = EventCallback.Factory.Create<List<string>>(_ctx, (List<string> _) =>
        {
            cut!.Render(p =>
            {
                p.Add(c => c.Items, threeNode);
                p.Add(c => c.SelectedValues, redirected); // redirects to "imgs", not "vids" (clicked) or "docs" (before)
                p.Add(c => c.SelectedValuesChanged, EventCallback.Factory.Create<List<string>>(_ctx, (List<string> _2) => { }));
            });
        });

        cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, threeNode)
            .Add(c => c.SelectedValues, before)
            .Add(c => c.SelectedValuesChanged, callback));

        Assert.Equal("true", TreeItem(cut, "Documents").GetAttribute("aria-selected"));

        // User clicks "Videos" — pushes ["vids"]; the parent's distinct redirect to ["imgs"] wins.
        await cut.InvokeAsync(() => Row(cut, "Videos").Click());

        Assert.Equal("true", TreeItem(cut, "Images").GetAttribute("aria-selected"));
        Assert.Equal("false", TreeItem(cut, "Videos").GetAttribute("aria-selected"));
        Assert.Equal("false", TreeItem(cut, "Documents").GetAttribute("aria-selected"));
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
