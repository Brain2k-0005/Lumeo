using AngleSharp.Dom;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Cascader;

/// <summary>
/// A new Options list instance with identical content (e.g. a parent that
/// re-creates <c>BuildOptions()</c> on every render, or async-reloaded options)
/// must NOT wipe the user's in-progress, uncommitted drill-down. While the
/// picker is open with no committed Value, the live drill-down trail
/// (_selectedPath / _activePanels) is owned by the user — only a real Value
/// change is allowed to rebuild it. Previously the
/// <c>!ReferenceEquals(_lastSyncedOptions, Options)</c> gate in
/// OnParametersSet fired on the fresh-but-equal instance and unconditionally
/// cleared the trail, collapsing the child column back to the root.
/// </summary>
public class CascaderOptionsResyncTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public CascaderOptionsResyncTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // A FRESH list instance each call (identical content) — mirrors a parent
    // whose render fragment recreates the option tree on every re-render.
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

    private static IElement? OptionButton(IRenderedComponent<L.Cascader> cut, string label)
        => cut.FindAll("button").FirstOrDefault(b => b.TextContent.Trim() == label);

    private static IReadOnlyList<string> VisibleOptionLabels(IRenderedComponent<L.Cascader> cut)
        => cut.FindAll("button")
            .Select(b => b.TextContent.Trim())
            .Where(t => t is "Fruit" or "Veg" or "Apple" or "Banana" or "Carrot")
            .ToList();

    [Fact]
    public void New_options_instance_same_content_preserves_in_progress_drill()
    {
        var cut = _ctx.Render<L.Cascader>(p => p.Add(c => c.Options, BuildOptions()));

        // Open and drill into a parent — this is an UNCOMMITTED in-progress trail
        // (Value is still null because "Fruit" is a non-leaf parent).
        cut.Find("button").Click();
        OptionButton(cut, "Fruit")!.Click();
        Assert.Contains("Apple", VisibleOptionLabels(cut)); // child column open

        // Parent re-renders with a brand-new Options list of identical content.
        // The reference differs but nothing the user cares about changed.
        cut.Render(p => p.Add(c => c.Options, BuildOptions()));

        // The in-progress child column must survive (was wiped before the fix).
        var labels = VisibleOptionLabels(cut);
        Assert.Contains("Apple", labels);
        Assert.Contains("Banana", labels);
        Assert.DoesNotContain("Carrot", labels);
    }

    [Fact]
    public void New_options_instance_still_resyncs_a_committed_value()
    {
        // Regression guard: a committed Value (closed picker) must still resolve
        // its drill-down trail against the latest Options instance.
        var cut = _ctx.Render<L.Cascader>(p => p
            .Add(c => c.Options, BuildOptions())
            .Add(c => c.Value, new List<string> { "fruit", "apple" }));

        // Swap in a fresh Options instance, keeping the same committed Value.
        cut.Render(p => p
            .Add(c => c.Options, BuildOptions())
            .Add(c => c.Value, new List<string> { "fruit", "apple" }));

        // Opening still shows the committed path highlighted (trail intact).
        cut.Find("button").Click();
        var selected = cut.FindAll("button").Count(b => b.ClassList.Contains("bg-accent"));
        Assert.Equal(2, selected);
    }
}
