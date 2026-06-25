using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Select;

/// <summary>
/// Regression tests for battle-wave1 finding #23 (state-on-data-change):
/// the keyboard highlight (<c>_focusedIndex</c>) is a raw POSITION into the nav
/// list, and the rendered highlight + what Enter/Space selects are read from
/// <c>navItems[_focusedIndex]</c>. An external Items refresh that REORDERS or
/// REMOVES rows while the popover is open used to leave that index pointing at a
/// DIFFERENT item, silently retargeting the highlight and the Enter selection.
///
/// The fix anchors focus by the option's VALUE (re-resolved in OnParametersSet)
/// so the highlight follows its row across a data change and drops to "no
/// highlight" only when the value genuinely disappears.
///
/// Mirrors SelectKeyboardNavTests' data-bound render + bg-accent assertions.
/// </summary>
public class SelectFocusOnDataChangeTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SelectFocusOnDataChangeTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static readonly RenderFragment Child = b =>
    {
        b.OpenComponent<L.SelectTrigger>(0);
        b.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "Choose...")));
        b.CloseComponent();
        b.OpenComponent<L.SelectContent>(2);
        b.CloseComponent();
    };

    private IRenderedComponent<L.Select> RenderOpen(
        IEnumerable<object> items,
        EventCallback<string?>? valueChanged = null)
        => _ctx.Render<L.Select>(p =>
        {
            p.Add(s => s.Open, true);
            p.Add(s => s.Items, items);
            p.Add(s => s.ChildContent, Child);
            if (valueChanged.HasValue)
                p.Add(s => s.ValueChanged, valueChanged.Value);
        });

    private static AngleSharp.Dom.IElement? FindOption(IRenderedComponent<L.Select> cut, string text)
        => cut.FindAll("button[role='option']").FirstOrDefault(b => b.TextContent.Contains(text));

    [Fact]
    public void Highlight_Follows_The_Item_When_Items_Reorder_While_Open()
    {
        // ArrowDown twice → highlight on "banana" (index 1), _focusedValue == "banana".
        var cut = RenderOpen(new object[] { "apple", "banana", "cherry" });
        var listbox = cut.Find("[role='listbox']");
        listbox.KeyDown("ArrowDown");
        listbox.KeyDown("ArrowDown");
        Assert.Contains("bg-accent", FindOption(cut, "banana")!.ClassList);

        // External refresh REORDERS the list: "banana" moves from index 1 → index 0.
        // Without the fix, index 1 now points at "cherry" → highlight would jump to
        // the wrong row. With the by-value re-anchor the highlight stays on "banana".
        cut.Render(p => p.Add(s => s.Items, new object[] { "banana", "cherry", "apple" }));

        Assert.Contains("bg-accent", FindOption(cut, "banana")!.ClassList);
        Assert.DoesNotContain("bg-accent", FindOption(cut, "cherry")!.ClassList);
    }

    [Fact]
    public void Enter_Selects_The_Anchored_Value_After_A_Reorder()
    {
        // Same repro, asserting the Enter target (FocusedItemValue) — the half of the
        // bug that silently SELECTS the wrong item, not just mis-highlights it.
        string? selected = null;
        var cb = EventCallback.Factory.Create<string?>(_ctx, (string? v) => selected = v);
        var cut = RenderOpen(new object[] { "apple", "banana", "cherry" }, valueChanged: cb);

        var listbox = cut.Find("[role='listbox']");
        listbox.KeyDown("ArrowDown");
        listbox.KeyDown("ArrowDown"); // focus "banana" (index 1)

        cut.Render(p => p.Add(s => s.Items, new object[] { "banana", "cherry", "apple" }));

        // Selecting closes the popover, which unmounts the listbox mid-dispatch.
        try { cut.Find("[role='listbox']").KeyDown("Enter"); } catch (ArgumentException) { }

        Assert.Equal("banana", selected);
    }

    [Fact]
    public void Highlight_Clears_When_The_Focused_Item_Is_Removed()
    {
        // Highlight "banana", then refresh Items WITHOUT it. The highlight must drop
        // (no option highlighted) rather than the stale index landing on whatever row
        // now sits at index 1.
        var cut = RenderOpen(new object[] { "apple", "banana", "cherry" });
        var listbox = cut.Find("[role='listbox']");
        listbox.KeyDown("ArrowDown");
        listbox.KeyDown("ArrowDown"); // focus "banana"
        Assert.Contains("bg-accent", FindOption(cut, "banana")!.ClassList);

        cut.Render(p => p.Add(s => s.Items, new object[] { "apple", "cherry", "date" }));

        Assert.All(cut.FindAll("button[role='option']"),
            opt => Assert.DoesNotContain("bg-accent", opt.ClassList));
    }

    [Fact]
    public void Highlight_Restores_After_An_Empty_Then_Refill_Flicker()
    {
        // An async Items refresh that flickers empty → refilled must not lose the
        // in-progress highlight: it is hidden while the list is empty and re-anchors
        // onto the same value when the rows return.
        var cut = RenderOpen(new object[] { "apple", "banana", "cherry" });
        var listbox = cut.Find("[role='listbox']");
        listbox.KeyDown("ArrowDown");
        listbox.KeyDown("ArrowDown"); // focus "banana"

        // Transient empty (loading) — nothing to highlight onto.
        cut.Render(p => p.Add(s => s.Items, Array.Empty<object>()));
        Assert.Empty(cut.FindAll("button[role='option']"));

        // Refill (reordered for good measure) — highlight re-anchors onto "banana".
        cut.Render(p => p.Add(s => s.Items, new object[] { "cherry", "banana", "apple" }));
        Assert.Contains("bg-accent", FindOption(cut, "banana")!.ClassList);
    }
}
