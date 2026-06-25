using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.PickList;

/// <summary>
/// Regression tests for the PickList "state survives a data refresh" battle-test
/// findings:
///   • #37 — a transient-empty Items snapshot during an async refresh must NOT
///     wipe the user's in-progress source checkbox selection.
///   • #38 — a within-target reorder must survive the next OnParametersSet when
///     the parent re-renders with the SAME SET of SelectedItems (it simply did
///     not echo the emitted order); only a genuine set change re-seeds the order.
/// </summary>
public class PickListStateOnDataChangeTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public PickListStateOnDataChangeTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // The source ("Available") listbox holds items that are NOT selected;
    // the target ("Selected") listbox holds the SelectedItems.
    private static IElement SourceListbox(IRenderedComponent<L.PickList<string>> cut)
        => cut.FindAll("[role='listbox']").First(e => e.GetAttribute("aria-label") == "Available");

    private static IElement TargetListbox(IRenderedComponent<L.PickList<string>> cut)
        => cut.FindAll("[role='listbox']").First(e => e.GetAttribute("aria-label") == "Selected");

    private static IReadOnlyList<IElement> Options(IElement listbox)
        => listbox.QuerySelectorAll("button[role='option']").ToList();

    private static IElement SourceOption(IRenderedComponent<L.PickList<string>> cut, string text)
        => Options(SourceListbox(cut)).First(b => b.TextContent.Trim() == text);

    private static IReadOnlyList<string> TargetOrder(IRenderedComponent<L.PickList<string>> cut)
        => Options(TargetListbox(cut)).Select(b => b.TextContent.Trim()).ToList();

    // ------------------------------------------------------------------ #37 --
    // User checks a source row, then Items briefly goes empty (async reload
    // flicker) and refills with identical content. The checkbox selection must
    // survive: OnParametersSet must not prune _sourceSelected against the
    // transient-empty source snapshot.
    [Fact]
    public void Transient_empty_items_refresh_preserves_in_progress_source_selection()
    {
        var all = new List<string> { "Alpha", "Beta", "Gamma" };
        var cut = _ctx.Render<L.PickList<string>>(p => p
            .Add(c => c.Items, all)
            .Add(c => c.SelectedItems, new List<string>()));

        // Check "Beta" in the source panel (adds it to the in-progress selection).
        SourceOption(cut, "Beta").Click();
        Assert.Equal("true", SourceOption(cut, "Beta").GetAttribute("aria-selected"));

        // Async refresh transiently empties Items (loading flicker).
        cut.Render(p => p
            .Add(c => c.Items, new List<string>())
            .Add(c => c.SelectedItems, new List<string>()));

        // Data settles back to the same content.
        cut.Render(p => p
            .Add(c => c.Items, new List<string> { "Alpha", "Beta", "Gamma" })
            .Add(c => c.SelectedItems, new List<string>()));

        // The in-progress selection must still be there (was wiped before the fix).
        Assert.Equal("true", SourceOption(cut, "Beta").GetAttribute("aria-selected"));
        // The header count badge reflects "1 / 3".
        Assert.Contains("1 / 3", SourceListbox(cut).ParentElement!.TextContent);
    }

    // ------------------------------------------------------------------ #38 --
    // User reorders within the target list. The parent re-renders WITHOUT
    // echoing the emitted order (passes the original SelectedItems order back).
    // The component must keep the user's order because the SET is unchanged.
    [Fact]
    public async Task Within_target_reorder_survives_a_same_set_selecteditems_refresh()
    {
        var items = new List<string> { "Alpha", "Beta", "Gamma", "Delta" };
        var selected = new List<string> { "Alpha", "Beta", "Gamma" };
        var cut = _ctx.Render<L.PickList<string>>(p => p
            .Add(c => c.Items, items)
            .Add(c => c.SelectedItems, selected));

        Assert.Equal(new[] { "Alpha", "Beta", "Gamma" }, TargetOrder(cut));

        // Select "Gamma" in the target, then Alt+ArrowUp on that row to reorder it
        // up one slot -> Alpha, Gamma, Beta. (Keyboard reorder hits ReorderTarget
        // directly, no Tooltip wrapper.)
        var gamma = Options(TargetListbox(cut)).First(b => b.TextContent.Trim() == "Gamma");
        gamma.Click();
        await cut.InvokeAsync(() => Options(TargetListbox(cut))
            .First(b => b.TextContent.Trim() == "Gamma")
            .KeyDown(new KeyboardEventArgs { Key = "ArrowUp", AltKey = true }));
        Assert.Equal(new[] { "Alpha", "Gamma", "Beta" }, TargetOrder(cut));

        // Parent re-renders with the SAME SET but the ORIGINAL order (did not echo
        // the reorder). The user's order must be preserved (snapped back before fix).
        cut.Render(p => p
            .Add(c => c.Items, new List<string> { "Alpha", "Beta", "Gamma", "Delta" })
            .Add(c => c.SelectedItems, new List<string> { "Alpha", "Beta", "Gamma" }));

        Assert.Equal(new[] { "Alpha", "Gamma", "Beta" }, TargetOrder(cut));
    }

    // Regression guard for #38: a GENUINE set change (item added to selection)
    // must still re-seed the target order from the new SelectedItems parameter.
    [Fact]
    public void Genuine_selecteditems_set_change_reseeds_target_order()
    {
        var cut = _ctx.Render<L.PickList<string>>(p => p
            .Add(c => c.Items, new List<string> { "Alpha", "Beta", "Gamma", "Delta" })
            .Add(c => c.SelectedItems, new List<string> { "Alpha", "Beta" }));

        Assert.Equal(new[] { "Alpha", "Beta" }, TargetOrder(cut));

        // Parent adds "Delta" to the selection in a specific order — the set
        // changed, so the new order is authoritative.
        cut.Render(p => p
            .Add(c => c.Items, new List<string> { "Alpha", "Beta", "Gamma", "Delta" })
            .Add(c => c.SelectedItems, new List<string> { "Delta", "Alpha", "Beta" }));

        Assert.Equal(new[] { "Delta", "Alpha", "Beta" }, TargetOrder(cut));
    }
}
