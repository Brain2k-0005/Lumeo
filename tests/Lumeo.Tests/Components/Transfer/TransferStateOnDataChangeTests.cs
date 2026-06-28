using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Transfer;

/// <summary>
/// Battle-test wave 1, state-on-data-change regressions for Transfer:
///  • #33 — selection HashSets are reconciled against incoming Items so a stale
///    Value (ghost selection) can't survive an external data refresh, while a
///    transient empty→refill load does NOT wipe an in-progress selection.
///  • #35 — Move only transfers rows that are BOTH selected AND currently
///    visible (passing the active search filter); a selected-but-filtered-out
///    row stays put instead of being moved invisibly.
/// </summary>
public class TransferStateOnDataChangeTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TransferStateOnDataChangeTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static List<L.Transfer.TransferItem> Source() =>
    [
        new("Apple", "apple"),
        new("Banana", "banana"),
        new("Cherry", "cherry"),
    ];

    // Item checkbox whose row label contains the given text.
    // Checkbox now wraps its <button> in an outer <div>, so ParentElement is that
    // wrapper div; ParentElement.ParentElement is the item <label> row that holds
    // the visible label text.
    private static IElement ItemCheckbox(IRenderedComponent<L.Transfer> cut, string label)
        => cut.FindAll("button[role='checkbox']")
            .First(c => (c.ParentElement?.ParentElement?.TextContent ?? "").Contains(label));

    // ---- #33: stale selection is pruned on a same-set/shrink data refresh ----

    [Fact]
    public void Ghost_Selection_Is_Pruned_When_Item_Removed_From_SourceItems()
    {
        var cut = _ctx.Render<L.Transfer>(p => p.Add(c => c.SourceItems, Source()));

        // Select Apple -> header reflects "1 / 3".
        ItemCheckbox(cut, "Apple").Click();
        Assert.Contains("1 / 3", cut.Markup);

        // Parent refreshes the list dropping Apple. The "apple" Value is now a
        // ghost in _selectedSource; without reconciliation the count would read
        // "1 / 2" (selection referencing a Value that isn't in the list).
        cut.Render(p => p.Add(c => c.SourceItems, new List<L.Transfer.TransferItem>
        {
            new("Banana", "banana"),
            new("Cherry", "cherry"),
        }));

        Assert.Contains("0 / 2", cut.Markup);
        Assert.DoesNotContain("1 / 2", cut.Markup);
    }

    [Fact]
    public void Transient_Empty_Refill_Does_Not_Wipe_Selection()
    {
        var cut = _ctx.Render<L.Transfer>(p => p.Add(c => c.SourceItems, Source()));

        ItemCheckbox(cut, "Apple").Click();
        Assert.Contains("1 / 3", cut.Markup);

        // Async refresh flicker: Items briefly goes empty, then refills with the
        // same content. The empty pass must NOT prune the live selection.
        cut.Render(p => p.Add(c => c.SourceItems, new List<L.Transfer.TransferItem>()));
        cut.Render(p => p.Add(c => c.SourceItems, Source()));

        // Apple is still selected after the refill.
        Assert.Contains("1 / 3", cut.Markup);
        Assert.Equal("true", ItemCheckbox(cut, "Apple").GetAttribute("aria-checked"));
    }

    // ---- #35: Move respects the active search filter ----

    [Fact]
    public void Move_Does_Not_Transfer_Selected_But_Filtered_Out_Rows()
    {
        List<L.Transfer.TransferItem>? movedTarget = null;
        var targetChanged = EventCallback.Factory.Create<List<L.Transfer.TransferItem>>(
            this, t => movedTarget = t);

        var cut = _ctx.Render<L.Transfer>(p =>
        {
            p.Add(c => c.SourceItems, Source());
            p.Add(c => c.ShowSearch, true);
            p.Add(c => c.TargetItemsChanged, targetChanged);
        });

        // Select Apple AND Banana while everything is visible.
        ItemCheckbox(cut, "Apple").Click();
        ItemCheckbox(cut, "Banana").Click();

        // Filter the source so only Banana is visible (Apple is filtered out but
        // stays selected).
        var sourceSearch = cut.FindAll("input[type='text']")[0];
        sourceSearch.Input("ban");

        // Move-to-target is the first h-8 w-8 transfer button (ChevronRight).
        cut.FindAll("button")
            .First(b => (b.GetAttribute("class") ?? "").Contains("h-8 w-8"))
            .Click();

        // Only the visible+selected Banana moved; the filtered-out Apple did not.
        Assert.NotNull(movedTarget);
        Assert.Contains(movedTarget!, i => i.Value == "banana");
        Assert.DoesNotContain(movedTarget!, i => i.Value == "apple");
    }
}
