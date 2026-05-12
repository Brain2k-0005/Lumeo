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

    [Fact]
    public void Search_filters_items_live()
    {
        var items = new List<L.TreeView<string>.TreeViewItem<string>>
        {
            new() { Text = "Documents", Value = "docs" },
            new() { Text = "Images",    Value = "imgs" },
            new() { Text = "Downloads", Value = "dl"   }
        };

        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, items)
            .Add(c => c.ShowSearch, true));

        // All items visible initially.
        Assert.Contains("Documents",  cut.Markup);
        Assert.Contains("Images",     cut.Markup);
        Assert.Contains("Downloads",  cut.Markup);

        // Type "mage" — only "Images" contains it (case-insensitive).
        cut.Find("input[type='text']").Input("mage");

        Assert.DoesNotContain("Documents", cut.Markup);
        Assert.Contains("Images",          cut.Markup);
        Assert.DoesNotContain("Downloads", cut.Markup);

        // Clear search — all items return.
        cut.Find("input[type='text']").Input("");

        Assert.Contains("Documents",  cut.Markup);
        Assert.Contains("Images",     cut.Markup);
        Assert.Contains("Downloads",  cut.Markup);
    }

    [Fact]
    public void Search_filters_nested_children()
    {
        var items = new List<L.TreeView<string>.TreeViewItem<string>>
        {
            new()
            {
                Text = "Root", Value = "root",
                Children =
                [
                    new() { Text = "Alpha", Value = "alpha" },
                    new() { Text = "Beta",  Value = "beta"  }
                ]
            }
        };

        var cut = _ctx.Render<L.TreeView<string>>(p => p
            .Add(c => c.Items, items)
            .Add(c => c.ShowSearch, true)
            .Add(c => c.Expandable, true));

        // Search for child that matches; parent should also be present.
        cut.Find("input[type='text']").Input("Alpha");

        Assert.Contains("Root",  cut.Markup);
        Assert.Contains("Alpha", cut.Markup);
    }
}
