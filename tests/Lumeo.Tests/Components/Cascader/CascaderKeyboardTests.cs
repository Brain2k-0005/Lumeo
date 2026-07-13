using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Cascader;

/// <summary>
/// Keyboard roving across the cascader's columns (WAI-ARIA menu pattern): Up/Down move
/// the roving tab stop within a column, Right drills into the focused parent's children
/// (and moves focus there), Left returns to the parent column, and Enter activates a leaf.
/// Moving DOM focus is JS interop (a no-op under bUnit), so these assert the observable
/// surface: option roles, the roving tabindex, expanded child columns, and the value.
/// </summary>
public class CascaderKeyboardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public CascaderKeyboardTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static List<L.Cascader.CascaderOption> BuildOptions() =>
    [
        new()
        {
            Label = "Fruit", Value = "fruit",
            Children = [new() { Label = "Apple", Value = "apple" }, new() { Label = "Banana", Value = "banana" }],
        },
        new() { Label = "Veg", Value = "veg", Children = [new() { Label = "Carrot", Value = "carrot" }] },
    ];

    private IRenderedComponent<L.Cascader> Render(
        Action<ComponentParameterCollectionBuilder<L.Cascader>>? extra = null)
        => _ctx.Render<L.Cascader>(p =>
        {
            p.Add(c => c.Options, BuildOptions());
            extra?.Invoke(p);
        });

    // Option buttons carry role=menuitem; the trigger does not, so this never matches it.
    private static IElement Option(IRenderedComponent<L.Cascader> cut, string label)
        => cut.FindAll("button[role='menuitem']").First(b => b.TextContent.Trim() == label);

    // The content wrapper owns @onkeydown and is the first tabindex=-1 element in DOM order.
    private static IElement Content(IRenderedComponent<L.Cascader> cut) => cut.Find("[tabindex='-1']");

    // The trigger is the only button without role=menuitem — the option buttons all carry it.
    private static IElement Trigger(IRenderedComponent<L.Cascader> cut) => cut.Find("button:not([role='menuitem'])");

    private static string? Tab(IElement el) => el.GetAttribute("tabindex");

    [Fact]
    public void Options_Are_Menuitems_And_Parents_Advertise_A_Popup()
    {
        var cut = Render();
        cut.Find("button").Click(); // open

        var fruit = Option(cut, "Fruit");
        Assert.Equal("menuitem", fruit.GetAttribute("role"));
        Assert.Equal("menu", fruit.GetAttribute("aria-haspopup")); // Fruit has children
    }

    // NVDA audit finding: the trigger button exposed no aria-haspopup/aria-expanded, so a
    // screen reader announced only "button" — no indication it opens a cascading menu or
    // whether that menu is currently open. Mirrors Select/SpeedDial trigger precedent.
    [Fact]
    public void Trigger_Advertises_Its_Menu_Popup()
    {
        var cut = Render();
        var trigger = Trigger(cut);
        Assert.Equal("menu", trigger.GetAttribute("aria-haspopup"));
        Assert.Equal("false", trigger.GetAttribute("aria-expanded"));

        trigger.Click(); // open

        Assert.Equal("true", Trigger(cut).GetAttribute("aria-expanded"));
    }

    [Fact]
    public void First_Option_Is_The_Roving_Tab_Stop_On_Open()
    {
        var cut = Render();
        cut.Find("button").Click();

        Assert.Equal("0", Tab(Option(cut, "Fruit")));
        Assert.Equal("-1", Tab(Option(cut, "Veg")));
    }

    [Fact]
    public void ArrowDown_Then_ArrowUp_Moves_The_Roving_Tab_Stop()
    {
        var cut = Render();
        cut.Find("button").Click();

        Content(cut).KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        Assert.Equal("0", Tab(Option(cut, "Veg")));
        Assert.Equal("-1", Tab(Option(cut, "Fruit")));

        Content(cut).KeyDown(new KeyboardEventArgs { Key = "ArrowUp" });
        Assert.Equal("0", Tab(Option(cut, "Fruit")));
    }

    [Fact]
    public void ArrowRight_Drills_Into_The_Focused_Parents_Children()
    {
        var cut = Render();
        cut.Find("button").Click();

        // Fruit is the roving tab stop (col 0, idx 0); ArrowRight opens its child column.
        Content(cut).KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        var labels = cut.FindAll("button[role='menuitem']").Select(b => b.TextContent.Trim()).ToList();
        Assert.Contains("Apple", labels);
        Assert.Contains("Banana", labels);
        // The first child becomes the roving tab stop.
        Assert.Equal("0", Tab(Option(cut, "Apple")));
    }

    [Fact]
    public void ArrowLeft_Returns_To_The_Parent_Column()
    {
        var cut = Render();
        cut.Find("button").Click();
        Content(cut).KeyDown(new KeyboardEventArgs { Key = "ArrowRight" }); // into Fruit's children
        Assert.Equal("0", Tab(Option(cut, "Apple")));

        Content(cut).KeyDown(new KeyboardEventArgs { Key = "ArrowLeft" });
        // Focus is back on the parent that was expanded.
        Assert.Equal("0", Tab(Option(cut, "Fruit")));
    }

    [Fact]
    public void Enter_On_A_Leaf_Commits_The_Path_And_Closes()
    {
        List<string>? committed = null;
        var cut = Render(p => p.Add(c => c.ValueChanged, (List<string>? v) => committed = v));
        cut.Find("button").Click();

        Content(cut).KeyDown(new KeyboardEventArgs { Key = "ArrowRight" }); // into Fruit's children, focus Apple
        Content(cut).KeyDown(new KeyboardEventArgs { Key = "Enter" });      // activate Apple (leaf)

        Assert.Equal(new List<string> { "fruit", "apple" }, committed);
        // Closed: the option columns are gone.
        Assert.Empty(cut.FindAll("button[role='menuitem']"));
    }
}
