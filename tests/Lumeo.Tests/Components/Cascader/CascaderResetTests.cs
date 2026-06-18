using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Cascader;

/// <summary>
/// #199 — resetting <see cref="L.Cascader.Value"/> to null must clear the
/// internal selected-path highlight and active drill-down panels. Previously
/// the rebuild in OnParametersSet was guarded behind a "Count &gt; 0" check, so
/// a null/empty reset skipped the clear and the old highlighted path lingered
/// (visible the next time the dropdown opened).
/// </summary>
public class CascaderResetTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public CascaderResetTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static List<L.Cascader.CascaderOption> BuildOptions() =>
    [
        new()
        {
            Label = "Fruit", Value = "fruit",
            Children =
            [
                new() { Label = "Apple", Value = "apple" },
                new() { Label = "Banana", Value = "banana" },
            ],
        },
        new()
        {
            Label = "Veg", Value = "veg",
            Children = [ new() { Label = "Carrot", Value = "carrot" } ],
        },
    ];

    private IRenderedComponent<L.Cascader> Render(List<string>? value)
        => _ctx.Render<L.Cascader>(p => p
            .Add(c => c.Options, BuildOptions())
            .Add(c => c.Value, value));

    // The selected option carries the standalone "bg-accent" token
    // (GetOptionClass(isSelected:true)); unselected rows only ever carry
    // "hover:bg-accent/50", a different token, so ClassList.Contains is exact.
    private static int SelectedOptionCount(IRenderedComponent<L.Cascader> cut)
        => cut.FindAll("button").Count(b => b.ClassList.Contains("bg-accent"));

    [Fact]
    public void Selected_path_highlights_options_when_value_set()
    {
        var cut = Render(["fruit", "apple"]);
        // Open the dropdown.
        cut.Find("button").Click();

        // Both levels of the selected path are highlighted (exactly two).
        Assert.Equal(2, SelectedOptionCount(cut));
    }

    [Fact]
    public void Resetting_value_to_null_clears_the_highlight()
    {
        var cut = Render(["fruit", "apple"]);
        cut.Find("button").Click(); // open the dropdown
        Assert.True(SelectedOptionCount(cut) >= 1);

        // Reset Value to null externally — the dropdown stays open. The highlight
        // and the drill-down child panel must clear immediately (#199).
        cut.Render(p => p.Add(c => c.Value, (List<string>?)null));

        Assert.Equal(0, SelectedOptionCount(cut));
        // The child panel collapsed: only the two root options remain (no "Apple").
        Assert.DoesNotContain(cut.FindAll("button"), b => b.TextContent.Trim() == "Apple");
        var roots = cut.FindAll("button").Where(b => b.TextContent.Trim() is "Fruit" or "Veg").ToList();
        Assert.Equal(2, roots.Count);
    }

    [Fact]
    public void Resetting_value_to_empty_list_clears_the_highlight()
    {
        var cut = Render(["veg", "carrot"]);
        cut.Find("button").Click();
        Assert.True(SelectedOptionCount(cut) >= 1);

        cut.Render(p => p.Add(c => c.Value, new List<string>()));
        Assert.Equal(0, SelectedOptionCount(cut));
    }
}
