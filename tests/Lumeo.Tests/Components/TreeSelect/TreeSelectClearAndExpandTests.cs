using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.TreeSelect;

/// <summary>
/// Clearable affordance, the Multiple-mode parent-selection fix, and the
/// ExpandAll-applies-to-late-arriving-Items fix (the original only expanded in
/// OnInitialized, so async/replaced Items never auto-expanded).
/// </summary>
public class TreeSelectClearAndExpandTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TreeSelectClearAndExpandTests() => _ctx.AddLumeoServices();
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
        }
    ];

    [Fact]
    public void Clearable_shows_clear_button_only_when_a_value_is_selected()
    {
        var cut = _ctx.Render<L.TreeSelect>(p => p
            .Add(c => c.Items, Tree())
            .Add(c => c.Clearable, true)
            .Add(c => c.Value, "apple"));

        var clear = cut.FindAll("[aria-label]").Where(e => e.GetAttribute("aria-label") == "Clear selection").ToList();
        Assert.Single(clear);
    }

    [Fact]
    public void Clearable_hides_clear_button_when_nothing_selected()
    {
        var cut = _ctx.Render<L.TreeSelect>(p => p
            .Add(c => c.Items, Tree())
            .Add(c => c.Clearable, true));

        Assert.DoesNotContain("Clear selection", cut.Markup);
    }

    [Fact]
    public void Clear_button_resets_single_value()
    {
        string? value = "apple";
        var cut = _ctx.Render<L.TreeSelect>(p => p
            .Add(c => c.Items, Tree())
            .Add(c => c.Clearable, true)
            .Add(c => c.Value, "apple")
            .Add(c => c.ValueChanged,
                Microsoft.AspNetCore.Components.EventCallback.Factory.Create<string>(this, v => value = v)));

        cut.Find("[aria-label='Clear selection']").Click();

        Assert.True(string.IsNullOrEmpty(value));
    }

    [Fact]
    public void Clear_button_resets_multiple_values()
    {
        List<string>? values = new() { "apple" };
        var cut = _ctx.Render<L.TreeSelect>(p => p
            .Add(c => c.Items, Tree())
            .Add(c => c.Multiple, true)
            .Add(c => c.Clearable, true)
            .Add(c => c.Values, new List<string> { "apple" })
            .Add(c => c.ValuesChanged,
                Microsoft.AspNetCore.Components.EventCallback.Factory.Create<List<string>?>(this, v => values = v)));

        cut.Find("[aria-label='Clear selection']").Click();

        Assert.True(values is null || values.Count == 0);
    }

    [Fact]
    public void ExpandAll_applies_to_items_supplied_after_first_render()
    {
        // Mount with ExpandAll=true but EMPTY items (simulates async load), then
        // supply the real tree — the parent must now be expanded.
        var cut = _ctx.Render<L.TreeSelect>(p => p
            .Add(c => c.Items, new List<L.TreeSelect.TreeSelectItem>())
            .Add(c => c.ExpandAll, true));
        cut.Find("button").Click();

        // No items yet.
        Assert.DoesNotContain("Apple", cut.Markup);

        // Late-arriving items.
        cut.Render(p => p.Add(c => c.Items, Tree()));

        // Children visible because ExpandAll re-applied on the new Items reference.
        Assert.Contains("Apple", cut.Markup);
        Assert.Contains("Banana", cut.Markup);
    }

    [Fact]
    public async Task Multiple_mode_clicking_parent_row_toggles_parent_value()
    {
        List<string>? values = null;
        var cut = _ctx.Render<L.TreeSelect>(p => p
            .Add(c => c.Items, Tree())
            .Add(c => c.Multiple, true)
            .Add(c => c.ValuesChanged,
                Microsoft.AspNetCore.Components.EventCallback.Factory.Create<List<string>?>(this, v => values = v)));
        await cut.InvokeAsync(() => cut.Find("button").Click());

        // Click the parent's label (bubbles to the row's onclick). The chevron is
        // a separate hit target that only expands; the label/row selects.
        // Re-find inside InvokeAsync so the handler id matches the live render tree.
        await cut.InvokeAsync(() =>
            cut.FindAll("[role='treeitem'] span").First(e => e.TextContent.Contains("Fruit")).Click());

        Assert.NotNull(values);
        Assert.Contains("fruit", values!);
    }
}
