using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Cascader;

public class CascaderTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public CascaderTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_default()
    {
        var cut = _ctx.Render<L.Cascader>();
        var button = cut.Find("button");
        Assert.NotNull(button);
    }

    [Fact]
    public void Merges_class_parameter()
    {
        var cut = _ctx.Render<L.Cascader>(p => p.Add(c => c.Class, "custom-cascader"));
        Assert.Contains("custom-cascader", cut.Markup);
    }

    [Fact]
    public void Forwards_additional_attributes()
    {
        var cut = _ctx.Render<L.Cascader>(p => p
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object> { ["data-testid"] = "cas-root" }));
        Assert.Contains("data-testid=\"cas-root\"", cut.Markup);
    }

    [Fact]
    public void Shows_placeholder_when_no_value()
    {
        var cut = _ctx.Render<L.Cascader>(p => p.Add(c => c.Placeholder, "Pick one"));
        Assert.Contains("Pick one", cut.Markup);
    }

    [Fact]
    public void Shows_selected_labels_when_value_set()
    {
        var options = new List<L.Cascader.CascaderOption>
        {
            new() { Label = "Fruit", Value = "fruit" }
        };
        var cut = _ctx.Render<L.Cascader>(p => p
            .Add(c => c.Options, options)
            .Add(c => c.Value, new List<string> { "fruit" }));
        Assert.Contains("Fruit", cut.Markup);
    }

    // Part of D: the search box is now the Lumeo Input component (Variant=Search), not a raw
    // <input> — assert its data-slot is present and its size classes match a standalone Input at
    // the same rung (Cascader has no Size parameter of its own; Xs is the fixed rung chosen to
    // match the box's previous h-7 visual size).
    [Fact]
    public void Search_box_is_a_lumeo_input_sized_like_the_standalone_component()
    {
        var cut = _ctx.Render<L.Cascader>(p => p.Add(c => c.ShowSearch, true));
        cut.Find("button").Click();

        var searchInput = cut.Find("input[type='search']");
        Assert.Equal("input-control", searchInput.GetAttribute("data-slot"));

        var standalone = _ctx.Render<L.Input>(p => p
            .Add(c => c.Size, L.Size.Xs)
            .Add(c => c.Variant, L.Input.InputVariant.Search));
        var standaloneInput = standalone.Find("input[type='search']");

        Assert.Equal(standaloneInput.GetAttribute("class"), searchInput.GetAttribute("class"));
    }
}
