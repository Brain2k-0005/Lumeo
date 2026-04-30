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
}
