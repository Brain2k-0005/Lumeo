using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Combobox;

/// <summary>
/// Regression test for battle-test finding #3 (high, state-on-data-change):
/// "Stale _focusedIndex survives an external Items refresh: highlight + Enter silently
/// retarget a different item."
///
/// In data-bound mode _focusedIndex is a POSITION into the bound nav list — bound rows
/// render inline (no component instance) and their ids are "{ContentId}-bound-{position}".
/// When a parent refreshes the Items reference (sort/filter/reorder/replace/empty→refill),
/// the item that previously sat at the focused position is now a different item, so the
/// roving highlight (bg-accent) and an Enter activation silently target the wrong row.
///
/// Fix: Combobox.OnParametersSet resets _focusedIndex to -1 when the Items reference
/// changes (ReferenceEquals false), mirroring ComboboxContent's _lastItems memoization.
/// </summary>
public class ComboboxFocusOnItemsChangeTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ComboboxFocusOnItemsChangeTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static AngleSharp.Dom.IElement? FindOption(IRenderedComponent<L.Combobox> cut, string text)
        => cut.FindAll("button[role='option']").FirstOrDefault(b => b.TextContent.Contains(text));

    private static bool AnyHighlighted(IRenderedComponent<L.Combobox> cut)
        => cut.FindAll("button[role='option']").Any(o => o.ClassList.Contains("bg-accent"));

    private static RenderFragment InputAndContent() => b =>
    {
        b.OpenComponent<L.ComboboxInput>(0);
        b.CloseComponent();
        b.OpenComponent<L.ComboboxContent>(2);
        b.CloseComponent();
    };

    private IRenderedComponent<L.Combobox> RenderDataBound(
        IEnumerable<object> items,
        EventCallback<string>? valueChanged = null)
    {
        return _ctx.Render<L.Combobox>(p =>
        {
            p.Add(c => c.Open, true);
            p.Add(c => c.Items, items);
            if (valueChanged.HasValue)
                p.Add(c => c.ValueChanged, valueChanged.Value);
            p.Add(c => c.ChildContent, InputAndContent());
        });
    }

    [Fact]
    public void Refreshing_Items_With_New_Reordered_Instance_Clears_Stale_Highlight()
    {
        // Focus "banana" (position 1) via ArrowDown twice.
        var cut = RenderDataBound(new object[] { "apple", "banana", "cherry" });
        cut.Find("[role='listbox']").KeyDown("ArrowDown"); // -> apple (idx 0)
        cut.Find("[role='listbox']").KeyDown("ArrowDown"); // -> banana (idx 1)

        var banana = FindOption(cut, "banana");
        Assert.NotNull(banana);
        Assert.Contains("bg-accent", banana!.ClassList);

        // Parent refreshes Items with a NEW, value-distinct, reordered instance. Position 1
        // is now "apple". Without the fix _focusedIndex stays 1 and the highlight jumps onto
        // "apple"; with the fix focus resets and nothing is highlighted.
        cut.Render(p => p.Add(x => x.Items, new object[] { "cherry", "apple", "banana" }));

        Assert.False(AnyHighlighted(cut));
    }

    [Fact]
    public void Refreshing_Items_Resets_Focus_So_Enter_Does_Not_Retarget()
    {
        string? selected = null;
        var cb = EventCallback.Factory.Create<string>(_ctx, (string v) => selected = v);

        var cut = RenderDataBound(new object[] { "apple", "banana", "cherry" }, valueChanged: cb);

        // Focus position 1 ("banana").
        cut.Find("[role='listbox']").KeyDown("ArrowDown");
        cut.Find("[role='listbox']").KeyDown("ArrowDown");

        // External refresh with a NEW instance whose position 1 is a different value.
        cut.Render(p => p.Add(x => x.Items, new object[] { "cherry", "date", "fig" }));

        // Enter must NOT silently select whatever now sits at the stale index.
        try { cut.Find("[role='listbox']").KeyDown("Enter"); } catch (ArgumentException) { }

        // Focus was reset (-1), so Enter has no focused row to commit — nothing selected,
        // and in particular the wrong item ("date") is not selected.
        Assert.Null(selected);
    }

    [Fact]
    public void Empty_Then_Refill_During_Async_Load_Does_Not_Resurrect_Stale_Highlight()
    {
        // Focus position 1 in the initial list.
        var cut = RenderDataBound(new object[] { "apple", "banana", "cherry" });
        cut.Find("[role='listbox']").KeyDown("ArrowDown");
        cut.Find("[role='listbox']").KeyDown("ArrowDown");
        Assert.Contains("bg-accent", FindOption(cut, "banana")!.ClassList);

        // Async refresh: list goes empty (loading flicker)...
        cut.Render(p => p.Add(x => x.Items, Array.Empty<object>()));
        // ...then refills with fresh results.
        cut.Render(p => p.Add(x => x.Items, new object[] { "grape", "kiwi", "lemon" }));

        // The stale index must not reappear as a highlight on the refilled list.
        Assert.False(AnyHighlighted(cut));
    }
}
