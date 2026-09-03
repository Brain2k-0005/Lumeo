using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Select;

/// <summary>Field report 4.1b: a single-value Select whose trigger has no custom content shows
/// the selected item's label, also before the list was ever opened.</summary>
public class SelectSingleValueLabelTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SelectSingleValueLabelTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private sealed record Process(string Code, string Label);

    private IRenderedComponent<IComponent> RenderComposition(string? value) => _ctx.Render(builder =>
    {
        builder.OpenComponent<L.Select>(0);
        builder.AddAttribute(1, "Value", value);
        builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
        {
            b.OpenComponent<L.SelectTrigger>(0);
            b.AddAttribute(1, "Placeholder", "Wählen");
            b.CloseComponent();
            b.OpenComponent<L.SelectContent>(2);
            b.AddAttribute(3, "ChildContent", (RenderFragment)(c =>
            {
                c.OpenComponent<L.SelectItem>(0);
                c.AddAttribute(1, "Value", "Ingoing");
                c.AddAttribute(2, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Eingehend")));
                c.CloseComponent();
                c.OpenComponent<L.SelectItem>(3);
                c.AddAttribute(4, "Value", "Outgoing");
                c.AddAttribute(5, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Ausgehend")));
                c.CloseComponent();
            }));
            b.CloseComponent();
        }));
        builder.CloseComponent();
    });

    private IRenderedComponent<IComponent> RenderDataBound(string? value) => _ctx.Render(builder =>
    {
        builder.OpenComponent<L.Select>(0);
        builder.AddAttribute(1, "Value", value);
        builder.AddAttribute(2, "Items", new List<object> { new Process("Ingoing", "Eingehend"), new Process("Outgoing", "Ausgehend") });
        builder.AddAttribute(3, "ItemValue", (Func<object, string>)(o => ((Process)o).Code));
        builder.AddAttribute(4, "ItemText", (Func<object, string>)(o => ((Process)o).Label));
        builder.AddAttribute(5, "ChildContent", (RenderFragment)(b =>
        {
            b.OpenComponent<L.SelectTrigger>(0);
            b.CloseComponent();
            b.OpenComponent<L.SelectContent>(1);
            b.CloseComponent();
        }));
        builder.CloseComponent();
    });

    private static string TriggerText(IRenderedComponent<IComponent> cut)
        => cut.Find("[data-slot=\"select-trigger\"]").TextContent.Trim();

    [Fact]
    public void Closed_Trigger_Shows_The_Label_Of_A_Preselected_Value()
    {
        var cut = RenderComposition("Ingoing");

        Assert.Contains("Eingehend", TriggerText(cut));
        Assert.DoesNotContain("Wählen", TriggerText(cut));
        // still closed: no option is rendered, the items only registered their labels
        Assert.Empty(cut.FindAll("[role=\"option\"]"));
    }

    [Fact]
    public void Nothing_Selected_Still_Shows_The_Placeholder()
    {
        var cut = RenderComposition(null);
        Assert.Contains("Wählen", TriggerText(cut));
    }

    [Fact]
    public void Opening_Renders_Each_Item_Once()
    {
        var cut = RenderComposition("Ingoing");
        cut.Find("[data-slot=\"select-trigger\"]").Click();

        // the registration-only pass must not leave duplicates behind once the list is open
        Assert.Equal(2, cut.FindAll("[role=\"option\"]").Count);
        Assert.Contains("Eingehend", TriggerText(cut));
    }

    [Fact]
    public void Data_Bound_Trigger_Shows_ItemText()
    {
        var cut = RenderDataBound("Outgoing");
        Assert.Contains("Ausgehend", TriggerText(cut));
        Assert.Empty(cut.FindAll("[role=\"option\"]"));
    }
}
