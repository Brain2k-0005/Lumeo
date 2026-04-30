using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.TreeView;

public class TreeViewTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TreeViewTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_default_empty()
    {
        var cut = _ctx.Render<L.TreeView<string>>();
        var tree = cut.Find("[role='tree']");
        Assert.NotNull(tree);
    }

    [Fact]
    public void Merges_class_parameter()
    {
        var cut = _ctx.Render<L.TreeView<string>>(p => p.Add(c => c.Class, "tv-cls"));
        var tree = cut.Find("[role='tree']");
        Assert.Contains("tv-cls", tree.GetAttribute("class") ?? "");
    }

    [Fact]
    public void Forwards_additional_attributes()
    {
        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object> { ["data-testid"] = "tree-view" }));
        Assert.Contains("data-testid=\"tree-view\"", cut.Markup);
    }

    [Fact]
    public void Renders_root_items()
    {
        var items = new List<L.TreeView<string>.TreeViewItem<string>>
        {
            new() { Text = "Documents", Value = "docs" },
            new() { Text = "Images", Value = "imgs" }
        };
        var cut = _ctx.Render<L.TreeView<string>>(p => p.Add(c => c.Items, items));
        Assert.Contains("Documents", cut.Markup);
        Assert.Contains("Images", cut.Markup);
    }

    [Fact]
    public void Renders_search_input_when_show_search()
    {
        var cut = _ctx.Render<L.TreeView<string>>(p => p.Add(c => c.ShowSearch, true));
        Assert.NotEmpty(cut.FindAll("input[type='text']"));
    }
}
