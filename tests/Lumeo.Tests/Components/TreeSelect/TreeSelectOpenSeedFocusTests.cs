using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.TreeSelect;

/// <summary>
/// Regression for the battle-test "keyboard-a11y" finding: opening the dropdown
/// did not place the roving focus on the currently-selected node, contradicting
/// the OpenDropdown comment's own contract ("Seed the roving focus on the first
/// selected node if one is visible, otherwise the first node"). EnsureFlatVisible
/// only ever seeded the first visible node, so a keyboard user always landed on
/// node 0 regardless of selection. OpenDropdown now calls SeedActiveFromSelection
/// to anchor the active node to the first VISIBLE selected value, and
/// CloseDropdown resets the active node so the NEXT open re-seeds deterministically
/// from the current selection instead of reusing the last session's node.
/// </summary>
public class TreeSelectOpenSeedFocusTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public TreeSelectOpenSeedFocusTests()
    {
        _ctx.AddLumeoServices();
        // Last registration wins — swap in the tracking interop so we can observe
        // the FocusElement target chosen when the dropdown opens.
        _ctx.Services.AddSingleton<L.Services.IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static List<L.TreeSelect.TreeSelectItem> Tree() =>
    [
        new()
        {
            Label = "Fruit", Value = "fruit",
            Children =
            [
                new() { Label = "Apple", Value = "apple" },
                new() { Label = "Banana", Value = "banana" }
            ]
        },
        new() { Label = "Veg", Value = "veg" }
    ];

    private static IElement Active(IRenderedComponent<L.TreeSelect> cut)
        => cut.Find("[role='treeitem'][tabindex='0']");

    private static string ActiveText(IRenderedComponent<L.TreeSelect> cut)
        => Active(cut).TextContent;

    [Fact]
    public void Opening_seeds_active_node_to_selected_top_level_value()
    {
        var cut = _ctx.Render<L.TreeSelect>(p =>
        {
            p.Add(c => c.Items, Tree());
            p.Add(c => c.Value, "veg");
        });

        cut.Find("button").Click();

        // Exactly one tabbable node, and it is the SELECTED "Veg" — not the first node
        // "Fruit". Without the seed-on-open fix this would be "Fruit". The interop focus
        // call fires in OpenDropdown's async continuation, so poll rather than asserting
        // synchronously (the markup seed is synchronous, the FocusElement push is not).
        cut.WaitForAssertion(() =>
        {
            Assert.Single(cut.FindAll("[role='treeitem'][tabindex='0']"));
            Assert.Contains("Veg", ActiveText(cut));
            Assert.Equal(Active(cut).GetAttribute("id"), _interop.FocusedElementIds.LastOrDefault());
        });
    }

    [Fact]
    public void Opening_seeds_active_node_to_selected_child_when_visible()
    {
        var cut = _ctx.Render<L.TreeSelect>(p =>
        {
            p.Add(c => c.Items, Tree());
            p.Add(c => c.ExpandAll, true); // "Apple" is visible because Fruit is expanded
            p.Add(c => c.Value, "apple");
        });

        cut.Find("button").Click();

        Assert.Single(cut.FindAll("[role='treeitem'][tabindex='0']"));
        Assert.Contains("Apple", ActiveText(cut));
    }

    [Fact]
    public void Opening_with_no_selection_still_defaults_to_first_node()
    {
        // The default (flat[0]) path must remain intact when nothing is selected.
        var cut = _ctx.Render<L.TreeSelect>(p => p.Add(c => c.Items, Tree()));

        cut.Find("button").Click();

        Assert.Single(cut.FindAll("[role='treeitem'][tabindex='0']"));
        Assert.Contains("Fruit", ActiveText(cut));
    }

    [Fact]
    public void Opening_with_a_selection_not_currently_visible_falls_back_to_first()
    {
        // "apple" is a collapsed child (no ExpandAll), so it is not visible on open;
        // per the triage the seed falls back to the first visible node.
        var cut = _ctx.Render<L.TreeSelect>(p =>
        {
            p.Add(c => c.Items, Tree());
            p.Add(c => c.Value, "apple");
        });

        cut.Find("button").Click();

        Assert.Single(cut.FindAll("[role='treeitem'][tabindex='0']"));
        Assert.Contains("Fruit", ActiveText(cut));
    }

    [Fact]
    public async Task Reopen_reseeds_from_selection_not_last_active_node()
    {
        var cut = _ctx.Render<L.TreeSelect>(p =>
        {
            p.Add(c => c.Items, Tree());
            p.Add(c => c.Value, "veg");
        });

        // First open: active is the selected "Veg". Move focus off it onto "Fruit".
        cut.Find("button").Click();
        cut.WaitForAssertion(() => Assert.Contains("Veg", ActiveText(cut)));
        await cut.InvokeAsync(() => cut.Find("[role='tree']").KeyDown(new KeyboardEventArgs { Key = "Home" }));
        cut.WaitForAssertion(() => Assert.Contains("Fruit", ActiveText(cut)));

        // Close, then reopen: the active node must re-seed from the selection
        // ("Veg") rather than reusing the last session's active node ("Fruit").
        cut.Find("button").Click(); // close
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[role='tree']")));
        cut.Find("button").Click(); // reopen

        cut.WaitForAssertion(() =>
        {
            Assert.Single(cut.FindAll("[role='treeitem'][tabindex='0']"));
            Assert.Contains("Veg", ActiveText(cut));
        });
    }

    [Fact]
    public void Opening_in_multiple_mode_seeds_active_to_a_selected_value()
    {
        var cut = _ctx.Render<L.TreeSelect>(p =>
        {
            p.Add(c => c.Items, Tree());
            p.Add(c => c.Multiple, true);
            p.Add(c => c.Values, new List<string> { "veg" });
        });

        cut.Find("button").Click();

        Assert.Single(cut.FindAll("[role='treeitem'][tabindex='0']"));
        Assert.Contains("Veg", ActiveText(cut));
    }
}
