using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;

namespace Lumeo.Tests.Components.Sortable;

public class SortableListTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SortableListTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_All_Items_With_Default_Template()
    {
        var cut = _ctx.Render<Lumeo.SortableList<string>>(p => p
            .Add(l => l.Items, new List<string> { "Alpha", "Bravo", "Charlie" })
            .Add(l => l.ItemTemplate, (RenderFragment<string>)(item => builder =>
            {
                builder.AddContent(0, item);
            })));

        Assert.Contains("Alpha", cut.Markup);
        Assert.Contains("Bravo", cut.Markup);
        Assert.Contains("Charlie", cut.Markup);
    }

    [Fact]
    public void Renders_With_Key_Parameter_And_Stable_Dom()
    {
        // Providing a Key selector should not break rendering; each item div
        // should be present and the list should contain all three items.
        var cut = _ctx.Render<Lumeo.SortableList<string>>(p => p
            .Add(l => l.Items, new List<string> { "X", "Y", "Z" })
            .Add(l => l.Key, (Func<string, object>)(item => item))
            .Add(l => l.ItemTemplate, (RenderFragment<string>)(item => builder =>
            {
                builder.AddContent(0, item);
            })));

        var draggableDivs = cut.FindAll("[draggable='true']");
        Assert.Equal(3, draggableDivs.Count);
    }

    [Fact]
    public void Disabled_True_Sets_Draggable_False()
    {
        var cut = _ctx.Render<Lumeo.SortableList<string>>(p => p
            .Add(l => l.Items, new List<string> { "A", "B" })
            .Add(l => l.Disabled, true));

        var draggableFalse = cut.FindAll("[draggable='false']");
        Assert.Equal(2, draggableFalse.Count);
    }

    [Fact]
    public void Custom_Class_Is_Applied_To_Root()
    {
        var cut = _ctx.Render<Lumeo.SortableList<string>>(p => p
            .Add(l => l.Items, new List<string> { "A" })
            .Add(l => l.Class, "my-sortable"));

        var root = cut.Find("div");
        Assert.Contains("my-sortable", root.GetAttribute("class"));
    }

    [Fact]
    public void Empty_Items_List_Renders_Empty_Container()
    {
        var cut = _ctx.Render<Lumeo.SortableList<string>>(p => p
            .Add(l => l.Items, new List<string>()));

        // Root div should render but contain no draggable items
        Assert.Empty(cut.FindAll("[draggable]"));
    }
}
