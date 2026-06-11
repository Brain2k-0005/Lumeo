using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Select;

/// <summary>
/// Regression tests for Select keyboard navigation:
/// 1. Data-bound mode (Items parameter) — bound rows render inline without component
///    instances, so the nav list must be derived from the same filtered/grouped sequence
///    the content renders (previously the registry stayed empty and arrows/Enter were dead).
///    Disabled items must be skipped entirely.
/// 2. Searching must not orphan still-visible items from the nav registry (previously
///    OnSearchChanged cleared the registry while visible SelectItems kept their
///    _registered flag, killing keyboard nav until close/reopen).
/// 3. Click-outside must exclude the trigger element so a click on the open trigger
///    closes the popover instead of racing close-then-reopen.
/// </summary>
public class SelectKeyboardNavTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SelectKeyboardNavTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderDataBound(
        IEnumerable<object> items,
        Func<object, bool>? itemDisabled = null,
        Func<object, string?>? itemGroup = null,
        bool searchable = false,
        EventCallback<string?>? valueChanged = null)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Select>(0);
            builder.AddAttribute(1, "Open", true);
            builder.AddAttribute(2, "Items", items);
            if (itemDisabled != null)
                builder.AddAttribute(3, "ItemDisabled", itemDisabled);
            if (itemGroup != null)
                builder.AddAttribute(4, "ItemGroup", itemGroup);
            if (searchable)
                builder.AddAttribute(5, "Searchable", true);
            if (valueChanged.HasValue)
                builder.AddAttribute(6, "ValueChanged", valueChanged.Value);
            builder.AddAttribute(7, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SelectTrigger>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "Choose...")));
                b.CloseComponent();

                b.OpenComponent<L.SelectContent>(2);
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    private static AngleSharp.Dom.IElement? FindOption(IRenderedComponent<IComponent> cut, string text)
        => cut.FindAll("button[role='option']").FirstOrDefault(b => b.TextContent.Contains(text));

    // --- Data-bound keyboard navigation ---

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
        var cb = EventCallback.Factory.Create<string?>(_ctx, (string? v) => selected = v);
        var cut = RenderDataBound(
            new object[] { "apple", "banana", "cherry" },
            itemDisabled: it => (string)it == "apple",
            valueChanged: cb);

        var listbox = cut.Find("[role='listbox']");
        listbox.KeyDown("ArrowDown");
        // Selecting closes the popover, which unmounts the listbox mid-dispatch.
        try { cut.Find("[role='listbox']").KeyDown("Enter"); } catch (ArgumentException) { }

        Assert.Equal("banana", selected);
    }

    [Fact]
    public void DataBound_End_Focuses_Last_Enabled_Item()
    {
        // "cherry" (last item) is disabled — End must clamp to "banana".
        var cut = RenderDataBound(
            new object[] { "apple", "banana", "cherry" },
            itemDisabled: it => (string)it == "cherry");

        cut.Find("[role='listbox']").KeyDown("End");

        var banana = FindOption(cut, "banana");
        Assert.NotNull(banana);
        Assert.Contains("bg-accent", banana!.ClassList);
    }

    [Fact]
    public void DataBound_Grouped_ArrowDown_Follows_Visual_Group_Order()
    {
        // Source order: apple, carrot, banana. Grouped rendering flattens to
        // apple, banana (Fruits) then carrot (Vegetables) — the nav order must
        // match what the user sees, not the source order.
        var cut = RenderDataBound(
            new object[] { "apple", "carrot", "banana" },
            itemGroup: it => (string)it == "carrot" ? "Vegetables" : "Fruits");

        var listbox = cut.Find("[role='listbox']");
        listbox.KeyDown("ArrowDown");
        listbox.KeyDown("ArrowDown");

        var banana = FindOption(cut, "banana");
        var carrot = FindOption(cut, "carrot");
        Assert.NotNull(banana);
        Assert.Contains("bg-accent", banana!.ClassList);
        Assert.DoesNotContain("bg-accent", carrot!.ClassList);
    }

    [Fact]
    public void DataBound_Search_Filters_Nav_To_Matching_Items()
    {
        string? selected = null;
        var cb = EventCallback.Factory.Create<string?>(_ctx, (string? v) => selected = v);
        var cut = RenderDataBound(
            new object[] { "apple", "banana", "blueberry" },
            searchable: true,
            valueChanged: cb);

        cut.Find("input").Input("blue");
        cut.Find("[role='listbox']").KeyDown("ArrowDown");
        try { cut.Find("[role='listbox']").KeyDown("Enter"); } catch (ArgumentException) { }

        Assert.Equal("blueberry", selected);
    }

    // --- Composition mode: searching must not kill keyboard nav ---

    private IRenderedComponent<IComponent> RenderSearchableComposition(EventCallback<string?>? valueChanged = null)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Select>(0);
            builder.AddAttribute(1, "Open", true);
            builder.AddAttribute(2, "Searchable", true);
            if (valueChanged.HasValue)
                builder.AddAttribute(3, "ValueChanged", valueChanged.Value);
            builder.AddAttribute(4, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SelectTrigger>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "Choose...")));
                b.CloseComponent();

                b.OpenComponent<L.SelectContent>(2);
                b.AddAttribute(3, "ChildContent", (RenderFragment)(c =>
                {
                    AddItem(c, 0, "apple", "Apple");
                    AddItem(c, 1, "banana", "Banana");
                    AddItem(c, 2, "blueberry", "Blueberry");
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        static void AddItem(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder rb, int seq, string value, string label)
        {
            rb.OpenComponent<L.SelectItem>(seq);
            rb.AddAttribute(seq + 1, "Value", value);
            rb.AddAttribute(seq + 2, "ChildContent", (RenderFragment)(i => i.AddContent(0, label)));
            rb.CloseComponent();
        }
    }

    [Fact]
    public void Search_Then_ArrowDown_Still_Navigates()
    {
        // Regression: OnSearchChanged used to clear the item registry while the
        // still-visible SelectItems kept their registered flag — they never
        // re-registered, so arrows/Enter were dead after the first keystroke.
        var cut = RenderSearchableComposition();

        cut.Find("input").Input("b");
        cut.Find("[role='listbox']").KeyDown("ArrowDown");

        var banana = FindOption(cut, "Banana");
        Assert.NotNull(banana);
        Assert.Contains("bg-accent", banana!.ClassList);
    }

    [Fact]
    public void Search_Then_Enter_Selects_Focused_Item()
    {
        string? selected = null;
        var cb = EventCallback.Factory.Create<string?>(_ctx, (string? v) => selected = v);
        var cut = RenderSearchableComposition(valueChanged: cb);

        cut.Find("input").Input("b");
        var listbox = cut.Find("[role='listbox']");
        listbox.KeyDown("ArrowDown");
        listbox.KeyDown("ArrowDown");
        try { cut.Find("[role='listbox']").KeyDown("Enter"); } catch (ArgumentException) { }

        Assert.Equal("blueberry", selected);
    }

    // --- Click outside must exclude the trigger ---

    [Fact]
    public void ClickOutside_Registration_Excludes_Trigger_Element()
    {
        // Without the trigger exclusion, mousedown on the open trigger fires the
        // outside-close handler AND the trigger's own click toggle — a race that
        // reopens the popover instead of closing it.
        var cut = RenderDataBound(new object[] { "apple" });

        var triggerId = cut.Find("button[role='combobox']").GetAttribute("id");
        Assert.False(string.IsNullOrEmpty(triggerId));

        var registration = Assert.Single(_ctx.JSInterop.Invocations
            .Where(i => i.Identifier == "registerClickOutside"));
        Assert.Equal(triggerId, registration.Arguments[1]);
    }
}
