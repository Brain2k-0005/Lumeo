using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.PickList;

/// <summary>
/// Regression tests for PickList battle-test finding #39 (edge-data):
/// "Move all" / "Move all back" must operate on the currently *filtered*
/// (visible) items while a search filter is active, instead of silently moving
/// items the user can no longer see. With no active search the filtered list is
/// the full list, so the normal "move everything" behaviour is unchanged.
/// </summary>
public class PickListMoveAllFilteredTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public PickListMoveAllFilteredTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static IElement SourceListbox(IRenderedComponent<L.PickList<string>> cut)
        => cut.FindAll("[role='listbox']").First(e => e.GetAttribute("aria-label") == "Available");

    private static IElement TargetListbox(IRenderedComponent<L.PickList<string>> cut)
        => cut.FindAll("[role='listbox']").First(e => e.GetAttribute("aria-label") == "Selected");

    // The source ("Available") search input is the first text box; the target
    // ("Selected") search input is the second.
    private static IElement SourceSearchInput(IRenderedComponent<L.PickList<string>> cut)
        => cut.FindAll("input[type='text']")[0];

    private static IElement TargetSearchInput(IRenderedComponent<L.PickList<string>> cut)
        => cut.FindAll("input[type='text']")[1];

    private static IElement MoveAllToTargetButton(IRenderedComponent<L.PickList<string>> cut)
        => cut.FindAll("button").First(b => b.GetAttribute("aria-label") == "Move all");

    private static IElement MoveAllToSourceButton(IRenderedComponent<L.PickList<string>> cut)
        => cut.FindAll("button").First(b => b.GetAttribute("aria-label") == "Move back all");

    // ------------------------------------------------------------------ #39 --
    // A search filter on the source panel narrows it to "Apple". "Move all"
    // must move ONLY the visible "Apple", leaving the filtered-out "Apricot"
    // and "Banana" in the source. Before the fix it moved the full source list.
    [Fact]
    public async Task MoveAll_to_target_only_moves_filtered_source_items()
    {
        IEnumerable<string>? captured = null;
        var cut = _ctx.Render<L.PickList<string>>(p => p
            .Add(c => c.Items, new List<string> { "Apple", "Apricot", "Banana" })
            .Add(c => c.SelectedItems, new List<string>())
            .Add(c => c.SelectedItemsChanged,
                EventCallback.Factory.Create<IEnumerable<string>>(this, v => captured = v)));

        // Filter the source panel to just "Apple".
        SourceSearchInput(cut).Input("Apple");

        await MoveAllToTargetButton(cut).ClickAsync(new MouseEventArgs());

        // Only the visible item moved across.
        Assert.NotNull(captured);
        Assert.Equal(new[] { "Apple" }, captured!);

        // Clearing the still-active "Apple" search reveals the remaining source items:
        // the filtered-out Apricot/Banana stayed put; only the visible Apple moved.
        SourceSearchInput(cut).Input("");
        var sourceTexts = SourceListbox(cut)
            .QuerySelectorAll("button[role='option']")
            .Select(b => b.TextContent.Trim())
            .ToList();
        Assert.Contains("Apricot", sourceTexts);
        Assert.Contains("Banana", sourceTexts);
        Assert.DoesNotContain("Apple", sourceTexts);
    }

    // Mirror direction: a search on the target panel narrows it, and "Move all
    // back" must only return the visible target items.
    [Fact]
    public async Task MoveAllBack_to_source_only_moves_filtered_target_items()
    {
        IEnumerable<string>? captured = null;
        var cut = _ctx.Render<L.PickList<string>>(p => p
            .Add(c => c.Items, new List<string> { "Apple", "Apricot", "Banana" })
            .Add(c => c.SelectedItems, new List<string> { "Apple", "Apricot", "Banana" })
            .Add(c => c.SelectedItemsChanged,
                EventCallback.Factory.Create<IEnumerable<string>>(this, v => captured = v)));

        // Everything starts in the target panel; filter it to "Banana".
        TargetSearchInput(cut).Input("Banana");

        await MoveAllToSourceButton(cut).ClickAsync(new MouseEventArgs());

        // Only "Banana" left the target; the filtered-out items remain selected.
        Assert.NotNull(captured);
        Assert.Equal(new[] { "Apple", "Apricot" }, captured!);
    }

    // Regression guard: with NO active search, "Move all" still moves the entire
    // source list (the filtered list equals the full list).
    [Fact]
    public async Task MoveAll_with_no_search_still_moves_everything()
    {
        IEnumerable<string>? captured = null;
        var cut = _ctx.Render<L.PickList<string>>(p => p
            .Add(c => c.Items, new List<string> { "Apple", "Apricot", "Banana" })
            .Add(c => c.SelectedItems, new List<string>())
            .Add(c => c.SelectedItemsChanged,
                EventCallback.Factory.Create<IEnumerable<string>>(this, v => captured = v)));

        await MoveAllToTargetButton(cut).ClickAsync(new MouseEventArgs());

        Assert.NotNull(captured);
        Assert.Equal(new[] { "Apple", "Apricot", "Banana" }, captured!);
    }

    // The "Move all" button must be disabled when the filtered source view is
    // empty even though the unfiltered source still has items, so it can't move
    // a phantom empty set.
    [Fact]
    public void MoveAll_disabled_when_filtered_source_is_empty()
    {
        var cut = _ctx.Render<L.PickList<string>>(p => p
            .Add(c => c.Items, new List<string> { "Apple", "Apricot", "Banana" })
            .Add(c => c.SelectedItems, new List<string>()));

        SourceSearchInput(cut).Input("zzz-no-match");

        Assert.True(MoveAllToTargetButton(cut).HasAttribute("disabled"));
    }
}
