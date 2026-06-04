using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Combobox;

public class ComboboxTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ComboboxTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderCombobox(
        bool isOpen = false,
        string? value = null,
        EventCallback<string>? valueChanged = null,
        EventCallback<bool>? isOpenChanged = null,
        bool includeItems = false)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Combobox>(0);
            builder.AddAttribute(1, "IsOpen", isOpen);
            if (value != null)
                builder.AddAttribute(2, "Value", value);
            if (valueChanged.HasValue)
                builder.AddAttribute(3, "ValueChanged", valueChanged.Value);
            if (isOpenChanged.HasValue)
                builder.AddAttribute(4, "IsOpenChanged", isOpenChanged.Value);
            builder.AddAttribute(5, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.ComboboxInput>(0);
                b.AddAttribute(1, "Placeholder", "Search...");
                b.CloseComponent();

                if (includeItems)
                {
                    b.OpenComponent<L.ComboboxContent>(0);
                    b.AddAttribute(1, "ChildContent", (RenderFragment)(c =>
                    {
                        c.OpenComponent<L.ComboboxItem>(0);
                        c.AddAttribute(1, "Value", "apple");
                        c.AddAttribute(2, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Apple")));
                        c.CloseComponent();

                        c.OpenComponent<L.ComboboxItem>(1);
                        c.AddAttribute(1, "Value", "banana");
                        c.AddAttribute(2, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Banana")));
                        c.CloseComponent();
                    }));
                    b.CloseComponent();
                }
            }));
            builder.CloseComponent();
        });
    }

    // --- Rendering ---

    [Fact]
    public void Combobox_Renders_Wrapper_Div()
    {
        var cut = RenderCombobox();
        var wrapper = cut.Find("div.relative");
        Assert.NotNull(wrapper);
    }

    [Fact]
    public void ComboboxInput_Renders_Input_Element()
    {
        var cut = RenderCombobox();
        var input = cut.Find("input[type='text']");
        Assert.NotNull(input);
    }

    [Fact]
    public void ComboboxInput_Has_Placeholder()
    {
        var cut = RenderCombobox();
        var input = cut.Find("input[type='text']");
        Assert.Equal("Search...", input.GetAttribute("placeholder"));
    }

    [Fact]
    public void ComboboxContent_Not_Rendered_When_Closed()
    {
        var cut = RenderCombobox(isOpen: false, includeItems: true);
        Assert.DoesNotContain("Apple", cut.Markup);
        Assert.DoesNotContain("Banana", cut.Markup);
    }

    [Fact]
    public void ComboboxContent_Rendered_When_Open()
    {
        var cut = RenderCombobox(isOpen: true, includeItems: true);
        Assert.Contains("Apple", cut.Markup);
        Assert.Contains("Banana", cut.Markup);
    }

    // --- Open/Close ---

    [Fact]
    public void Focusing_Input_When_Closed_Opens_Combobox()
    {
        bool? openedValue = null;
        var callback = EventCallback.Factory.Create<bool>(_ctx, (bool v) => openedValue = v);
        var cut = RenderCombobox(isOpen: false, isOpenChanged: callback);

        cut.Find("input").Focus();
        Assert.True(openedValue);
    }

    [Fact]
    public void Typing_In_Input_When_Closed_Opens_Combobox()
    {
        bool? openedValue = null;
        var callback = EventCallback.Factory.Create<bool>(_ctx, (bool v) => openedValue = v);
        var cut = RenderCombobox(isOpen: false, isOpenChanged: callback);

        cut.Find("input").Input("ap");
        Assert.True(openedValue);
    }

    // --- Item Selection ---

    [Fact]
    public void Clicking_ComboboxItem_Fires_ValueChanged()
    {
        string? selectedValue = null;
        var callback = EventCallback.Factory.Create<string>(_ctx, (string v) => selectedValue = v);
        var cut = RenderCombobox(isOpen: true, valueChanged: callback, includeItems: true);

        var buttons = cut.FindAll("button[type='button']");
        var appleBtn = buttons.FirstOrDefault(b => b.TextContent.Contains("Apple"));
        Assert.NotNull(appleBtn);
        try { appleBtn!.Click(); } catch (ArgumentException) { }

        Assert.Equal("apple", selectedValue);
    }

    [Fact]
    public void Clicking_ComboboxItem_Fires_IsOpenChanged_False()
    {
        bool? openValue = null;
        var callback = EventCallback.Factory.Create<bool>(_ctx, (bool v) => openValue = v);
        var cut = RenderCombobox(isOpen: true, isOpenChanged: callback, includeItems: true);

        var buttons = cut.FindAll("button[type='button']");
        var appleBtn = buttons.FirstOrDefault(b => b.TextContent.Contains("Apple"));
        Assert.NotNull(appleBtn);
        try { appleBtn!.Click(); } catch (ArgumentException) { }

        Assert.False(openValue);
    }

    [Fact]
    public void Clicking_Second_Item_Fires_ValueChanged_With_Correct_Value()
    {
        string? selectedValue = null;
        var callback = EventCallback.Factory.Create<string>(_ctx, (string v) => selectedValue = v);
        var cut = RenderCombobox(isOpen: true, valueChanged: callback, includeItems: true);

        var buttons = cut.FindAll("button[type='button']");
        var bananaBtn = buttons.FirstOrDefault(b => b.TextContent.Contains("Banana"));
        Assert.NotNull(bananaBtn);
        try { bananaBtn!.Click(); } catch (ArgumentException) { }

        Assert.Equal("banana", selectedValue);
    }

    // --- Selected Indicator ---

    [Fact]
    public void Selected_Item_Shows_Check_Icon()
    {
        var cut = RenderCombobox(isOpen: true, value: "apple", includeItems: true);

        var buttons = cut.FindAll("button[type='button']");
        var appleBtn = buttons.FirstOrDefault(b => b.TextContent.Contains("Apple"));
        Assert.NotNull(appleBtn);
        Assert.NotEmpty(appleBtn!.QuerySelectorAll("svg"));
    }

    [Fact]
    public void Non_Selected_Item_Has_No_Check_Icon()
    {
        var cut = RenderCombobox(isOpen: true, value: "apple", includeItems: true);

        var buttons = cut.FindAll("button[type='button']");
        var bananaBtn = buttons.FirstOrDefault(b => b.TextContent.Contains("Banana") && !b.TextContent.Contains("Apple"));
        Assert.NotNull(bananaBtn);
        Assert.Empty(bananaBtn!.QuerySelectorAll("svg"));
    }

    // --- ComboboxEmpty ---

    [Fact]
    public void ComboboxEmpty_Renders_Default_Message()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.ComboboxEmpty>(0);
            builder.CloseComponent();
        });

        Assert.Contains("No results found", cut.Markup);
    }

    [Fact]
    public void ComboboxEmpty_Renders_Custom_ChildContent()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.ComboboxEmpty>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b => b.AddContent(0, "Nothing here")));
            builder.CloseComponent();
        });

        Assert.Contains("Nothing here", cut.Markup);
    }

    // --- Custom CSS ---

    [Fact]
    public void Custom_Class_Forwarded_On_ComboboxInput()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Combobox>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.ComboboxInput>(0);
                b.AddAttribute(1, "Class", "my-input-class");
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var input = cut.Find("input");
        Assert.Contains("my-input-class", input.GetAttribute("class"));
    }

    [Fact]
    public void AdditionalAttributes_Forwarded_On_Combobox_Wrapper()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Combobox>(0);
            builder.AddAttribute(1, "data-testid", "my-combobox");
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.ComboboxInput>(0);
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var wrapper = cut.Find("div.relative");
        Assert.Equal("my-combobox", wrapper.GetAttribute("data-testid"));
    }

    // --- #156: top-level Class + Placeholder API parity ---

    [Fact]
    public void Class_Is_Merged_Onto_Wrapper()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Combobox>(0);
            builder.AddAttribute(1, "Class", "my-combobox-class");
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.ComboboxInput>(0);
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var wrapper = cut.Find("div.relative");
        Assert.Contains("my-combobox-class", wrapper.GetAttribute("class"));
    }

    [Fact]
    public void TopLevel_Placeholder_Shows_On_Input()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Combobox>(0);
            builder.AddAttribute(1, "Placeholder", "Search fruit…");
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.ComboboxInput>(0);
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.Equal("Search fruit…", cut.Find("input").GetAttribute("placeholder"));
    }

    [Fact]
    public void Input_Placeholder_Overrides_TopLevel()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Combobox>(0);
            builder.AddAttribute(1, "Placeholder", "combobox-level");
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.ComboboxInput>(0);
                b.AddAttribute(1, "Placeholder", "input-level");
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.Equal("input-level", cut.Find("input").GetAttribute("placeholder"));
    }

    // --- #162: Multi-select chips + Clearable + MaxDisplayTags parity with Select ---

    private IRenderedComponent<IComponent> RenderMultiCombobox(
        HashSet<string>? values = null,
        bool clearable = false,
        int? maxDisplayTags = null,
        EventCallback<HashSet<string>>? valuesChanged = null)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Combobox>(0);
            builder.AddAttribute(1, "Multiple", true);
            if (values != null)
                builder.AddAttribute(2, "Values", values);
            if (valuesChanged.HasValue)
                builder.AddAttribute(3, "ValuesChanged", valuesChanged.Value);
            if (clearable)
                builder.AddAttribute(4, "Clearable", true);
            if (maxDisplayTags.HasValue)
                builder.AddAttribute(5, "MaxDisplayTags", maxDisplayTags.Value);
            builder.AddAttribute(6, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.ComboboxInput>(0);
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    [Fact]
    public void Multiple_With_No_Values_Renders_Plain_Input()
    {
        var cut = RenderMultiCombobox(values: null);
        Assert.Empty(cut.FindAll("button[aria-label^='Remove ']"));
        Assert.NotNull(cut.Find("input[type='text']"));
    }

    [Fact]
    public void Multiple_With_Values_Renders_Chip_Per_Value()
    {
        var cut = RenderMultiCombobox(values: new HashSet<string> { "react", "vue", "angular" });
        var chips = cut.FindAll("button[aria-label^='Remove ']");
        Assert.Equal(3, chips.Count);
        Assert.Contains("react", cut.Markup);
        Assert.Contains("vue", cut.Markup);
        Assert.Contains("angular", cut.Markup);
    }

    [Fact]
    public void Multiple_With_Values_Renders_Input_Alongside_Chips()
    {
        var cut = RenderMultiCombobox(values: new HashSet<string> { "react" });
        Assert.NotNull(cut.Find("input[type='text']"));
    }

    [Fact]
    public void Chip_Remove_Click_Toggles_Value_Off()
    {
        HashSet<string>? captured = null;
        var callback = EventCallback.Factory.Create<HashSet<string>>(_ctx, (HashSet<string> v) => captured = v);
        var cut = RenderMultiCombobox(
            values: new HashSet<string> { "react", "vue" },
            valuesChanged: callback);

        var removeReact = cut.Find("button[aria-label='Remove react']");
        try { removeReact.Click(); } catch (ArgumentException) { }

        Assert.NotNull(captured);
        Assert.DoesNotContain("react", captured!);
        Assert.Contains("vue", captured!);
    }

    [Fact]
    public void MaxDisplayTags_Caps_Visible_Chips_And_Shows_Remainder()
    {
        var values = new HashSet<string> { "a", "b", "c", "d", "e" };
        var cut = RenderMultiCombobox(values: values, maxDisplayTags: 2);

        Assert.Equal(2, cut.FindAll("button[aria-label^='Remove ']").Count);
        Assert.Contains("+3 more", cut.Markup);
    }

    [Fact]
    public void MaxDisplayTags_Default_Is_3()
    {
        var values = new HashSet<string> { "a", "b", "c", "d" };
        var cut = RenderMultiCombobox(values: values);

        Assert.Equal(3, cut.FindAll("button[aria-label^='Remove ']").Count);
        Assert.Contains("+1 more", cut.Markup);
    }

    [Fact]
    public void Clearable_Multiple_Shows_Clear_Button_When_Values_Present()
    {
        var cut = RenderMultiCombobox(values: new HashSet<string> { "react" }, clearable: true);
        Assert.NotEmpty(cut.FindAll("button[aria-label='Clear selection']"));
    }

    [Fact]
    public void Clearable_False_Hides_Clear_Button()
    {
        var cut = RenderMultiCombobox(values: new HashSet<string> { "react" }, clearable: false);
        Assert.Empty(cut.FindAll("button[aria-label='Clear selection']"));
    }

    [Fact]
    public void Clear_Button_Clears_All_Values_In_Multiple_Mode()
    {
        HashSet<string>? captured = null;
        var callback = EventCallback.Factory.Create<HashSet<string>>(_ctx, (HashSet<string> v) => captured = v);
        var cut = RenderMultiCombobox(
            values: new HashSet<string> { "react", "vue" },
            clearable: true,
            valuesChanged: callback);

        var clearBtn = cut.Find("button[aria-label='Clear selection']");
        try { clearBtn.Click(); } catch (ArgumentException) { }

        // Codex P2 (#163 review): emit empty set rather than null so consumers
        // binding to a non-null HashSet<string> don't NRE on _selected.Count.
        Assert.NotNull(captured);
        Assert.Empty(captured!);
    }

    // --- #164: single-select chip parity with multi ---

    private IRenderedComponent<IComponent> RenderSingleCombobox(
        string? value = null,
        bool clearable = false,
        EventCallback<string>? valueChanged = null)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Combobox>(0);
            if (value != null)
                builder.AddAttribute(1, "Value", value);
            if (valueChanged.HasValue)
                builder.AddAttribute(2, "ValueChanged", valueChanged.Value);
            if (clearable)
                builder.AddAttribute(3, "Clearable", true);
            builder.AddAttribute(4, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.ComboboxInput>(0);
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    [Fact]
    public void Single_Select_With_Value_Renders_Chip_In_Input()
    {
        var cut = RenderSingleCombobox(value: "react");
        Assert.Contains("react", cut.Markup);
        // Chip wrapper marks the tag-input layout (border-border/40 + bg-secondary on the span).
        Assert.NotEmpty(cut.FindAll("span.bg-secondary"));
    }

    [Fact]
    public void Single_Select_With_No_Value_Renders_Empty_Search_Input()
    {
        var cut = RenderSingleCombobox(value: null);
        Assert.Empty(cut.FindAll("span.bg-secondary"));
        Assert.NotNull(cut.Find("input[type='text']"));
    }

    [Fact]
    public void Single_Select_Chip_Has_No_Remove_Button_When_Not_Clearable()
    {
        var cut = RenderSingleCombobox(value: "react", clearable: false);
        // The chip is shown, but with no X button — pure visual indicator.
        Assert.Empty(cut.FindAll("button[aria-label^='Remove ']"));
    }

    [Fact]
    public void Single_Select_Clearable_Chip_Shows_Remove_Button()
    {
        var cut = RenderSingleCombobox(value: "react", clearable: true);
        Assert.NotEmpty(cut.FindAll("button[aria-label='Remove react']"));
    }

    [Fact]
    public void Single_Select_Clearable_Chip_Click_Clears_Value()
    {
        string? captured = "react";
        var callback = EventCallback.Factory.Create<string>(_ctx, (string v) => captured = v);
        var cut = RenderSingleCombobox(value: "react", clearable: true, valueChanged: callback);

        var removeBtn = cut.Find("button[aria-label='Remove react']");
        try { removeBtn.Click(); } catch (ArgumentException) { }

        Assert.Null(captured);
    }

    // --- PR #165 review fixes: Codex P2 (label lookup, chip click, comparer) ---

    [Fact]
    public void Clear_Preserves_HashSet_Comparer()
    {
        HashSet<string>? captured = null;
        var callback = EventCallback.Factory.Create<HashSet<string>>(_ctx, (HashSet<string> v) => captured = v);

        var seed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "React", "Vue" };
        var cut = RenderMultiCombobox(values: seed, clearable: true, valuesChanged: callback);

        var clearBtn = cut.Find("button[aria-label='Clear selection']");
        try { clearBtn.Click(); } catch (ArgumentException) { }

        Assert.NotNull(captured);
        Assert.Empty(captured!);
        // Comparer must survive the clear — otherwise downstream Contains/Add
        // changes equality semantics silently (Codex P2, PR #165).
        Assert.Same(StringComparer.OrdinalIgnoreCase, captured!.Comparer);
    }

    private IRenderedComponent<IComponent> RenderDataBoundSingleCombobox(
        string? value,
        IEnumerable<object> items,
        Func<object, string> itemValue,
        Func<object, string> itemText,
        bool clearable = false)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Combobox>(0);
            if (value != null) builder.AddAttribute(1, "Value", value);
            builder.AddAttribute(2, "Items", items);
            builder.AddAttribute(3, "ItemValue", itemValue);
            builder.AddAttribute(4, "ItemText", itemText);
            if (clearable) builder.AddAttribute(5, "Clearable", true);
            builder.AddAttribute(6, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.ComboboxInput>(0);
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    [Fact]
    public void Single_Chip_Shows_ItemText_Not_Raw_Value_When_Data_Bound()
    {
        // Data-bound: stored value "dotnet" should display as ".NET" in the chip.
        var items = new object[]
        {
            new { Id = "dotnet", Label = ".NET" },
            new { Id = "blazor", Label = "Blazor" },
        };
        var cut = RenderDataBoundSingleCombobox(
            value: "dotnet",
            items: items,
            itemValue: o => ((dynamic)o).Id,
            itemText: o => ((dynamic)o).Label);

        // The chip wrapper span must contain the label, not the id.
        var chipSpans = cut.FindAll("span.bg-secondary");
        Assert.NotEmpty(chipSpans);
        var chipText = chipSpans[0].TextContent;
        Assert.Contains(".NET", chipText);
        Assert.DoesNotContain("dotnet", chipText);
    }

    [Fact]
    public void Single_Chip_Falls_Back_To_Raw_Value_In_Composition_Mode()
    {
        // Composition mode (no Items): no value→label registry, so the raw
        // value is the best we can do. Matches v3.12.0 multi-chip behavior.
        var cut = RenderSingleCombobox(value: "react");
        var chipSpans = cut.FindAll("span.bg-secondary");
        Assert.NotEmpty(chipSpans);
        Assert.Contains("react", chipSpans[0].TextContent);
    }

    [Fact]
    public void Clicking_Single_Chip_Wrapper_Opens_Combobox()
    {
        bool? opened = null;
        var callback = EventCallback.Factory.Create<bool>(_ctx, (bool v) => opened = v);

        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Combobox>(0);
            builder.AddAttribute(1, "Value", "react");
            builder.AddAttribute(2, "IsOpen", false);
            builder.AddAttribute(3, "IsOpenChanged", callback);
            builder.AddAttribute(4, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.ComboboxInput>(0);
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        // Click the wrapper div (chip layout). The wrapper carries @onclick=OpenAndFocus.
        var wrapper = cut.Find("div.flex.flex-wrap");
        try { wrapper.Click(); } catch (ArgumentException) { }

        Assert.True(opened);
    }
}
