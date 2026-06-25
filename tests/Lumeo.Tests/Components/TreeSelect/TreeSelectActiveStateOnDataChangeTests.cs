using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.TreeSelect;

/// <summary>
/// Regression for the battle-test "state-on-data-change" finding: the roving
/// focus active node was held by object reference and silently snapped back to
/// the first node when Items was replaced with an equal-content but
/// reference-distinct list (e.g. an async refresh). The active node is now
/// anchored by stable identity (item.Value), so focus survives a same-content
/// Items replacement, mirroring how _expandedNodes already survives.
/// </summary>
public class TreeSelectActiveStateOnDataChangeTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public TreeSelectActiveStateOnDataChangeTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<L.Services.IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // Fresh instances every call so each Tree() is a distinct, equal-content list.
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

    private static IElement Panel(IRenderedComponent<L.TreeSelect> cut) => cut.Find("[role='tree']");

    private static string ActiveText(IRenderedComponent<L.TreeSelect> cut)
        => cut.Find("[role='treeitem'][tabindex='0']").TextContent;

    private static async Task Key(IRenderedComponent<L.TreeSelect> cut, string key)
        => await cut.InvokeAsync(() => Panel(cut).KeyDown(new KeyboardEventArgs { Key = key }));

    [Fact]
    public async Task Active_node_survives_equal_content_Items_replacement()
    {
        var cut = _ctx.Render<L.TreeSelect>(p => p.Add(c => c.Items, Tree()));
        cut.Find("button").Click();

        // Move the roving focus off the first node onto "Veg".
        await Key(cut, "ArrowDown");
        Assert.Contains("Veg", ActiveText(cut));

        // Async-style refresh: a brand-new list with identical content (same
        // Values, new instances). Without the by-Value anchor the active node
        // would reference-mismatch the new instances and snap back to "Fruit".
        cut.Render(p => p.Add(c => c.Items, Tree()));

        // Exactly one tabbable node, and it is still "Veg".
        Assert.Single(cut.FindAll("[role='treeitem'][tabindex='0']"));
        Assert.Contains("Veg", ActiveText(cut));
    }

    [Fact]
    public async Task Active_node_falls_back_to_first_when_its_value_is_gone()
    {
        var cut = _ctx.Render<L.TreeSelect>(p => p.Add(c => c.Items, Tree()));
        cut.Find("button").Click();

        // Active "Veg".
        await Key(cut, "ArrowDown");
        Assert.Contains("Veg", ActiveText(cut));

        // Replace with a tree where "veg" no longer exists.
        cut.Render(p => p.Add(c => c.Items, new List<L.TreeSelect.TreeSelectItem>
        {
            new() { Label = "Fruit", Value = "fruit" }
        }));

        // Gracefully re-seeds onto the first (and only) visible node.
        Assert.Single(cut.FindAll("[role='treeitem'][tabindex='0']"));
        Assert.Contains("Fruit", ActiveText(cut));
    }
}
