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
}
