using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Cascader;

/// <summary>
/// Battle-test #29 — clearing the value while the picker is open collapses the
/// drill-down so only the root column renders. The roving tab stop
/// (_focusCol/_focusIdx) was left parked on a now-gone deeper column, so NO
/// option carried tabindex=0 and keyboard focus was silently dropped. The fix
/// re-anchors the roving stop onto the first root option (and queues focus)
/// inside Clear(). These assert the observable surface — the single tabindex=0
/// roving stop — not real DOM focus (which is a JS no-op under bUnit).
/// </summary>
public class CascaderClearFocusTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public CascaderClearFocusTests() => _ctx.AddLumeoServices();
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

    // Render with a committed Value whose head ("Fruit") has children, and
    // Clearable so the X button is present in the trigger.
    private IRenderedComponent<L.Cascader> Render()
        => _ctx.Render<L.Cascader>(p => p
            .Add(c => c.Options, BuildOptions())
            .Add(c => c.Clearable, true)
            .Add(c => c.Value, new List<string> { "fruit" }));

    // Option buttons carry role=menuitem; the trigger / clear button do not.
    private static IElement Option(IRenderedComponent<L.Cascader> cut, string label)
        => cut.FindAll("button[role='menuitem']").First(b => b.TextContent.Trim() == label);

    // The content wrapper owns @onkeydown and is the first tabindex=-1 element.
    private static IElement Content(IRenderedComponent<L.Cascader> cut) => cut.Find("[tabindex='-1']");

    [Fact]
    public void Clearing_While_Open_Keeps_A_Valid_Roving_Tab_Stop_On_The_Root()
    {
        var cut = Render();

        // Open: the trigger is the first/outermost button in DOM order.
        cut.Find("button").Click();

        // Drill into Fruit's children — this moves the roving stop into column 1
        // (Apple becomes the tabindex=0 option). Fruit was the seeded stop.
        Content(cut).KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });
        Assert.Equal("0", Option(cut, "Apple").GetAttribute("tabindex")); // col 1 owns the stop

        // Click the clear (X) button — aria-label "Clear selection".
        var clear = cut.FindAll("button").First(b => b.GetAttribute("aria-label") is not null);
        clear.Click();

        // The deeper column collapsed: only the root options remain.
        var menuItems = cut.FindAll("button[role='menuitem']").Select(b => b.TextContent.Trim()).ToList();
        Assert.DoesNotContain("Apple", menuItems);
        Assert.Contains("Fruit", menuItems);

        // Exactly one roving tab stop survives, and it is the first root option —
        // not a dropped/stale tabindex=0 on a column that no longer renders.
        var roving = cut.FindAll("button[role='menuitem'][tabindex='0']");
        Assert.Single(roving);
        Assert.Equal("Fruit", roving[0].TextContent.Trim());
    }
}
