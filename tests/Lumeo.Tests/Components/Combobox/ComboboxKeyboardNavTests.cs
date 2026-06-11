using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Combobox;

/// <summary>
/// Regression tests for Combobox keyboard interaction:
/// 1. Data-bound mode (Items parameter) — bound rows render inline without component
///    instances, so the nav list must be derived from the same filtered/grouped sequence
///    the content renders (previously the registry stayed empty and arrows/Enter were dead).
///    Disabled items must be skipped entirely.
/// 2. Async search (OnSearchAsync) must not orphan still-mounted items from the registry —
///    previously OnSearchChanged cleared it, killing keyboard nav AND mis-reporting
///    ItemCount as 0, which showed ComboboxEmpty underneath live results.
/// 3. Multi-select Backspace must remove the most recently added chip — HashSet
///    enumeration order is undefined, so Values.Last() removed an arbitrary one.
/// 4. Click-outside must exclude the combobox wrapper (input + chips) so interacting
///    with the input doesn't race close-then-reopen.
/// </summary>
public class ComboboxKeyboardNavTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ComboboxKeyboardNavTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static AngleSharp.Dom.IElement? FindOption(IRenderedComponent<IComponent> cut, string text)
        => cut.FindAll("button[role='option']").FirstOrDefault(b => b.TextContent.Contains(text));

    private static void AddItem(RenderTreeBuilder rb, int seq, string value, string label)
    {
        rb.OpenComponent<L.ComboboxItem>(seq);
        rb.AddAttribute(seq + 1, "Value", value);
        rb.AddAttribute(seq + 2, "ChildContent", (RenderFragment)(i => i.AddContent(0, label)));
        rb.CloseComponent();
    }

    // --- Data-bound keyboard navigation ---

    private IRenderedComponent<IComponent> RenderDataBound(
        IEnumerable<object> items,
        Func<object, bool>? itemDisabled = null,
        EventCallback<string>? valueChanged = null)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Combobox>(0);
            builder.AddAttribute(1, "Open", true);
            builder.AddAttribute(2, "Items", items);
            if (itemDisabled != null)
                builder.AddAttribute(3, "ItemDisabled", itemDisabled);
            if (valueChanged.HasValue)
                builder.AddAttribute(4, "ValueChanged", valueChanged.Value);
            builder.AddAttribute(5, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.ComboboxInput>(0);
                b.CloseComponent();

                b.OpenComponent<L.ComboboxContent>(2);
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    [Fact]
    public void DataBound_ArrowDown_Focuses_First_Enabled_Item()
    {
        var cut = RenderDataBound(new object[] { "apple", "banana", "cherry" });

        cut.Find("[role='listbox']").KeyDown("ArrowDown");

        var apple = FindOption(cut, "apple");
        Assert.NotNull(apple);
        Assert.Contains("bg-accent", apple!.ClassList);
    }

    [Fact]
    public void DataBound_ArrowDown_Skips_Disabled_Item()
    {
        // "apple" (first item) is disabled — ArrowDown must land on "banana" directly.
        var cut = RenderDataBound(
            new object[] { "apple", "banana", "cherry" },
            itemDisabled: it => (string)it == "apple");

        cut.Find("[role='listbox']").KeyDown("ArrowDown");

        var apple = FindOption(cut, "apple");
        var banana = FindOption(cut, "banana");
        Assert.NotNull(banana);
        Assert.Contains("bg-accent", banana!.ClassList);
        Assert.DoesNotContain("bg-accent", apple!.ClassList);
    }

    [Fact]
    public void DataBound_Enter_Selects_Focused_Item_And_Never_A_Disabled_One()
    {
        string? selected = null;
        var cb = EventCallback.Factory.Create<string>(_ctx, (string v) => selected = v);
        var cut = RenderDataBound(
            new object[] { "apple", "banana", "cherry" },
            itemDisabled: it => (string)it == "apple",
            valueChanged: cb);

        cut.Find("[role='listbox']").KeyDown("ArrowDown");
        // Selecting closes the popover, which unmounts the listbox mid-dispatch.
        try { cut.Find("[role='listbox']").KeyDown("Enter"); } catch (ArgumentException) { }

        Assert.Equal("banana", selected);
    }

    [Fact]
    public void DataBound_ArrowDown_From_Input_Navigates_Too()
    {
        // The text input forwards the same nav keys as the listbox.
        var cut = RenderDataBound(new object[] { "apple", "banana" });

        cut.Find("input").KeyDown("ArrowDown");

        var apple = FindOption(cut, "apple");
        Assert.NotNull(apple);
        Assert.Contains("bg-accent", apple!.ClassList);
    }

    // --- Async search: registry must survive, no spurious empty state ---

    private IRenderedComponent<IComponent> RenderAsyncSearchComposition(
        EventCallback<string>? valueChanged = null)
    {
        var onSearchAsync = EventCallback.Factory.Create<string>(_ctx, (string _) => { });
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Combobox>(0);
            builder.AddAttribute(1, "Open", true);
            builder.AddAttribute(2, "OnSearchAsync", onSearchAsync);
            if (valueChanged.HasValue)
                builder.AddAttribute(3, "ValueChanged", valueChanged.Value);
            builder.AddAttribute(4, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.ComboboxInput>(0);
                b.CloseComponent();

                b.OpenComponent<L.ComboboxContent>(2);
                b.AddAttribute(3, "ChildContent", (RenderFragment)(c =>
                {
                    AddItem(c, 0, "apple", "Apple");
                    AddItem(c, 10, "banana", "Banana");
                    c.OpenComponent<L.ComboboxEmpty>(20);
                    c.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    [Fact]
    public void AsyncSearch_With_Matches_Does_Not_Show_Empty_State_Under_Live_Items()
    {
        var cut = RenderAsyncSearchComposition();

        // "an" matches Banana only; Apple unregisters itself, Banana stays mounted
        // and registered — ItemCount must remain 1, keeping ComboboxEmpty hidden.
        cut.Find("input").Input("an");

        Assert.NotNull(FindOption(cut, "Banana"));
        Assert.DoesNotContain("No results found", cut.Markup);
    }

    [Fact]
    public void AsyncSearch_Then_ArrowDown_Still_Navigates_And_Enter_Selects()
    {
        string? selected = null;
        var cb = EventCallback.Factory.Create<string>(_ctx, (string v) => selected = v);
        var cut = RenderAsyncSearchComposition(valueChanged: cb);

        cut.Find("input").Input("an");
        cut.Find("input").KeyDown("ArrowDown");

        var banana = FindOption(cut, "Banana");
        Assert.NotNull(banana);
        Assert.Contains("bg-accent", banana!.ClassList);

        try { cut.Find("input").KeyDown("Enter"); } catch (ArgumentException) { }
        Assert.Equal("banana", selected);
    }

    // --- Multi-select Backspace removes the most recently added chip ---

    private IRenderedComponent<IComponent> RenderMultiComposition(
        HashSet<string> values,
        EventCallback<HashSet<string>>? valuesChanged = null)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Combobox>(0);
            builder.AddAttribute(1, "Open", true);
            builder.AddAttribute(2, "Multiple", true);
            builder.AddAttribute(3, "Values", values);
            if (valuesChanged.HasValue)
                builder.AddAttribute(4, "ValuesChanged", valuesChanged.Value);
            builder.AddAttribute(5, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.ComboboxInput>(0);
                b.CloseComponent();

                b.OpenComponent<L.ComboboxContent>(2);
                b.AddAttribute(3, "ChildContent", (RenderFragment)(c =>
                {
                    AddItem(c, 0, "a", "Alpha");
                    AddItem(c, 10, "b", "Beta");
                    AddItem(c, 20, "c", "Gamma");
                    AddItem(c, 30, "d", "Delta");
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    private static void ClickOption(IRenderedComponent<IComponent> cut, string label)
    {
        var option = FindOption(cut, label);
        Assert.NotNull(option);
        try { option!.Click(); } catch (ArgumentException) { }
    }

    [Fact]
    public void Backspace_Removes_Most_Recently_Added_Chip()
    {
        HashSet<string>? captured = null;
        var cb = EventCallback.Factory.Create<HashSet<string>>(_ctx, (HashSet<string> v) => captured = v);
        var cut = RenderMultiComposition(new HashSet<string>(), valuesChanged: cb);

        // Select a, b, c; deselect a; select d. The HashSet reuses a's freed slot
        // for d, so enumeration order is no longer insertion order — Values.Last()
        // would return "c" here, but the most recently added chip is "d".
        ClickOption(cut, "Alpha");
        ClickOption(cut, "Beta");
        ClickOption(cut, "Gamma");
        ClickOption(cut, "Alpha"); // toggle off
        ClickOption(cut, "Delta");

        try { cut.Find("input").KeyDown("Backspace"); } catch (ArgumentException) { }

        Assert.NotNull(captured);
        Assert.DoesNotContain("d", captured!);
        Assert.Equal(new[] { "b", "c" }, captured!.Order().ToArray());
    }

    [Fact]
    public void Backspace_Repeated_Removes_Chips_In_Reverse_Insertion_Order()
    {
        var removed = new List<string>();
        HashSet<string>? current = new();
        var cb = EventCallback.Factory.Create<HashSet<string>>(_ctx, (HashSet<string> v) =>
        {
            if (current is not null)
                removed.AddRange(current.Except(v));
            current = new HashSet<string>(v);
        });
        var cut = RenderMultiComposition(new HashSet<string>(), valuesChanged: cb);

        ClickOption(cut, "Alpha");
        ClickOption(cut, "Gamma");
        ClickOption(cut, "Beta");
        removed.Clear();

        try { cut.Find("input").KeyDown("Backspace"); } catch (ArgumentException) { }
        try { cut.Find("input").KeyDown("Backspace"); } catch (ArgumentException) { }

        Assert.Equal(new[] { "b", "c" }, removed);
    }

    [Fact]
    public void Backspace_With_Custom_Comparer_Still_Removes_Most_Recent()
    {
        // PR #165 guarantees the bound set's comparer survives — the insertion-order
        // mirror must honour it too (values may differ in case from what's stored).
        HashSet<string>? captured = null;
        var cb = EventCallback.Factory.Create<HashSet<string>>(_ctx, (HashSet<string> v) => captured = v);
        var seed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "React" };
        var cut = RenderMultiComposition(seed, valuesChanged: cb);

        ClickOption(cut, "Alpha");
        try { cut.Find("input").KeyDown("Backspace"); } catch (ArgumentException) { }

        Assert.NotNull(captured);
        Assert.Contains("React", captured!);
        Assert.DoesNotContain("a", captured!);
        Assert.Same(StringComparer.OrdinalIgnoreCase, captured!.Comparer);
    }

    // --- Click outside must exclude the combobox wrapper (input + chips) ---

    [Fact]
    public void ClickOutside_Registration_Excludes_Wrapper_Element()
    {
        var cut = RenderDataBound(new object[] { "apple" });

        var wrapperId = cut.Find("div.relative").GetAttribute("id");
        Assert.False(string.IsNullOrEmpty(wrapperId));

        var registration = Assert.Single(_ctx.JSInterop.Invocations
            .Where(i => i.Identifier == "registerClickOutside"));
        Assert.Equal(wrapperId, registration.Arguments[1]);
    }
}
