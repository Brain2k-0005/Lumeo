using AngleSharp.Dom;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Cascader;

/// <summary>
/// Battle-test #69 (lifecycle) — re-opening the picker after a value is already
/// committed used to seed the roving stop back to (0,0) (the root column's first
/// option) instead of the committed selection's option in the deepest rendered
/// column. A keyboard user re-opening a populated cascader was dumped on the root
/// and had to re-drill. The fix (SetOpen.SeedFocusToSelection) anchors the single
/// tabindex=0 roving stop onto the committed option in the deepest column.
///
/// These assert the observable surface — which option carries tabindex=0 — not
/// real DOM focus (a JS no-op under bUnit). With a committed value ["fruit",
/// "apple"], "Fruit" has children so it expands column 0->1 and the committed
/// leaf "Apple" lives in column 1; the roving stop must land on "Apple", not
/// "Fruit".
/// </summary>
public class CascaderReopenFocusTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public CascaderReopenFocusTests() => _ctx.AddLumeoServices();
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

    // Render with a committed two-level Value whose head ("Fruit") has children,
    // so opening drills to a second column and the committed leaf ("Apple") lives
    // in that deeper column.
    private IRenderedComponent<L.Cascader> Render()
        => _ctx.Render<L.Cascader>(p => p
            .Add(c => c.Options, BuildOptions())
            .Add(c => c.Value, new List<string> { "fruit", "apple" }));

    // Option buttons carry role=menuitem; the trigger / clear button do not.
    private static IReadOnlyList<IElement> RovingStops(IRenderedComponent<L.Cascader> cut)
        => cut.FindAll("button[role='menuitem'][tabindex='0']");

    [Fact]
    public void Opening_With_A_Committed_Value_Seeds_Focus_On_The_Deepest_Selected_Option()
    {
        var cut = Render();

        // Open: the trigger is the first/outermost button in DOM order.
        cut.Find("button").Click();

        // Both columns render (root + Fruit's children). Exactly one roving stop
        // survives and it is the committed leaf "Apple" in the deeper column —
        // NOT "Fruit" at the root (0,0), which is what the pre-fix seed produced.
        var roving = RovingStops(cut);
        Assert.Single(roving);
        Assert.Equal("Apple", roving[0].TextContent.Trim());
    }

    [Fact]
    public void Reopening_After_Close_Reseeds_Focus_On_The_Deepest_Selected_Option()
    {
        var cut = Render();

        // Open, then close (toggle the trigger), then re-open — the literal
        // re-open repro from battle-test #69.
        cut.Find("button").Click(); // open
        cut.Find("button").Click(); // close
        cut.Find("button").Click(); // re-open

        var roving = RovingStops(cut);
        Assert.Single(roving);
        Assert.Equal("Apple", roving[0].TextContent.Trim());
    }

    [Fact]
    public void Opening_With_No_Committed_Value_Falls_Back_To_The_Root_First_Option()
    {
        // Guard the fallback path: with no committed Value the seed must remain
        // (0,0) — the first option of the root column.
        var cut = _ctx.Render<L.Cascader>(p => p
            .Add(c => c.Options, BuildOptions()));

        cut.Find("button").Click();

        var roving = RovingStops(cut);
        Assert.Single(roving);
        Assert.Equal("Fruit", roving[0].TextContent.Trim());
    }
}
