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

        Assert.Contains("No results found.", cut.Markup);
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
}
