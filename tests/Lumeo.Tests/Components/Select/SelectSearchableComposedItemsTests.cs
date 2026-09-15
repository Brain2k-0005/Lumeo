using System.Linq;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Select;

/// <summary>
/// Field report #464, finding 1 — a Searchable Select whose options are composed as
/// <see cref="L.SelectItem"/> children (rather than passed via the data-bound Items
/// parameter) must filter on those items' own text, not just the data-bound list.
/// </summary>
public class SelectSearchableComposedItemsTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public SelectSearchableComposedItemsTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // Values deliberately differ from the visible labels (like a real country picker:
    // code as Value, name as the rendered label) and no SearchValue is set — the exact
    // shape the field report hit.
    private IRenderedComponent<IComponent> RenderComposed(
        bool multiple = false, List<string>? values = null, bool creatable = false)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Select>(0);
            builder.AddAttribute(1, "Open", true);
            builder.AddAttribute(2, "Searchable", true);
            builder.AddAttribute(3, "Multiple", multiple);
            if (values is not null) builder.AddAttribute(4, "Values", values);
            if (creatable) builder.AddAttribute(5, "Creatable", true);
            builder.AddAttribute(6, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SelectTrigger>(0);
                b.CloseComponent();

                b.OpenComponent<L.SelectContent>(2);
                b.AddAttribute(3, "ChildContent", (RenderFragment)(c =>
                {
                    Item(c, 0, "ch", "Schweiz");
                    Item(c, 4, "de", "Deutschland");
                    Item(c, 8, "at", "Österreich");
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        void Item(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder rb, int seq, string value, string label)
        {
            rb.OpenComponent<L.SelectItem>(seq);
            rb.AddAttribute(seq + 1, "Value", value);
            rb.AddAttribute(seq + 2, "ChildContent", (RenderFragment)(i => i.AddContent(0, label)));
            rb.CloseComponent();
        }
    }

    [Fact]
    public void All_Composed_Items_Render_With_Empty_Search()
    {
        var cut = RenderComposed();
        Assert.Equal(3, cut.FindAll("[role='option']").Count);
    }

    [Fact]
    public void Typing_Filters_Composed_Items_By_Their_Rendered_Label()
    {
        var cut = RenderComposed();

        cut.Find("input").Input("Schw");

        var options = cut.FindAll("[role='option']");
        Assert.Single(options);
        Assert.Contains("Schweiz", options[0].TextContent);
    }

    [Fact]
    public void Typing_A_Non_Matching_Query_Shows_Empty_State_Not_All_Items()
    {
        var cut = RenderComposed();

        cut.Find("input").Input("zzz");

        Assert.Empty(cut.FindAll("[role='option']"));
    }

    [Fact]
    public void Multiple_Searchable_Composed_Items_Filter_And_Stay_Selectable()
    {
        var selected = new List<string>();
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Select>(0);
            builder.AddAttribute(1, "Open", true);
            builder.AddAttribute(2, "Searchable", true);
            builder.AddAttribute(3, "Multiple", true);
            builder.AddAttribute(4, "Values", selected);
            builder.AddAttribute(5, "ValuesChanged", EventCallback.Factory.Create<List<string>?>(this, v => selected = v ?? new()));
            builder.AddAttribute(6, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SelectTrigger>(0);
                b.CloseComponent();
                b.OpenComponent<L.SelectContent>(2);
                b.AddAttribute(3, "ChildContent", (RenderFragment)(c =>
                {
                    c.OpenComponent<L.SelectItem>(0);
                    c.AddAttribute(1, "Value", "ch");
                    c.AddAttribute(2, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Schweiz")));
                    c.CloseComponent();

                    c.OpenComponent<L.SelectItem>(4);
                    c.AddAttribute(5, "Value", "de");
                    c.AddAttribute(6, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Deutschland")));
                    c.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        cut.Find("input").Input("Schw");
        var options = cut.FindAll("[role='option']");
        Assert.Single(options);

        options[0].Click();
        Assert.Contains("ch", selected);
    }

    // --- Label wrapped in a child component (documented limitation) ---
    //
    // ExtractText walks ChildContent's own RenderTreeBuilder frames without running a
    // nested component's BuildRenderTree (see SelectItem.ExtractText) — a label that
    // lives entirely inside a child component (e.g. <Text As="span">) is therefore
    // invisible to it. SelectItem.SearchValue's XML doc calls this out explicitly: set
    // SearchValue when the label is wrapped like this. These two tests pin that
    // documented, deliberate behaviour so a future change to ExtractText's scope doesn't
    // silently drift: without SearchValue the filter still falls back to matching Value
    // (not empty, not a crash); with SearchValue it matches the real label.
    private IRenderedComponent<IComponent> RenderComposedWithWrappedLabel(bool withSearchValue)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Select>(0);
            builder.AddAttribute(1, "Open", true);
            builder.AddAttribute(2, "Searchable", true);
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SelectTrigger>(0);
                b.CloseComponent();

                b.OpenComponent<L.SelectContent>(2);
                b.AddAttribute(3, "ChildContent", (RenderFragment)(c =>
                {
                    c.OpenComponent<L.SelectItem>(0);
                    c.AddAttribute(1, "Value", "ch");
                    if (withSearchValue) c.AddAttribute(2, "SearchValue", "Switzerland");
                    c.AddAttribute(3, "ChildContent", (RenderFragment)(i =>
                    {
                        i.OpenComponent<L.Text>(0);
                        i.AddAttribute(1, "As", "span");
                        i.AddAttribute(2, "ChildContent", (RenderFragment)(t => t.AddContent(0, "Switzerland")));
                        i.CloseComponent();
                    }));
                    c.CloseComponent();

                    c.OpenComponent<L.SelectItem>(4);
                    c.AddAttribute(5, "Value", "de");
                    if (withSearchValue) c.AddAttribute(6, "SearchValue", "Germany");
                    c.AddAttribute(7, "ChildContent", (RenderFragment)(i =>
                    {
                        i.OpenComponent<L.Text>(0);
                        i.AddAttribute(1, "As", "span");
                        i.AddAttribute(2, "ChildContent", (RenderFragment)(t => t.AddContent(0, "Germany")));
                        i.CloseComponent();
                    }));
                    c.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    [Fact]
    public void Without_SearchValue_A_Component_Wrapped_Label_Falls_Back_To_Matching_Value()
    {
        // Documented limitation: "Switzerland" lives inside <Text As="span">, which
        // ExtractText cannot see, so typing the label finds nothing...
        var cut = RenderComposedWithWrappedLabel(withSearchValue: false);
        cut.Find("input").Input("Switzerland");
        Assert.Empty(cut.FindAll("[role='option']"));

        // ...but typing the raw Value ("ch") still matches, because ExtractText's empty
        // result falls back to Value rather than hiding the item outright.
        cut.Find("input").Input("ch");
        Assert.Single(cut.FindAll("[role='option']"));
    }

    [Fact]
    public void With_SearchValue_A_Component_Wrapped_Label_Filters_By_The_Real_Label()
    {
        var cut = RenderComposedWithWrappedLabel(withSearchValue: true);
        cut.Find("input").Input("Switzerland");

        var options = cut.FindAll("[role='option']");
        Assert.Single(options);
        Assert.Contains("Switzerland", options[0].TextContent);
    }

    // --- Creatable + Searchable + composed items ---

    [Fact]
    public void Creatable_Shows_Create_Affordance_For_A_Query_Matching_No_Composed_Item()
    {
        var cut = RenderComposed(creatable: true);

        cut.Find("input").Input("Portugal");

        Assert.Empty(cut.FindAll("[role='option']"));
        var createButton = cut.FindAll("button").FirstOrDefault(b => b.TextContent.Contains("Portugal"));
        Assert.NotNull(createButton);
    }

    [Fact]
    public void Creatable_Selecting_The_Create_Affordance_Raises_ValueChanged_With_The_Typed_Text()
    {
        string? createdValue = null;
        string? selectedValue = null;
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Select>(0);
            builder.AddAttribute(1, "Open", true);
            builder.AddAttribute(2, "Searchable", true);
            builder.AddAttribute(3, "Creatable", true);
            builder.AddAttribute(4, "OnCreate", EventCallback.Factory.Create<string>(this, v => createdValue = v));
            builder.AddAttribute(5, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => selectedValue = v));
            builder.AddAttribute(6, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SelectTrigger>(0);
                b.CloseComponent();
                b.OpenComponent<L.SelectContent>(2);
                b.AddAttribute(3, "ChildContent", (RenderFragment)(c =>
                {
                    c.OpenComponent<L.SelectItem>(0);
                    c.AddAttribute(1, "Value", "ch");
                    c.AddAttribute(2, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Schweiz")));
                    c.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        cut.Find("input").Input("Portugal");
        var createButton = cut.FindAll("button").First(b => b.TextContent.Contains("Portugal"));
        createButton.Click();

        Assert.Equal("Portugal", createdValue);
        Assert.Equal("Portugal", selectedValue);
    }

    [Fact]
    public void Creatable_Still_Shows_The_Create_Affordance_When_The_Query_Matches_An_Existing_Item()
    {
        // Documents current, pre-existing behaviour rather than asserting a suppression
        // rule that doesn't exist in this component: SelectContent's Creatable button
        // is gated only on `Creatable && SearchText non-empty` (identical in data-bound
        // mode — SelectContent.razor renders the same unconditional block in both
        // branches), with no "already matches an item" check. Composed items don't
        // change that; this test pins it so composed mode and data-bound mode don't
        // silently drift apart on this point in the future.
        var cut = RenderComposed(creatable: true);

        cut.Find("input").Input("Schw");

        Assert.Single(cut.FindAll("[role='option']"));
        var createButton = cut.FindAll("button").FirstOrDefault(b => b.TextContent.Contains("Schw"));
        Assert.NotNull(createButton);
    }
}
