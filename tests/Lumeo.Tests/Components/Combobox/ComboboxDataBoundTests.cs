using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Combobox;

public class ComboboxDataBoundTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ComboboxDataBoundTests()
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
        EventCallback<string>? valueChanged = null,
        bool virtualize = false)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Combobox>(0);
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
                b.OpenComponent<L.ComboboxInput>(0);
                b.AddAttribute(1, "Placeholder", "Search...");
                b.CloseComponent();

                b.OpenComponent<L.ComboboxContent>(0);
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    // --- Test 1: data-bound Items renders options ---

    [Fact]
    public void DataBound_Items_Renders_Options()
    {
        var cut = RenderDataBound(items: Fruits);

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
    }

    [Fact]
    public void DataBound_Items_Each_Rendered_As_Option_Button()
    {
        var cut = RenderDataBound(items: Fruits);

        var options = cut.FindAll("button[role='option']");
        Assert.Equal(3, options.Count);
    }

    // --- Test 2: selecting a data-bound item updates Value ---

    [Fact]
    public void DataBound_Clicking_Item_Fires_ValueChanged()
    {
        string? selected = null;
        var cb = EventCallback.Factory.Create<string>(_ctx, (string v) => selected = v);
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

    // --- Test 4: data-bound + Virtualize renders initial batch ---

    [Fact]
    public void DataBound_Virtualize_Renders_Without_Crash()
    {
        // bUnit has a zero-height container so Virtualize renders 0 visible rows,
        // but the component should not throw.
        var items = Enumerable.Range(1, 5).Select(i => (object)$"item-{i}").ToList();

        var cut = RenderDataBound(items: items, virtualize: true);

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
            builder.OpenComponent<L.Combobox>(0);
            builder.AddAttribute(1, "Open", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.ComboboxInput>(0);
                b.CloseComponent();

                b.OpenComponent<L.ComboboxContent>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(c =>
                {
                    c.OpenComponent<L.ComboboxItem>(0);
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
}
