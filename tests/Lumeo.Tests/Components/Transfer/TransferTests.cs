using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Transfer;

public class TransferTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TransferTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_default()
    {
        var cut = _ctx.Render<L.Transfer>();
        // Two panels should be rendered
        var panels = cut.FindAll("div.flex-1");
        Assert.True(panels.Count >= 2);
    }

    [Fact]
    public void Merges_class_parameter()
    {
        var cut = _ctx.Render<L.Transfer>(p => p.Add(c => c.Class, "transfer-cls"));
        Assert.Contains("transfer-cls", cut.Markup);
    }

    [Fact]
    public void Forwards_additional_attributes()
    {
        var cut = _ctx.Render<L.Transfer>(p => p
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object> { ["data-testid"] = "transfer" }));
        Assert.Contains("data-testid=\"transfer\"", cut.Markup);
    }

    [Fact]
    public void Renders_source_items()
    {
        var sourceItems = new List<L.Transfer.TransferItem>
        {
            new("Apple", "apple"),
            new("Banana", "banana")
        };
        var cut = _ctx.Render<L.Transfer>(p => p.Add(c => c.SourceItems, sourceItems));
        Assert.Contains("Apple", cut.Markup);
        Assert.Contains("Banana", cut.Markup);
    }

    [Fact]
    public void Shows_custom_source_title()
    {
        var cut = _ctx.Render<L.Transfer>(p => p.Add(c => c.SourceTitle, "Available items"));
        Assert.Contains("Available items", cut.Markup);
    }

    // Part of D: both pane search boxes are now the Lumeo Input component (Variant=Search), not
    // a raw <input> — assert the data-slot is present and the size classes match a standalone
    // Input at the same rung (Transfer has no Size parameter of its own; Xs is the fixed rung
    // chosen to match the boxes' previous h-7 visual size).
    [Fact]
    public void Search_boxes_are_lumeo_inputs_sized_like_the_standalone_component()
    {
        var cut = _ctx.Render<L.Transfer>(p => p.Add(c => c.ShowSearch, true));

        var searchInputs = cut.FindAll("input[type='search']");
        Assert.Equal(2, searchInputs.Count);
        Assert.All(searchInputs, i => Assert.Equal("input-control", i.GetAttribute("data-slot")));

        var standalone = _ctx.Render<L.Input>(p => p
            .Add(c => c.Size, L.Size.Xs)
            .Add(c => c.Variant, L.Input.InputVariant.Search));
        var standaloneInput = standalone.Find("input[type='search']");

        Assert.All(searchInputs, i => Assert.Equal(standaloneInput.GetAttribute("class"), i.GetAttribute("class")));
    }
}
