using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Cascader;

/// <summary>
/// Core cascading drill-down interaction: opening reveals the first column,
/// drilling into a parent reveals its children column, and selecting a leaf
/// fires Value/ValueChanged and closes the picker. The Cascader renders each
/// level as a vertical panel of option <c>&lt;button&gt;</c> elements; the
/// trigger is the first button in the wrapper. Parent options carry a
/// ChevronRight affordance and reveal a child panel on click; leaf options
/// commit the selection.
/// </summary>
public class CascaderBehaviorTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public CascaderBehaviorTests() => _ctx.AddLumeoServices();
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

    private IRenderedComponent<L.Cascader> Render(
        Action<ComponentParameterCollectionBuilder<L.Cascader>>? extra = null)
        => _ctx.Render<L.Cascader>(p =>
        {
            p.Add(c => c.Options, BuildOptions());
            extra?.Invoke(p);
        });

    // The trigger is the first button (the one that opens the panel). Helpers
    // below operate on the option buttons inside the panels by their visible
    // label so assertions stay independent of CSS class details.
    private static IElement? OptionButton(IRenderedComponent<L.Cascader> cut, string label)
        => cut.FindAll("button").FirstOrDefault(b => b.TextContent.Trim() == label);

    private static IReadOnlyList<string> VisibleOptionLabels(IRenderedComponent<L.Cascader> cut)
        => cut.FindAll("button")
            .Select(b => b.TextContent.Trim())
            .Where(t => t is "Fruit" or "Veg" or "Apple" or "Banana" or "Carrot")
            .ToList();

    [Fact]
    public void Opening_shows_only_the_first_column()
    {
        var cut = Render();

        // Closed: no option panels rendered yet.
        Assert.Empty(VisibleOptionLabels(cut));

        // Open the picker via the trigger button.
        cut.Find("button").Click();

        // Only the root-level options appear; no child column until a parent
        // is drilled into.
        var labels = VisibleOptionLabels(cut);
        Assert.Contains("Fruit", labels);
        Assert.Contains("Veg", labels);
        Assert.DoesNotContain("Apple", labels);
        Assert.DoesNotContain("Carrot", labels);
    }

    [Fact]
    public void Selecting_a_parent_reveals_its_children_column_without_committing()
    {
        List<string>? committed = null;
        var fired = 0;
        var cut = Render(p => p.Add(c => c.ValueChanged, (List<string>? v) =>
        {
            committed = v;
            fired++;
        }));

        cut.Find("button").Click();          // open
        OptionButton(cut, "Fruit")!.Click(); // drill into the "Fruit" parent

        // The child column for "Fruit" is now visible alongside the root.
        var labels = VisibleOptionLabels(cut);
        Assert.Contains("Apple", labels);
        Assert.Contains("Banana", labels);
        // The sibling parent's children stay hidden.
        Assert.DoesNotContain("Carrot", labels);

        // Drilling into a non-leaf parent must NOT commit a value.
        Assert.Equal(0, fired);
        Assert.Null(committed);
    }

    [Fact]
    public void Selecting_a_leaf_fires_value_changed_with_the_full_path()
    {
        List<string>? committed = null;
        var fired = 0;
        var cut = Render(p => p.Add(c => c.ValueChanged, (List<string>? v) =>
        {
            committed = v;
            fired++;
        }));

        cut.Find("button").Click();          // open
        OptionButton(cut, "Fruit")!.Click(); // reveal children
        OptionButton(cut, "Apple")!.Click(); // commit the leaf

        Assert.Equal(1, fired);
        Assert.NotNull(committed);
        Assert.Equal(new[] { "fruit", "apple" }, committed!);
    }

    [Fact]
    public void Committing_a_leaf_closes_the_picker_and_shows_the_path_label()
    {
        var cut = Render(p => p.Add(c => c.ValueChanged, (List<string>? _) => { }));

        cut.Find("button").Click();
        OptionButton(cut, "Veg")!.Click();    // parent
        OptionButton(cut, "Carrot")!.Click(); // leaf -> commit + close

        // Picker closed: no option panels left.
        Assert.Empty(VisibleOptionLabels(cut));

        // Trigger now shows the joined selection path "Veg / Carrot".
        Assert.Contains("Veg / Carrot", cut.Markup);
    }

    [Fact]
    public void Re_drilling_a_different_parent_replaces_the_child_column()
    {
        var cut = Render();

        cut.Find("button").Click();
        OptionButton(cut, "Fruit")!.Click();
        Assert.Contains("Apple", VisibleOptionLabels(cut));

        // Switch to the sibling parent: its children replace the previous
        // child column (the path is trimmed back to the new branch).
        OptionButton(cut, "Veg")!.Click();
        var labels = VisibleOptionLabels(cut);
        Assert.Contains("Carrot", labels);
        Assert.DoesNotContain("Apple", labels);
        Assert.DoesNotContain("Banana", labels);
    }

    [Fact]
    public void Escape_key_closes_the_open_picker()
    {
        var cut = Render();

        cut.Find("button").Click();
        Assert.NotEmpty(VisibleOptionLabels(cut));

        // The content panel handles keydown; Escape closes the picker.
        cut.Find("[tabindex=\"-1\"]").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Empty(VisibleOptionLabels(cut));
    }
}
