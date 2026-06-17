using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Transfer;

/// <summary>
/// #202 — the Transfer now offers a per-panel select-all checkbox (enabled +
/// visible items only, with an indeterminate partial state) and per-item
/// Disabled support (disabled rows can't be selected or moved).
/// </summary>
public class TransferSelectAllDisabledTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TransferSelectAllDisabledTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static List<L.Transfer.TransferItem> Source() =>
    [
        new("Apple", "apple"),
        new("Banana", "banana"),
        new("Cherry", "cherry", Disabled: true),
    ];

    private IRenderedComponent<L.Transfer> Render(
        List<L.Transfer.TransferItem>? source = null,
        EventCallback<List<L.Transfer.TransferItem>>? targetChanged = null,
        Action<ComponentParameterCollectionBuilder<L.Transfer>>? extra = null)
        => _ctx.Render<L.Transfer>(p =>
        {
            p.Add(c => c.SourceItems, source ?? Source());
            if (targetChanged.HasValue) p.Add(c => c.TargetItemsChanged, targetChanged.Value);
            extra?.Invoke(p);
        });

    // Header checkbox is the first role=checkbox; item checkboxes follow.
    private static AngleSharp.Dom.IElement SourceSelectAll(IRenderedComponent<L.Transfer> cut)
        => cut.FindAll("button[role='checkbox']")[0];

    [Fact]
    public void Select_All_Checkbox_Renders_In_Header_By_Default()
    {
        var cut = Render();
        // 1 header select-all + 3 item checkboxes in the source panel,
        // plus 1 header in the (empty) target panel.
        Assert.True(cut.FindAll("button[role='checkbox']").Count >= 4);
    }

    [Fact]
    public void Disabled_Item_Checkbox_Is_Disabled()
    {
        var cut = Render();
        var cherry = cut.FindAll("button[role='checkbox']")
            .First(c => (c.ParentElement?.TextContent ?? "").Contains("Cherry"));
        Assert.True(cherry.HasAttribute("disabled"));
    }

    [Fact]
    public void Select_All_Selects_Only_Enabled_Items()
    {
        var cut = Render();
        SourceSelectAll(cut).Click();

        // Apple + Banana selected (2 enabled); Cherry (disabled) skipped.
        // The header count reflects "2 / 3".
        Assert.Contains("2 / 3", cut.Markup);
    }

    [Fact]
    public void Select_All_Then_Move_Moves_Only_Enabled_Items()
    {
        List<L.Transfer.TransferItem>? movedTarget = null;
        var cb = EventCallback.Factory.Create<List<L.Transfer.TransferItem>>(this, t => movedTarget = t);
        var cut = Render(targetChanged: cb);

        SourceSelectAll(cut).Click();
        // Move-to-target is the first transfer button (ChevronRight).
        cut.FindAll("button").First(b => (b.GetAttribute("class") ?? "").Contains("h-8 w-8")).Click();

        Assert.NotNull(movedTarget);
        Assert.Equal(2, movedTarget!.Count); // apple + banana
        Assert.DoesNotContain(movedTarget, i => i.Value == "cherry");
    }

    [Fact]
    public void Select_All_Reflects_Indeterminate_When_Some_Selected()
    {
        var cut = Render();
        // Select just one enabled item (Apple).
        var apple = cut.FindAll("button[role='checkbox']")
            .First(c => (c.ParentElement?.TextContent ?? "").Contains("Apple"));
        apple.Click();

        // The header select-all is now indeterminate (aria-checked="mixed"),
        // not fully checked.
        var selectAll = SourceSelectAll(cut);
        Assert.Equal("mixed", selectAll.GetAttribute("aria-checked"));
    }

    [Fact]
    public void ShowSelectAll_False_Hides_The_Header_Checkbox()
    {
        var cut = Render(extra: p => p.Add(c => c.ShowSelectAll, false));

        // Only the 3 item checkboxes in source remain (no header select-all,
        // and the empty target panel has no items either).
        Assert.Equal(3, cut.FindAll("button[role='checkbox']").Count);
    }

    [Fact]
    public void Disabled_Item_Cannot_Be_Selected_By_Clicking()
    {
        var cut = Render();
        var cherry = cut.FindAll("button[role='checkbox']")
            .First(c => (c.ParentElement?.TextContent ?? "").Contains("Cherry"));
        // A disabled button's click is a no-op; the count stays 0 / 3.
        cherry.Click();
        Assert.Contains("0 / 3", cut.Markup);
    }
}
