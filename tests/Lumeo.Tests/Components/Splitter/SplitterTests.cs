using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using L = Lumeo;

namespace Lumeo.Tests.Components.Splitter;

public class SplitterTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SplitterTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderSplitter(
        L.Splitter.SplitterOrientation orientation = L.Splitter.SplitterOrientation.Horizontal,
        int paneCount = 2)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Splitter>(0);
            builder.AddAttribute(1, "Orientation", orientation);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                for (var i = 0; i < paneCount; i++)
                {
                    var idx = i;
                    b.OpenComponent<L.SplitterPane>(idx);
                    b.AddAttribute(idx * 10 + 1, "ChildContent", (RenderFragment)(inner =>
                        inner.AddMarkupContent(0, $"<span data-testid='pane-{idx}'>pane {idx}</span>")));
                    b.CloseComponent();
                }
            }));
            builder.CloseComponent();
        });
    }

    [Fact]
    public void Horizontal_Orientation_Has_Flex_Row_Class()
    {
        var cut = RenderSplitter(L.Splitter.SplitterOrientation.Horizontal);

        var root = cut.Find("[data-splitter-orientation]");
        Assert.Contains("flex-row", root.GetAttribute("class"));
    }

    [Fact]
    public void Vertical_Orientation_Has_Flex_Col_Class()
    {
        var cut = RenderSplitter(L.Splitter.SplitterOrientation.Vertical);

        var root = cut.Find("[data-splitter-orientation]");
        Assert.Contains("flex-col", root.GetAttribute("class"));
    }

    [Fact]
    public void Root_Has_Orientation_Data_Attribute()
    {
        var cut = RenderSplitter(L.Splitter.SplitterOrientation.Horizontal);

        Assert.Equal("horizontal", cut.Find("[data-splitter-orientation]").GetAttribute("data-splitter-orientation"));
    }

    [Fact]
    public void Vertical_Orientation_Data_Attribute_Is_Vertical()
    {
        var cut = RenderSplitter(L.Splitter.SplitterOrientation.Vertical);

        Assert.Equal("vertical", cut.Find("[data-splitter-orientation]").GetAttribute("data-splitter-orientation"));
    }

    [Fact]
    public void Children_Render_Inside_Splitter()
    {
        var cut = RenderSplitter(paneCount: 2);

        Assert.NotNull(cut.Find("[data-testid='pane-0']"));
        Assert.NotNull(cut.Find("[data-testid='pane-1']"));
    }

    [Fact]
    public void Root_Has_Full_Size_Layout_Classes()
    {
        var cut = RenderSplitter();

        var cls = cut.Find("[data-splitter-orientation]").GetAttribute("class");
        Assert.Contains("flex", cls);
        Assert.Contains("h-full", cls);
        Assert.Contains("w-full", cls);
    }

    [Fact]
    public void Root_Has_Min_Zero_Inline_Style()
    {
        var cut = RenderSplitter();

        var style = cut.Find("[data-splitter-orientation]").GetAttribute("style");
        Assert.Contains("min-width:0", style);
        Assert.Contains("min-height:0", style);
    }
}
