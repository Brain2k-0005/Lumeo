using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Combobox;

/// <summary>
/// Regression tests for two battle-test findings (medium, state-on-data-change):
///
/// #25 — "Multiple mode never clears SearchText / _focusedIndex / _items on close, so
/// reopen shows a stale filter and stale highlight." SetOpen's clear was gated on
/// `!value && !Multiple`, so a multi-select close left the previous session's search
/// filter (and roving highlight) hanging around on reopen. Fix: clear on close in both
/// single AND multiple mode — the committed selection lives in Values, not in this
/// transient view state.
///
/// #26 — "Composition-mode _items registry index is not reset when items reorder/replace
/// while open, desyncing the focus highlight." _focusedIndex is a POSITION into the
/// _items registry; when a ComboboxItem registers/unregisters (reorder/replace/show-hide
/// while open) the later slots shift but the index stays put, so the highlight silently
/// jumps to a different row. Fix: reset _focusedIndex to -1 on registry membership churn.
/// </summary>
public class ComboboxCloseAndReorderStateTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ComboboxCloseAndReorderStateTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static AngleSharp.Dom.IElement? FindOption<T>(IRenderedComponent<T> cut, string text)
        where T : IComponent
        => cut.FindAll("button[role='option']").FirstOrDefault(b => b.TextContent.Contains(text));

    private static bool AnyHighlighted<T>(IRenderedComponent<T> cut)
        where T : IComponent
        => cut.FindAll("button[role='option']").Any(o => o.ClassList.Contains("bg-accent"));

    // --- #25: multiple-mode close must clear the transient search/highlight state ---

    [Fact]
    public void Multiple_Close_Clears_SearchText_So_Reopen_Is_Not_Stale()
    {
        var values = new HashSet<string>();
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Combobox>(0);
            builder.AddAttribute(1, "Open", true);
            builder.AddAttribute(2, "Multiple", true);
            builder.AddAttribute(3, "Values", values);
            builder.AddAttribute(4, "Items", new object[] { "apple", "banana", "cherry" });
            builder.AddAttribute(5, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.ComboboxInput>(0);
                b.CloseComponent();
                b.OpenComponent<L.ComboboxContent>(2);
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        // Type a filter so SearchText is non-empty, then highlight a row.
        cut.Find("input").Input("an");
        Assert.Equal("an", cut.Find("input").GetAttribute("value"));
        cut.Find("[role='listbox']").KeyDown("ArrowDown");
        Assert.True(AnyHighlighted(cut));

        // Close via Escape — routes through SetOpen(false). With the old `&& !Multiple`
        // guard the search filter survived; the input still showed "an" on reopen.
        cut.Find("input").KeyDown("Escape");

        // SearchText must be cleared regardless of Multiple mode.
        Assert.Equal(string.Empty, cut.Find("input").GetAttribute("value"));
    }

    [Fact]
    public void Multiple_Close_Clears_Focus_Highlight()
    {
        var values = new HashSet<string>();
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Combobox>(0);
            builder.AddAttribute(1, "Open", true);
            builder.AddAttribute(2, "Multiple", true);
            builder.AddAttribute(3, "Values", values);
            builder.AddAttribute(4, "Items", new object[] { "apple", "banana", "cherry" });
            builder.AddAttribute(5, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.ComboboxInput>(0);
                b.CloseComponent();
                b.OpenComponent<L.ComboboxContent>(2);
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        cut.Find("[role='listbox']").KeyDown("ArrowDown"); // focus apple (idx 0)
        cut.Find("[role='listbox']").KeyDown("ArrowDown"); // focus banana (idx 1)
        Assert.True(AnyHighlighted(cut));

        // Close via Escape, then reopen with ArrowDown on the (closed) input — when closed
        // ArrowDown only re-opens (SetOpen(true)); it does NOT advance focus on the same
        // press. So any highlight present here is a STALE _focusedIndex that survived close.
        cut.Find("input").KeyDown("Escape");
        cut.Find("input").KeyDown("ArrowDown");

        // The stale highlight must NOT survive the close/reopen in multiple mode.
        Assert.False(AnyHighlighted(cut));
    }

    // --- #26: composition-mode registry churn resets the position-based focus ---

    // A host so we can flip which items render in the composition ChildContent and re-render
    // it via cut.Render on the HOST — without re-providing the Combobox's internal cascade.
    private bool _includeFirst = true;

    private RenderFragment CompositionFragment() => b =>
    {
        b.OpenComponent<L.ComboboxInput>(0);
        b.CloseComponent();
        b.OpenComponent<L.ComboboxContent>(2);
        b.AddAttribute(3, "ChildContent", (RenderFragment)(c =>
        {
            var seq = 0;
            if (_includeFirst)
                AddItem(c, seq, "alpha", "Alpha");
            AddItem(c, seq += 10, "beta", "Beta");
            AddItem(c, seq + 10, "gamma", "Gamma");
        }));
        b.CloseComponent();
    };

    private static void AddItem(RenderTreeBuilder rb, int seq, string value, string label)
    {
        rb.OpenComponent<L.ComboboxItem>(seq);
        rb.AddAttribute(seq + 1, "Value", value);
        rb.AddAttribute(seq + 2, "ChildContent", (RenderFragment)(i => i.AddContent(0, label)));
        rb.CloseComponent();
    }

    [Fact]
    public void Composition_Registry_Churn_While_Open_Clears_Stale_Highlight()
    {
        _includeFirst = true;
        var cut = _ctx.Render<L.Combobox>(p =>
        {
            p.Add(c => c.Open, true);
            p.Add(c => c.ChildContent, CompositionFragment());
        });

        // Focus Beta (registry position 1) via two ArrowDowns.
        cut.Find("[role='listbox']").KeyDown("ArrowDown"); // alpha (idx 0)
        cut.Find("[role='listbox']").KeyDown("ArrowDown"); // beta  (idx 1)
        Assert.Contains("bg-accent", FindOption(cut, "Beta")!.ClassList);

        // Remove the FIRST item while open — ComboboxItem.Dispose -> UnregisterItem compacts
        // _items, so position 1 now points at a different row. Without the fix _focusedIndex
        // stays 1 and the highlight jumps onto Gamma; with the fix focus resets to -1.
        _includeFirst = false;
        cut.Render(p =>
        {
            p.Add(c => c.Open, true);
            p.Add(c => c.ChildContent, CompositionFragment());
        });

        Assert.False(AnyHighlighted(cut));
    }
}
