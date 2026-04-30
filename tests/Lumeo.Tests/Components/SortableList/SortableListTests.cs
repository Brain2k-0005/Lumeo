using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.SortableList;

public class SortableListTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SortableListTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_default_with_items()
    {
        var items = new List<string> { "Alpha", "Beta", "Gamma" };
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.SortableList<string>>(0);
            builder.AddAttribute(1, "Items", items);
            builder.AddAttribute(2, "ItemTemplate", (RenderFragment<string>)(item => b =>
            {
                b.AddContent(0, item);
            }));
            builder.CloseComponent();
        });
        Assert.Contains("Alpha", cut.Markup);
        Assert.Contains("Beta", cut.Markup);
        Assert.Contains("Gamma", cut.Markup);
    }

    [Fact]
    public void Merges_class_parameter()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.SortableList<string>>(0);
            builder.AddAttribute(1, "Items", new List<string>());
            builder.AddAttribute(2, "Class", "sortable-cls");
            builder.CloseComponent();
        });
        Assert.Contains("sortable-cls", cut.Markup);
    }

    [Fact]
    public void Forwards_additional_attributes()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.SortableList<string>>(0);
            builder.AddAttribute(1, "Items", new List<string>());
            builder.AddAttribute(2, "AdditionalAttributes", new Dictionary<string, object>
            {
                ["data-testid"] = "sortable"
            });
            builder.CloseComponent();
        });
        Assert.Contains("data-testid=\"sortable\"", cut.Markup);
    }

    [Fact]
    public void Items_are_draggable_when_not_disabled()
    {
        var items = new List<string> { "Item1", "Item2" };
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.SortableList<string>>(0);
            builder.AddAttribute(1, "Items", items);
            builder.AddAttribute(2, "ItemTemplate", (RenderFragment<string>)(item => b =>
            {
                b.AddContent(0, item);
            }));
            builder.CloseComponent();
        });
        var draggableDivs = cut.FindAll("[draggable='true']");
        Assert.Equal(2, draggableDivs.Count);
    }
}
