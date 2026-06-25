using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.PickList;

/// <summary>
/// #71 — the roving-tabindex focus must be anchored to the focused ITEM's stable
/// identity, not a bare positional index. After items move between panels or are
/// reordered within a panel, the single tabbable option (tabindex="0") must stay
/// on the same item rather than pointing at whatever item now sits at the old
/// numeric position.
/// </summary>
public class PickListRovingFocusTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public PickListRovingFocusTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<Lumeo.PickList<string>> Render(
        IEnumerable<string>? items = null,
        IEnumerable<string>? selected = null,
        Action<ComponentParameterCollectionBuilder<Lumeo.PickList<string>>>? extra = null)
        => _ctx.Render<Lumeo.PickList<string>>(p =>
        {
            p.Add(l => l.Items, items ?? new[] { "Alpha", "Bravo", "Charlie" });
            p.Add(l => l.SelectedItems, selected ?? Array.Empty<string>());
            p.Add(l => l.ShowSearch, false);
            extra?.Invoke(p);
        });

    private static AngleSharp.Dom.IElement SourceList(IRenderedComponent<Lumeo.PickList<string>> cut)
        => cut.FindAll("[role='listbox']")[0];
    private static AngleSharp.Dom.IElement TargetList(IRenderedComponent<Lumeo.PickList<string>> cut)
        => cut.FindAll("[role='listbox']")[1];

    private static AngleSharp.Dom.IElement Option(AngleSharp.Dom.IElement list, string text)
        => list.QuerySelectorAll("button[role='option']").First(b => b.TextContent.Trim() == text);

    // The single tabbable (tabindex="0") option's text within a list.
    private static string TabbableOption(AngleSharp.Dom.IElement list)
        => list.QuerySelectorAll("button[role='option'][tabindex='0']").Single().TextContent.Trim();

    // ------------------------------------------------------------------ #71 --
    // Focus a target item, then reorder it to a new slot. The tabbable option
    // must follow the item (by identity), not stay parked at the old position.
    [Fact]
    public async Task Roving_focus_follows_the_item_after_a_target_reorder()
    {
        var cut = Render(
            items: new[] { "Alpha", "Bravo", "Charlie" },
            selected: new[] { "Alpha", "Bravo", "Charlie" }); // all in target: A,B,C

        // Focus + select "Charlie" (the 3rd target row, index 2).
        Option(TargetList(cut), "Charlie").Focus();
        Option(TargetList(cut), "Charlie").Click();
        Assert.Equal("Charlie", TabbableOption(TargetList(cut)));

        // Alt+ArrowUp reorders Charlie up one slot -> A, C, B. Charlie now sits at
        // index 1; the old positional focus index (2) would point at "Bravo".
        await cut.InvokeAsync(() => Option(TargetList(cut), "Charlie")
            .KeyDown(new KeyboardEventArgs { Key = "ArrowUp", AltKey = true }));
        Assert.Equal(new[] { "Alpha", "Charlie", "Bravo" },
            TargetList(cut).QuerySelectorAll("button[role='option']").Select(b => b.TextContent.Trim()));

        // The tabbable option must still be "Charlie" (anchored by identity),
        // NOT "Bravo" (the item now at the stale numeric index 2).
        Assert.Equal("Charlie", TabbableOption(TargetList(cut)));
    }

    // Focus a middle source item, then move an EARLIER item to the target so the
    // focused item shifts to a lower index. The tabbable option must remain on the
    // originally-focused item, not on whatever now sits at the old numeric index.
    [Fact]
    public void Roving_focus_follows_the_item_after_an_earlier_item_leaves_the_source_panel()
    {
        var cut = Render(items: new[] { "Alpha", "Bravo", "Charlie", "Delta" }); // all in source

        // Roving focus lands on "Bravo" (source index 1).
        Option(SourceList(cut), "Bravo").Focus();
        Assert.Equal("Bravo", TabbableOption(SourceList(cut)));

        // Select + move "Alpha" out to the target. Source becomes Bravo, Charlie,
        // Delta — "Bravo" now sits at index 0; the stale positional index (1) would
        // point at "Charlie".
        Option(SourceList(cut), "Alpha").Click();
        cut.Find("button[aria-label='Move selected']").Click();
        Assert.Equal(new[] { "Bravo", "Charlie", "Delta" },
            SourceList(cut).QuerySelectorAll("button[role='option']").Select(b => b.TextContent.Trim()));

        // The tabbable option must still be "Bravo" (anchored by identity),
        // NOT "Charlie" (the item now at the stale numeric index 1).
        Assert.Equal("Bravo", TabbableOption(SourceList(cut)));
    }
}
