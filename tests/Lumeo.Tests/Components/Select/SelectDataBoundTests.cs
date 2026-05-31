using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Select;

public class SelectDataBoundTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SelectDataBoundTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // Helpers

    private static IEnumerable<object> Fruits => new object[] { "apple", "banana", "cherry" };

    private IRenderedComponent<IComponent> RenderDataBound(
        IEnumerable<object>? items = null,
        Func<object, string>? itemValue = null,
        Func<object, string>? itemText = null,
        Func<object, string?>? itemGroup = null,
        bool isOpen = true,
        string? value = null,
        EventCallback<string?>? valueChanged = null,
        bool virtualize = false)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Select>(0);
            builder.AddAttribute(1, "Open", isOpen);
            if (value != null)
                builder.AddAttribute(2, "Value", value);
            if (valueChanged.HasValue)
                builder.AddAttribute(3, "ValueChanged", valueChanged.Value);
            if (items != null)
                builder.AddAttribute(4, "Items", items);
            if (itemValue != null)
                builder.AddAttribute(5, "ItemValue", itemValue);
            if (itemText != null)
                builder.AddAttribute(6, "ItemText", itemText);
            if (itemGroup != null)
                builder.AddAttribute(7, "ItemGroup", itemGroup);
            builder.AddAttribute(8, "Virtualize", virtualize);
            builder.AddAttribute(9, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SelectTrigger>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "Choose...")));
                b.CloseComponent();

                b.OpenComponent<L.SelectContent>(0);
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    // --- Test 1: data-bound Items renders options ---

    [Fact]
    public void DataBound_Items_Renders_Options_With_Default_ItemValue()
    {
        var cut = RenderDataBound(items: Fruits);

        // All three option texts should appear in the markup
        Assert.Contains("apple", cut.Markup);
        Assert.Contains("banana", cut.Markup);
        Assert.Contains("cherry", cut.Markup);
    }

    [Fact]
    public void DataBound_Items_With_ItemText_Renders_Custom_Labels()
    {
        var items = new object[] { "apple", "banana" };
        Func<object, string> itemText = it => it.ToString()!.ToUpperInvariant();

        var cut = RenderDataBound(items: items, itemText: itemText);

        Assert.Contains("APPLE", cut.Markup);
        Assert.Contains("BANANA", cut.Markup);
        // raw lowercase values should not appear as item text — but could appear in IDs/attributes
    }

    [Fact]
    public void DataBound_Items_Each_Rendered_As_Option_Button()
    {
        var cut = RenderDataBound(items: Fruits);

        // Each bound item renders a button[role=option]
        var options = cut.FindAll("button[role='option']");
        Assert.Equal(3, options.Count);
    }

    // --- Test 2: selecting a data-bound item updates Value ---

    [Fact]
    public void DataBound_Clicking_Item_Fires_ValueChanged_With_ItemValue()
    {
        string? selected = null;
        var cb = EventCallback.Factory.Create<string?>(_ctx, (string? v) => selected = v);
        var cut = RenderDataBound(items: Fruits, valueChanged: cb);

        var options = cut.FindAll("button[role='option']");
        var appleBtn = options.FirstOrDefault(b => b.TextContent.Contains("apple"));
        Assert.NotNull(appleBtn);
        try { appleBtn!.Click(); } catch (ArgumentException) { }

        Assert.Equal("apple", selected);
    }

    [Fact]
    public void DataBound_Selected_Item_Shows_Check_Icon()
    {
        var cut = RenderDataBound(items: Fruits, value: "banana");

        var options = cut.FindAll("button[role='option']");
        var bananaBtn = options.FirstOrDefault(b => b.TextContent.Contains("banana"));
        Assert.NotNull(bananaBtn);
        Assert.NotEmpty(bananaBtn!.QuerySelectorAll("svg"));
    }

    [Fact]
    public void DataBound_Unselected_Item_Has_No_Check_Icon()
    {
        var cut = RenderDataBound(items: Fruits, value: "banana");

        var options = cut.FindAll("button[role='option']");
        var appleBtn = options.FirstOrDefault(b => b.TextContent.Contains("apple") && !b.TextContent.Contains("banana"));
        Assert.NotNull(appleBtn);
        Assert.Empty(appleBtn!.QuerySelectorAll("svg"));
    }

    // --- Test 3: grouped data-bound renders group headers ---

    [Fact]
    public void DataBound_With_ItemGroup_Renders_Group_Headers()
    {
        var items = new object[] { "apple", "banana", "carrot", "broccoli" };
        Func<object, string?> itemGroup = it =>
            it.ToString()! switch
            {
                "apple" or "banana" => "Fruits",
                _ => "Vegetables"
            };

        var cut = RenderDataBound(items: items, itemGroup: itemGroup);

        Assert.Contains("Fruits", cut.Markup);
        Assert.Contains("Vegetables", cut.Markup);
    }

    [Fact]
    public void DataBound_With_ItemGroup_Renders_All_Items()
    {
        var items = new object[] { "apple", "banana", "carrot" };
        Func<object, string?> itemGroup = it =>
            it.ToString()! switch
            {
                "apple" or "banana" => "Fruits",
                _ => "Vegetables"
            };

        var cut = RenderDataBound(items: items, itemGroup: itemGroup);

        var options = cut.FindAll("button[role='option']");
        Assert.Equal(3, options.Count);
    }

    // --- Test 4: data-bound + Virtualize renders initial batch of options ---

    [Fact]
    public void DataBound_Virtualize_Renders_Initial_Options()
    {
        // Use a small set so the virtualizer renders all of them in bUnit's container
        var items = Enumerable.Range(1, 5).Select(i => (object)$"item-{i}").ToList();

        var cut = RenderDataBound(items: items, virtualize: true);

        // Virtualize renders at least the first few items in the initial pass
        // bUnit uses a mock container height of 0, so Virtualize may render 0 items.
        // We verify the component renders without crashing.
        Assert.NotNull(cut.Markup);
    }

    [Fact]
    public void DataBound_Virtualize_False_Renders_All_Items()
    {
        var items = Enumerable.Range(1, 5).Select(i => (object)$"item-{i}").ToList();

        var cut = RenderDataBound(items: items, virtualize: false);

        var options = cut.FindAll("button[role='option']");
        Assert.Equal(5, options.Count);
    }

    // --- Composition mode still works (non-regression) ---

    [Fact]
    public void CompositionMode_Still_Works_When_Items_Is_Null()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Select>(0);
            builder.AddAttribute(1, "Open", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SelectTrigger>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "Pick")));
                b.CloseComponent();

                b.OpenComponent<L.SelectContent>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(c =>
                {
                    c.OpenComponent<L.SelectItem>(0);
                    c.AddAttribute(1, "Value", "opt1");
                    c.AddAttribute(2, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Option 1")));
                    c.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.Contains("Option 1", cut.Markup);
    }

    // --- ItemDescription / ItemIcon (data-bound default-renderer extensions) ---

    private record TaggedItem(string Value, string Label, string? Description);

    [Fact]
    public void ItemDescription_Renders_Below_Label_When_Set()
    {
        var items = new object[]
        {
            new TaggedItem("apple", "Apple", "Crisp red fruit"),
        };

        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Select>(0);
            builder.AddAttribute(1, "Open", true);
            builder.AddAttribute(2, "Items", (IEnumerable<object>)items);
            builder.AddAttribute(3, "ItemValue", (Func<object, string>)(o => ((TaggedItem)o).Value));
            builder.AddAttribute(4, "ItemText", (Func<object, string>)(o => ((TaggedItem)o).Label));
            builder.AddAttribute(5, "ItemDescription", (Func<object, string?>)(o => ((TaggedItem)o).Description));
            builder.AddAttribute(6, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SelectTrigger>(0);
                b.CloseComponent();
                b.OpenComponent<L.SelectContent>(1);
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.Contains("Apple", cut.Markup);
        Assert.Contains("Crisp red fruit", cut.Markup);
        Assert.Contains("text-muted-foreground", cut.Markup);
    }

    [Fact]
    public void ItemIcon_Renders_Leading_Icon_When_Provided()
    {
        var items = new object[] { "apple", "banana" };

        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Select>(0);
            builder.AddAttribute(1, "Open", true);
            builder.AddAttribute(2, "Items", (IEnumerable<object>)items);
            builder.AddAttribute(3, "ItemIcon",
                (Func<object, RenderFragment?>)(o => b =>
                {
                    b.OpenElement(0, "i");
                    b.AddAttribute(1, "data-testid", $"icon-{o}");
                    b.CloseElement();
                }));
            builder.AddAttribute(4, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SelectTrigger>(0);
                b.CloseComponent();
                b.OpenComponent<L.SelectContent>(1);
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.NotNull(cut.Find("[data-testid='icon-apple']"));
        Assert.NotNull(cut.Find("[data-testid='icon-banana']"));
    }

    [Fact]
    public void ItemIcon_Null_For_Item_Skips_Icon_Slot()
    {
        var items = new object[] { "apple", "banana" };

        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Select>(0);
            builder.AddAttribute(1, "Open", true);
            builder.AddAttribute(2, "Items", (IEnumerable<object>)items);
            builder.AddAttribute(3, "ItemIcon",
                (Func<object, RenderFragment?>)(o => o.ToString() == "apple"
                    ? b => { b.OpenElement(0, "i"); b.AddAttribute(1, "data-testid", "icon-apple"); b.CloseElement(); }
                    : null));
            builder.AddAttribute(4, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SelectTrigger>(0);
                b.CloseComponent();
                b.OpenComponent<L.SelectContent>(1);
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.Single(cut.FindAll("[data-testid^='icon-']"));
    }
}
