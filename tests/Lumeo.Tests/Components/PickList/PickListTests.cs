using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;

namespace Lumeo.Tests.Components.PickList;

public class PickListTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public PickListTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<Lumeo.PickList<string>> RenderList(
        IEnumerable<string>? items = null,
        IEnumerable<string>? selected = null,
        EventCallback<IEnumerable<string>>? onChange = null,
        bool showSearch = true)
    {
        return _ctx.Render<Lumeo.PickList<string>>(p =>
        {
            p.Add(l => l.Items, items ?? new[] { "Alpha", "Bravo", "Charlie" });
            p.Add(l => l.SelectedItems, selected ?? Array.Empty<string>());
            p.Add(l => l.ShowSearch, showSearch);
            if (onChange.HasValue)
                p.Add(l => l.SelectedItemsChanged, onChange.Value);
        });
    }

    [Fact]
    public void Renders_Both_Panels_With_Default_Titles()
    {
        var cut = RenderList();

        Assert.Contains("Available", cut.Markup);
        Assert.Contains("Selected", cut.Markup);
    }

    [Fact]
    public void Source_Shows_All_Items_When_None_Selected()
    {
        var cut = RenderList(
            items: new[] { "Alpha", "Bravo", "Charlie" },
            selected: Array.Empty<string>());

        Assert.Contains("Alpha", cut.Markup);
        Assert.Contains("Bravo", cut.Markup);
        Assert.Contains("Charlie", cut.Markup);
    }

    [Fact]
    public void Selected_Items_Appear_In_Target_Not_Source()
    {
        var cut = RenderList(
            items: new[] { "Alpha", "Bravo", "Charlie" },
            selected: new[] { "Bravo" });

        // Bravo should appear once in the Target panel, not in Source
        // We count occurrences via FindAll on buttons that contain "Bravo"
        var itemButtons = cut.FindAll("button").Where(b => b.TextContent.Trim() == "Bravo").ToList();
        Assert.Single(itemButtons);
    }

    [Fact]
    public void Shows_Empty_State_When_No_Items_Available()
    {
        var cut = RenderList(
            items: new[] { "A" },
            selected: new[] { "A" });

        Assert.Contains("No items", cut.Markup);
    }

    [Fact]
    public void Custom_Titles_Render()
    {
        var cut = _ctx.Render<Lumeo.PickList<string>>(p => p
            .Add(l => l.Items, new[] { "x" })
            .Add(l => l.SourceTitle, "Options")
            .Add(l => l.TargetTitle, "Chosen"));

        Assert.Contains("Options", cut.Markup);
        Assert.Contains("Chosen", cut.Markup);
    }

    [Fact]
    public void ShowSearch_False_Hides_Search_Inputs()
    {
        var cut = RenderList(showSearch: false);

        Assert.Empty(cut.FindAll("input[type='text']"));
    }

    [Fact]
    public void ShowSearch_True_Shows_Two_Search_Inputs()
    {
        var cut = RenderList(showSearch: true);

        Assert.Equal(2, cut.FindAll("input[type='text']").Count);
    }

    [Fact]
    public async Task MoveAll_Button_Moves_All_Source_To_Target()
    {
        IEnumerable<string>? captured = null;
        var cut = RenderList(
            items: new[] { "A", "B", "C" },
            selected: Array.Empty<string>(),
            onChange: EventCallback.Factory.Create<IEnumerable<string>>(this, v => captured = v));

        // First button in middle column = "Move all" (ChevronsRight)
        var moveAllBtn = cut.FindAll("button").First(b => b.GetAttribute("title") == "Move all");
        await moveAllBtn.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.NotNull(captured);
        Assert.Equal(new[] { "A", "B", "C" }, captured!);
    }

    [Fact]
    public async Task Clicking_Source_Item_Then_Move_Selected_Moves_One()
    {
        IEnumerable<string>? captured = null;
        var cut = RenderList(
            items: new[] { "A", "B", "C" },
            selected: Array.Empty<string>(),
            onChange: EventCallback.Factory.Create<IEnumerable<string>>(this, v => captured = v));

        // Click "A" in source
        var aBtn = cut.FindAll("button").First(b => b.TextContent.Trim() == "A");
        await aBtn.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // Click "Move selected" button
        var moveSelected = cut.FindAll("button").First(b => b.GetAttribute("title") == "Move selected");
        await moveSelected.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.NotNull(captured);
        Assert.Contains("A", captured!);
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.PickList<string>>(p => p
            .Add(l => l.Items, new[] { "x" })
            .Add(l => l.Class, "custom-picklist"));

        Assert.Contains("custom-picklist", cut.Find("div").GetAttribute("class"));
    }

    [Fact]
    public void Additional_Attributes_Forward()
    {
        var cut = _ctx.Render<Lumeo.PickList<string>>(p => p
            .Add(l => l.Items, new[] { "x" })
            .Add(l => l.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "picklist"
            }));

        Assert.Equal("picklist", cut.Find("div").GetAttribute("data-testid"));
    }

    [Fact]
    public void Height_Prop_Applies_To_Scrollable_Containers()
    {
        var cut = _ctx.Render<Lumeo.PickList<string>>(p => p
            .Add(l => l.Items, new[] { "x" })
            .Add(l => l.Height, "500px"));

        var scrollers = cut.FindAll("[style*='height:500px']");
        Assert.NotEmpty(scrollers);
    }
}
