using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Combobox;

/// <summary>
/// Tests for composition-mode grouping (<see cref="L.ComboboxGroup"/>): labelled headers,
/// nested (multi-level) groups, collapsible behaviour, group-level selection in multi-select,
/// and filtering that hides groups with no matching descendants.
/// </summary>
public class ComboboxGroupTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ComboboxGroupTests() => _ctx.AddLumeoServices();

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // Renders an open Combobox with two groups (Fruits: apple/banana, Vegetables: carrot)
    // and lets the caller tweak the group/item options via the builder hooks.
    private IRenderedComponent<IComponent> RenderGrouped(
        string? search = null,
        bool collapsible = false,
        bool defaultExpanded = true,
        bool multiple = false,
        bool groupSelect = false,
        HashSet<string>? values = null)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Combobox>(0);
            builder.AddAttribute(1, "Open", true);
            builder.AddAttribute(2, "Multiple", multiple);
            if (values is not null) builder.AddAttribute(3, "Values", values);
            builder.AddAttribute(4, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.ComboboxInput>(0);
                b.AddAttribute(1, "Placeholder", "Search...");
                b.CloseComponent();

                b.OpenComponent<L.ComboboxContent>(2);
                b.AddAttribute(3, "ChildContent", (RenderFragment)(c =>
                {
                    Group(c, 0, "Fruits", grp =>
                    {
                        Item(grp, 0, "apple", "Apple");
                        Item(grp, 1, "banana", "Banana");
                    }, collapsible, defaultExpanded, groupSelect);

                    Group(c, 10, "Vegetables", grp =>
                    {
                        Item(grp, 0, "carrot", "Carrot");
                    }, collapsible, defaultExpanded, groupSelect);
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        void Group(RenderTreeBuilder rb, int seq, string label, RenderFragment children,
                   bool col, bool def, bool gs)
        {
            rb.OpenComponent<L.ComboboxGroup>(seq);
            rb.AddAttribute(seq + 1, "Label", label);
            rb.AddAttribute(seq + 2, "Collapsible", col);
            rb.AddAttribute(seq + 3, "DefaultExpanded", def);
            rb.AddAttribute(seq + 4, "GroupSelect", gs);
            rb.AddAttribute(seq + 5, "ChildContent", children);
            rb.CloseComponent();
        }

        void Item(RenderTreeBuilder rb, int seq, string value, string label)
        {
            rb.OpenComponent<L.ComboboxItem>(seq);
            rb.AddAttribute(seq + 1, "Value", value);
            rb.AddAttribute(seq + 2, "ChildContent", (RenderFragment)(i => i.AddContent(0, label)));
            rb.CloseComponent();
        }
    }

    // Helper to type into the search input (drives client-side filtering).
    private static void Search(IRenderedComponent<IComponent> cut, string text)
        => cut.Find("input[type='text']").Input(text);

    [Fact]
    public void Renders_Group_Headers_As_Non_Option_Roles()
    {
        var cut = RenderGrouped();
        var groups = cut.FindAll("[role='group']");
        Assert.Equal(2, groups.Count);
        Assert.Contains(cut.Markup, m => true); // sanity
        Assert.Contains("Fruits", cut.Markup);
        Assert.Contains("Vegetables", cut.Markup);
    }

    [Fact]
    public void Group_Header_Is_Not_An_Option()
    {
        var cut = RenderGrouped();
        // Only the 3 items are options; headers are not role=option.
        Assert.Equal(3, cut.FindAll("[role='option']").Count);
    }

    [Fact]
    public void Filter_Hides_Group_With_No_Matching_Items()
    {
        var cut = RenderGrouped();
        Search(cut, "carrot");

        // Only the Vegetables group should remain visible; Fruits is hidden.
        var groups = cut.FindAll("[role='group']");
        var fruits = groups.First(g => g.TextContent.Contains("Fruits"));
        var veg = groups.First(g => g.TextContent.Contains("Vegetables"));
        Assert.Contains("hidden", fruits.GetAttribute("class"));
        Assert.DoesNotContain("hidden", veg.GetAttribute("class") ?? "");
        // Only the carrot option is rendered now.
        Assert.Single(cut.FindAll("[role='option']"));
    }

    [Fact]
    public void Collapsible_Group_Hides_Items_When_Collapsed()
    {
        var cut = RenderGrouped(collapsible: true, defaultExpanded: false);
        // Collapsed by default → its items are not rendered.
        Assert.Empty(cut.FindAll("[role='option']"));

        // Click the Fruits header to expand it.
        var fruitsHeader = cut.FindAll("[role='button']").First(h => h.TextContent.Contains("Fruits"));
        fruitsHeader.Click();

        Assert.Equal(2, cut.FindAll("[role='option']").Count);
    }

    [Fact]
    public void Active_Search_Force_Expands_Collapsed_Group()
    {
        var cut = RenderGrouped(collapsible: true, defaultExpanded: false);
        Assert.Empty(cut.FindAll("[role='option']"));

        Search(cut, "apple");
        // The matching item shows even though the group started collapsed.
        var options = cut.FindAll("[role='option']");
        Assert.Single(options);
        Assert.Contains("Apple", options[0].TextContent);
    }

    [Fact]
    public void GroupSelect_Checkbox_Selects_All_Descendants()
    {
        var selected = new HashSet<string>();
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Combobox>(0);
            builder.AddAttribute(1, "Open", true);
            builder.AddAttribute(2, "Multiple", true);
            builder.AddAttribute(3, "Values", selected);
            builder.AddAttribute(4, "ValuesChanged",
                EventCallback.Factory.Create<HashSet<string>>(this, v => selected = v));
            builder.AddAttribute(5, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.ComboboxContent>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(c =>
                {
                    c.OpenComponent<L.ComboboxGroup>(0);
                    c.AddAttribute(1, "Label", "Fruits");
                    c.AddAttribute(2, "GroupSelect", true);
                    c.AddAttribute(3, "ChildContent", (RenderFragment)(g =>
                    {
                        g.OpenComponent<L.ComboboxItem>(0);
                        g.AddAttribute(1, "Value", "apple");
                        g.AddAttribute(2, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Apple")));
                        g.CloseComponent();
                        g.OpenComponent<L.ComboboxItem>(3);
                        g.AddAttribute(1, "Value", "banana");
                        g.AddAttribute(2, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Banana")));
                        g.CloseComponent();
                    }));
                    c.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        // The group header checkbox is the first role=checkbox in the markup.
        var checkbox = cut.Find("[role='checkbox']");
        checkbox.Click();

        Assert.Contains("apple", selected);
        Assert.Contains("banana", selected);
    }

    [Fact]
    public void Nested_Groups_Render_Multi_Level()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Combobox>(0);
            builder.AddAttribute(1, "Open", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.ComboboxContent>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(c =>
                {
                    c.OpenComponent<L.ComboboxGroup>(0);
                    c.AddAttribute(1, "Label", "Food");
                    c.AddAttribute(2, "ChildContent", (RenderFragment)(outer =>
                    {
                        outer.OpenComponent<L.ComboboxGroup>(0);
                        outer.AddAttribute(1, "Label", "Fruits");
                        outer.AddAttribute(2, "ChildContent", (RenderFragment)(inner =>
                        {
                            inner.OpenComponent<L.ComboboxItem>(0);
                            inner.AddAttribute(1, "Value", "apple");
                            inner.AddAttribute(2, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Apple")));
                            inner.CloseComponent();
                        }));
                        outer.CloseComponent();
                    }));
                    c.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        // Two nested group containers + the apple option.
        Assert.Equal(2, cut.FindAll("[role='group']").Count);
        Assert.Single(cut.FindAll("[role='option']"));
        Assert.Contains("Food", cut.Markup);
        Assert.Contains("Fruits", cut.Markup);
    }
}
