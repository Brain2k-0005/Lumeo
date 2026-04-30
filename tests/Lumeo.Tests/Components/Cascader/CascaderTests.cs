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
}
