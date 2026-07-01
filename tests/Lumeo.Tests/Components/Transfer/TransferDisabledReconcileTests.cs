using AngleSharp.Dom;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Transfer;

/// <summary>
/// Battle-test wave 1, edge-data regression #34 for Transfer:
/// a row the parent flips to Disabled WHILE it is already selected must not stay
/// stuck-selected. Before the fix the checkbox rendered checked+disabled
/// (un-uncheckable), the header count stayed inflated, select-all skewed, and a
/// Move silently left the row behind (Move excludes disabled rows). The
/// reconciliation pass in OnParametersSet/PruneSelection now drops a selected
/// Value once its item becomes Disabled.
/// </summary>
public class TransferDisabledReconcileTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TransferDisabledReconcileTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static List<L.Transfer.TransferItem> Source(bool appleDisabled = false) =>
    [
        new("Apple", "apple", Disabled: appleDisabled),
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

    [Fact]
    public void Selecting_Then_Flipping_Item_To_Disabled_Drops_It_From_Selection()
    {
        // Apple starts enabled and the user selects it -> "1 / 3".
        var cut = _ctx.Render<L.Transfer>(p => p.Add(c => c.SourceItems, Source()));
        ItemCheckbox(cut, "Apple").Click();
        Assert.Contains("1 / 3", cut.Markup);

        // Parent now flips Apple to Disabled (same Value, still present). Without
        // reconciliation the stale selection survives: the count would still read
        // "1 / 3" and Apple's checkbox would be checked+disabled.
        cut.Render(p => p.Add(c => c.SourceItems, Source(appleDisabled: true)));

        // The now-disabled Apple is dropped from the selection: count back to 0,
        // and its checkbox is unchecked (no longer stuck-checked) and disabled.
        Assert.Contains("0 / 3", cut.Markup);
        Assert.DoesNotContain("1 / 3", cut.Markup);

        var apple = ItemCheckbox(cut, "Apple");
        Assert.Equal("false", apple.GetAttribute("aria-checked"));
        Assert.True(apple.HasAttribute("disabled"));
    }
}
