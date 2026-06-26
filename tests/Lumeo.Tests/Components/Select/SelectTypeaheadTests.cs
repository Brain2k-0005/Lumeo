using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Select;

/// <summary>
/// G22c — type-to-jump (typeahead) for the non-searchable Select listbox, matching
/// Radix/shadcn: printable keys move the highlighted option to the first whose text
/// starts with the accumulated query; a repeated single key cycles; multi-char
/// refines; and when Searchable the keystrokes belong to the search input instead.
/// </summary>
public class SelectTypeaheadTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public SelectTypeaheadTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static AngleSharp.Dom.IElement? FindOption(IRenderedComponent<IComponent> cut, string text)
        => cut.FindAll("button[role='option']").FirstOrDefault(b => b.TextContent.Contains(text));

    private static void Type(IRenderedComponent<IComponent> cut, string ch)
        => cut.Find("[role='listbox']").KeyDown(new KeyboardEventArgs { Key = ch });

    // --- Data-bound mode ---

    private IRenderedComponent<IComponent> RenderBound(
        IEnumerable<object> items, bool searchable = false, Func<object, bool>? disabled = null)
        => _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Select>(0);
            builder.AddAttribute(1, "Open", true);
            builder.AddAttribute(2, "Items", items);
            if (searchable) builder.AddAttribute(3, "Searchable", true);
            if (disabled != null) builder.AddAttribute(4, "ItemDisabled", disabled);
            builder.AddAttribute(5, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SelectTrigger>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "Choose...")));
                b.CloseComponent();
                b.OpenComponent<L.SelectContent>(2);
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

    [Fact]
    public void Printable_Key_Jumps_To_The_First_Matching_Option()
    {
        var cut = RenderBound(new object[] { "apple", "banana", "cherry" });

        Type(cut, "b");

        Assert.Contains("bg-accent", FindOption(cut, "banana")!.ClassList);
        Assert.DoesNotContain("bg-accent", FindOption(cut, "apple")!.ClassList);
    }

    [Fact]
    public void Multi_Char_Query_Refines_The_Match()
    {
        // "ch" must skip "cherry"? No — "ch" matches "cherry" and "chocolate"; the
        // query refines to the first item starting with the full "ch".
        var cut = RenderBound(new object[] { "cobalt", "cherry", "chocolate" });

        Type(cut, "c");   // -> cobalt (first c)
        Assert.Contains("bg-accent", FindOption(cut, "cobalt")!.ClassList);

        Type(cut, "h");   // "ch" -> cherry (first starting with "ch")
        Assert.Contains("bg-accent", FindOption(cut, "cherry")!.ClassList);
        Assert.DoesNotContain("bg-accent", FindOption(cut, "cobalt")!.ClassList);
    }

    [Fact]
    public void Repeated_Key_Cycles_Through_Same_Initial_Items()
    {
        var cut = RenderBound(new object[] { "apple", "apricot", "banana", "avocado" });

        Type(cut, "a");   // apple
        Assert.Contains("bg-accent", FindOption(cut, "apple")!.ClassList);

        Type(cut, "a");   // -> apricot (cycles to next "a")
        Assert.Contains("bg-accent", FindOption(cut, "apricot")!.ClassList);

        Type(cut, "a");   // -> avocado (next "a" after apricot, skipping banana)
        Assert.Contains("bg-accent", FindOption(cut, "avocado")!.ClassList);
    }

    [Fact]
    public void No_Match_Leaves_The_Highlight_Unchanged()
    {
        var cut = RenderBound(new object[] { "apple", "banana" });

        Type(cut, "b");
        Assert.Contains("bg-accent", FindOption(cut, "banana")!.ClassList);

        Type(cut, "z");   // nothing starts with "bz"/"z" -> highlight stays on banana
        Assert.Contains("bg-accent", FindOption(cut, "banana")!.ClassList);
    }

    [Fact]
    public void Bound_Typeahead_Skips_Disabled_Items()
    {
        // The only "b" item is disabled -> it's not in the nav set, so "b" finds no
        // match and the highlight doesn't move onto the disabled row.
        var cut = RenderBound(
            new object[] { "apple", "banana", "cherry" },
            disabled: it => (string)it == "banana");

        Type(cut, "b");

        Assert.DoesNotContain("bg-accent", FindOption(cut, "banana")!.ClassList);
    }

    [Fact]
    public void Searchable_Select_Does_Not_Typeahead_From_The_Listbox()
    {
        // When Searchable, printable keys belong to the search input; the listbox
        // typeahead must stay inert so it doesn't fight the filter.
        var cut = RenderBound(new object[] { "apple", "banana", "cherry" }, searchable: true);

        Type(cut, "b");

        // No option should get highlighted by the (suppressed) typeahead path.
        Assert.All(cut.FindAll("button[role='option']"),
            o => Assert.DoesNotContain("bg-accent", o.ClassList));
    }

    // --- Composition mode (SelectItem, text may differ from value) ---

    [Fact]
    public void Composition_Typeahead_Matches_The_Item_Text_Not_The_Value()
    {
        // Value "us"/"uk" differ from the visible label; typeahead must match the
        // label ("United..."), via SearchValue/text, not the value.
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Select>(0);
            builder.AddAttribute(1, "Open", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SelectTrigger>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "Choose...")));
                b.CloseComponent();
                b.OpenComponent<L.SelectContent>(2);
                b.AddAttribute(3, "ChildContent", (RenderFragment)(c =>
                {
                    void Item(int seq, string val, string label)
                    {
                        c.OpenComponent<L.SelectItem>(seq);
                        c.AddAttribute(seq + 1, "Value", val);
                        c.AddAttribute(seq + 2, "SearchValue", label);
                        c.AddAttribute(seq + 3, "ChildContent", (RenderFragment)(i => i.AddContent(0, label)));
                        c.CloseComponent();
                    }
                    Item(0, "ca", "Canada");
                    Item(4, "uk", "United Kingdom");
                    Item(8, "us", "United States");
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Type(cut, "u");   // -> United Kingdom (first "United…")

        Assert.Contains("bg-accent", FindOption(cut, "United Kingdom")!.ClassList);
        Assert.DoesNotContain("bg-accent", FindOption(cut, "Canada")!.ClassList);
    }
}
