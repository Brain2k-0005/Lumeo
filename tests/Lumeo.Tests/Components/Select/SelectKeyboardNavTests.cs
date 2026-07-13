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

    // --- Codex P2: the search input's keydown must not ALSO fire the listbox's own
    //     handler via bubbling — HandleKeyDown was invoked twice per keystroke ---

    [Fact]
    public void Enter_On_The_Search_Input_Itself_Selects_Only_Once()
    {
        // Dispatching on the LISTBOX (as the other tests above do) never exercised the
        // bubbling path — the bug only reproduces when the keydown originates on the
        // INPUT, which bubbles to the outer listbox div's own @onkeydown="HandleKeyDown"
        // unless stopped. Without the fix, Enter here invoked Context.OnSelect twice for
        // the same FocusedItemValue (ValueChanged fired twice for the same value).
        var callCount = 0;
        string? selected = null;
        var cb = EventCallback.Factory.Create<string?>(_ctx, (string? v) => { callCount++; selected = v; });
        var cut = RenderSearchableComposition(valueChanged: cb);

        var input = cut.Find("input");
        input.KeyDown("ArrowDown"); // focus "apple"
        try { input.KeyDown("Enter"); } catch (ArgumentException) { } // selecting closes the popover

        Assert.Equal("apple", selected);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void Enter_On_The_Search_Input_In_Multiple_Mode_Adds_The_Value_Once()
    {
        // Multiple mode toggles membership and never closes the popover, so a double
        // invocation is directly observable: the first call ADDS the value, the bubbled
        // call immediately REMOVES it again — keyboard selection appeared to do nothing.
        List<string>? selected = null;
        var cb = EventCallback.Factory.Create<List<string>?>(_ctx, (List<string>? v) => selected = v);
        var cut = _ctx.Render<L.Select>(p =>
        {
            p.Add(s => s.Open, true);
            p.Add(s => s.Multiple, true);
            p.Add(s => s.Searchable, true);
            p.Add(s => s.ValuesChanged, cb);
            p.Add(s => s.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<L.SelectTrigger>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "Choose...")));
                b.CloseComponent();
                b.OpenComponent<L.SelectContent>(2);
                b.AddAttribute(3, "ChildContent", (RenderFragment)(c =>
                {
                    c.OpenComponent<L.SelectItem>(0);
                    c.AddAttribute(1, "Value", "apple");
                    c.AddAttribute(2, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Apple")));
                    c.CloseComponent();
                }));
                b.CloseComponent();
            }));
        });

        var input = cut.Find("input");
        input.KeyDown("ArrowDown"); // focus "apple"
        input.KeyDown("Enter");     // popover stays open in Multiple mode — no unmount

        Assert.NotNull(selected);
        Assert.Equal(new[] { "apple" }, selected);
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

        var registration = Assert.Single(_ctx.JSInterop.Invocations,
            i => i.Identifier == "registerClickOutside");
        Assert.Equal(triggerId, registration.Arguments[1]);
    }

    // --- Seed focus to the selected value on open (Radix parity) ---

    private IRenderedComponent<IComponent> RenderDataBoundWithValue(string value, IEnumerable<object> items)
        => _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Select>(0);
            builder.AddAttribute(1, "Open", true);
            builder.AddAttribute(2, "Value", value);
            builder.AddAttribute(3, "Items", items);
            builder.AddAttribute(4, "ChildContent", (RenderFragment)(b =>
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
    public void Open_Seeds_Focus_To_Selected_Value_Without_Keypress()
    {
        var cut = RenderDataBoundWithValue("banana", new object[] { "apple", "banana", "cherry" });

        var banana = FindOption(cut, "banana");
        var apple = FindOption(cut, "apple");
        Assert.NotNull(banana);
        Assert.Contains("bg-accent", banana!.ClassList);      // selected option highlighted on open
        Assert.DoesNotContain("bg-accent", apple!.ClassList); // not the top item
    }

    [Fact]
    public void Open_Seeded_ArrowDown_Moves_From_Selected_Not_Top()
    {
        // With seeding, ArrowDown from a Select whose value is the FIRST item must
        // land on the SECOND. Without seeding (_focusedIndex == -1) ArrowDown would
        // land on the first — so a green "banana" here proves the seed ran.
        var cut = RenderDataBoundWithValue("apple", new object[] { "apple", "banana", "cherry" });

        cut.Find("[role='listbox']").KeyDown("ArrowDown");

        var banana = FindOption(cut, "banana");
        Assert.NotNull(banana);
        Assert.Contains("bg-accent", banana!.ClassList);
    }

    [Fact]
    public void Open_Seeds_Focus_To_Selected_In_Composition_Mode()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Select>(0);
            builder.AddAttribute(1, "Open", true);
            builder.AddAttribute(2, "Value", "blueberry");
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(b =>
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
                        c.AddAttribute(seq + 2, "ChildContent", (RenderFragment)(i => i.AddContent(0, label)));
                        c.CloseComponent();
                    }
                    Item(0, "apple", "Apple");
                    Item(3, "banana", "Banana");
                    Item(6, "blueberry", "Blueberry");
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var blueberry = FindOption(cut, "Blueberry");
        Assert.NotNull(blueberry);
        Assert.Contains("bg-accent", blueberry!.ClassList);
    }

    // --- preventDefault on the listbox nav keys (no page scroll) ---

    [Fact]
    public void Open_Registers_PreventDefault_For_Listbox_Nav_Keys()
    {
        var cut = RenderDataBound(new object[] { "apple", "banana" });

        var contentId = cut.Find("[role='listbox']").GetAttribute("id");
        var reg = Assert.Single(_ctx.JSInterop.Invocations,
            i => i.Identifier == "registerPreventDefaultKeys");
        Assert.Equal(contentId, reg.Arguments[0]);

        var rules = Lumeo.Tests.Helpers.PreventDefaultRuleCapture.Parse(reg.Arguments[1]);
        var keys = rules.Select(r => r.Key).ToList();
        Assert.Contains("ArrowDown", keys);
        Assert.Contains("ArrowUp", keys);
        Assert.Contains("Home", keys);
        Assert.Contains("End", keys);
        // SkipEditable keeps the search input's caret keys alive when Searchable.
        Assert.All(rules, r => Assert.True(r.SkipEditable));
    }

    [Fact]
    public void Open_With_Late_Loading_Items_Seeds_When_They_Arrive()
    {
        // The seed is deferred until the option list exists: a Select opened while
        // its Items are still loading must re-seed on the render where options
        // appear — not consume the one-shot early and never highlight the value.
        RenderFragment child = b =>
        {
            b.OpenComponent<L.SelectTrigger>(0);
            b.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "Choose...")));
            b.CloseComponent();
            b.OpenComponent<L.SelectContent>(2);
            b.CloseComponent();
        };

        var cut = _ctx.Render<L.Select>(p => p
            .Add(s => s.Open, true)
            .Add(s => s.Value, "banana")
            .Add(s => s.Items, Array.Empty<object>())
            .Add(s => s.ChildContent, child));

        Assert.Empty(cut.FindAll("button[role='option']")); // nothing to seed onto yet

        cut.Render(p => p
            .Add(s => s.Items, new object[] { "apple", "banana", "cherry" }));

        var banana = FindOption(cut, "banana");
        Assert.NotNull(banana);
        Assert.Contains("bg-accent", banana!.ClassList);
    }

    // --- Focus management on open/close (B3/B4) ---

    [Fact]
    public void Open_NonSearchable_Moves_Focus_Into_The_Listbox_And_Saves_Trigger_Focus()
    {
        // Without this, focus stays on the trigger (a sibling), so the listbox
        // @onkeydown + the page-scroll preventDefault never receive keys.
        var cut = RenderDataBound(new object[] { "apple", "banana" });
        var contentId = cut.Find("[role='listbox']").GetAttribute("id");

        Assert.Contains(_ctx.JSInterop.Invocations, i => i.Identifier == "saveFocus");
        Assert.Contains(_ctx.JSInterop.Invocations,
            i => i.Identifier == "focusElementById" && (i.Arguments[0] as string) == contentId);
    }

    [Fact]
    public void Open_Searchable_Moves_Focus_To_The_Search_Input()
    {
        var cut = RenderDataBound(new object[] { "apple", "banana" }, searchable: true);
        var contentId = cut.Find("[role='listbox']").GetAttribute("id");

        Assert.Contains(_ctx.JSInterop.Invocations,
            i => i.Identifier == "focusElementById" && (i.Arguments[0] as string) == $"{contentId}-search");
    }

    [Fact]
    public void Close_Restores_Focus_To_The_Trigger()
    {
        RenderFragment child = b =>
        {
            b.OpenComponent<L.SelectTrigger>(0);
            b.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "Choose")));
            b.CloseComponent();
            b.OpenComponent<L.SelectContent>(2);
            b.CloseComponent();
        };
        var items = new object[] { "apple", "banana" };
        var cut = _ctx.Render<L.Select>(p => p
            .Add(s => s.Open, true).Add(s => s.Items, items).Add(s => s.ChildContent, child));

        cut.Render(p => p
            .Add(s => s.Open, false).Add(s => s.Items, items).Add(s => s.ChildContent, child));

        Assert.Contains(_ctx.JSInterop.Invocations, i => i.Identifier == "restoreFocus");
    }

    // --- Space selects the focused option (APG listbox) ---

    [Fact]
    public void Space_Selects_The_Focused_Option_When_Not_Searchable()
    {
        string? selected = null;
        var cb = EventCallback.Factory.Create<string?>(_ctx, (string? v) => selected = v);
        var cut = RenderDataBound(new object[] { "apple", "banana", "cherry" }, valueChanged: cb);

        cut.Find("[role='listbox']").KeyDown("ArrowDown"); // focus apple
        // Selecting closes the popover, which unmounts the listbox mid-dispatch.
        try { cut.Find("[role='listbox']").KeyDown(" "); } catch (ArgumentException) { }

        Assert.Equal("apple", selected);
    }

    [Fact]
    public void Space_Does_Not_Select_When_Searchable()
    {
        // When Searchable, Space belongs to the filter input — it must not select.
        string? selected = null;
        var cb = EventCallback.Factory.Create<string?>(_ctx, (string? v) => selected = v);
        var cut = RenderDataBound(new object[] { "apple", "banana" }, searchable: true, valueChanged: cb);

        cut.Find("[role='listbox']").KeyDown("ArrowDown"); // focus apple
        cut.Find("[role='listbox']").KeyDown(" ");

        Assert.Null(selected);
    }
}
