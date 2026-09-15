using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.TreeSelect;

public class TreeSelectTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TreeSelectTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_trigger_button()
    {
        var cut = _ctx.Render<L.TreeSelect>();
        var button = cut.Find("button");
        Assert.NotNull(button);
    }

    [Fact]
    public void Merges_class_parameter()
    {
        var cut = _ctx.Render<L.TreeSelect>(p => p.Add(c => c.Class, "ts-cls"));
        Assert.Contains("ts-cls", cut.Markup);
    }

    [Fact]
    public void Forwards_additional_attributes()
    {
        var cut = _ctx.Render<L.TreeSelect>(p => p
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object> { ["data-testid"] = "tree-sel" }));
        Assert.Contains("data-testid=\"tree-sel\"", cut.Markup);
    }

    [Fact]
    public void Shows_placeholder_when_no_value()
    {
        var cut = _ctx.Render<L.TreeSelect>(p => p.Add(c => c.Placeholder, "Choose node"));
        Assert.Contains("Choose node", cut.Markup);
    }

    [Fact]
    public void Shows_tree_items_when_open_clicked()
    {
        var items = new List<L.TreeSelect.TreeSelectItem>
        {
            new() { Label = "Root", Value = "root" }
        };
        var cut = _ctx.Render<L.TreeSelect>(p => p.Add(c => c.Items, items));
        cut.Find("button").Click();
        Assert.Contains("Root", cut.Markup);
    }

    // Part of D: the search box is now the Lumeo Input component (Variant=Search), not a raw
    // <input> — assert its data-slot is present and its size classes match a standalone Input at
    // the same rung (TreeSelect has no Size parameter of its own; Md is the fixed rung chosen to
    // match the box's previous unsized/default height).
    [Fact]
    public void Search_box_is_a_lumeo_input_sized_like_the_standalone_component()
    {
        var items = new List<L.TreeSelect.TreeSelectItem> { new() { Label = "Root", Value = "root" } };
        var cut = _ctx.Render<L.TreeSelect>(p => p
            .Add(c => c.Items, items)
            .Add(c => c.Searchable, true));
        cut.Find("button").Click();

        var searchInput = cut.Find("input[type='search']");
        Assert.Equal("input-control", searchInput.GetAttribute("data-slot"));

        var standalone = _ctx.Render<L.Input>(p => p
            .Add(c => c.Size, L.Size.Md)
            .Add(c => c.Variant, L.Input.InputVariant.Search));
        var standaloneInput = standalone.Find("input[type='search']");

        Assert.Equal(standaloneInput.GetAttribute("class"), searchInput.GetAttribute("class"));
    }
}
