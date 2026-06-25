using AngleSharp.Dom;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Cascader;

/// <summary>
/// Battle-test #27 — the search filter only narrows the ROOT column (level 0);
/// deeper columns render verbatim from the live <c>_activePanels</c> drill
/// trail. If the user drills into a parent and THEN types a search that
/// excludes that parent from the filtered root, level 0 shows the filtered root
/// while level 1+ still shows the (now orphaned) children — the columns visibly
/// desync. The fix collapses the drill trail back to the filtered root on every
/// search-text change so the rendered columns stay consistent. (Without the
/// fix, the stale child column survives the search edit.)
/// </summary>
public class CascaderSearchTrailTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public CascaderSearchTrailTests() => _ctx.AddLumeoServices();
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
            Children = [new() { Label = "Carrot", Value = "carrot" }],
        },
    ];

    private IRenderedComponent<L.Cascader> Render()
        => _ctx.Render<L.Cascader>(p => p
            .Add(c => c.Options, BuildOptions())
            .Add(c => c.ShowSearch, true));

    private static IElement? OptionButton(IRenderedComponent<L.Cascader> cut, string label)
        => cut.FindAll("button").FirstOrDefault(b => b.TextContent.Trim() == label);

    private static IReadOnlyList<string> VisibleOptionLabels(IRenderedComponent<L.Cascader> cut)
        => cut.FindAll("button")
            .Select(b => b.TextContent.Trim())
            .Where(t => t is "Fruit" or "Veg" or "Apple" or "Banana" or "Carrot")
            .ToList();

    [Fact]
    public void Typing_a_search_collapses_a_stale_child_column()
    {
        var cut = Render();

        // Open and drill into the "Fruit" parent so the child column (Apple /
        // Banana) is rendered alongside the root.
        cut.Find("button").Click();
        OptionButton(cut, "Fruit")!.Click();
        Assert.Contains("Apple", VisibleOptionLabels(cut));

        // Type a search that EXCLUDES "Fruit" from the filtered root column.
        // Level 0 now matches only "Veg"; the orphaned "Apple"/"Banana" child
        // column must not linger.
        cut.Find("input[type=\"text\"]").Input("Veg");

        var labels = VisibleOptionLabels(cut);
        Assert.Contains("Veg", labels);
        // The stale child column from the (now filtered-out) "Fruit" parent is gone.
        Assert.DoesNotContain("Apple", labels);
        Assert.DoesNotContain("Banana", labels);
        // The root column is filtered to the single match — no leftover "Fruit".
        Assert.DoesNotContain("Fruit", labels);
    }

    [Fact]
    public void Search_resets_the_roving_focus_stop_to_the_first_filtered_option()
    {
        var cut = Render();

        cut.Find("button").Click();
        OptionButton(cut, "Fruit")!.Click(); // drill — moves the roving stop into column 1

        cut.Find("input[type=\"text\"]").Input("Veg");

        // After the search, exactly one option carries tabindex=0 and it is the
        // first option of the filtered root column ("Veg"), not a stale deeper
        // column index.
        var roving = cut.FindAll("button[tabindex=\"0\"]");
        Assert.Single(roving);
        Assert.Equal("Veg", roving[0].TextContent.Trim());
    }
}
