using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using L = Lumeo;

namespace Lumeo.Tests.Components.Splitter;

public class SplitterPaneTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SplitterPaneTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderWithPane(double size = 0, double minSize = 10, double maxSize = 90)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Splitter>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SplitterPane>(0);
                b.AddAttribute(1, "Size", size);
                b.AddAttribute(2, "MinSize", minSize);
                b.AddAttribute(3, "MaxSize", maxSize);
                b.AddAttribute(4, "ChildContent", (RenderFragment)(inner =>
                    inner.AddMarkupContent(0, "<span data-testid='pane-body'>body</span>")));
                b.CloseComponent();

                // A second pane so the distribute logic has work
                b.OpenComponent<L.SplitterPane>(10);
                b.AddAttribute(11, "Size", 0.0);
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    [Fact]
    public void Pane_Renders_ChildContent()
    {
        var cut = RenderWithPane(size: 50);

        Assert.NotNull(cut.Find("[data-testid='pane-body']"));
    }

    [Fact]
    public void Pane_With_Explicit_Size_Produces_Flex_Inline_Style()
    {
        // Splitter switched from `flex: 0 0 X%` (broken in vertical mode without explicit parent height)
        // to `flex: X 1 0` — grow ratio instead of flex-basis %.
        var cut = RenderWithPane(size: 40);

        var style = cut.Find("[data-testid='pane-body']").ParentElement!.GetAttribute("style");
        Assert.Contains("flex:", style);
        Assert.Contains("40", style); // size value now appears as the grow ratio
    }

    [Fact]
    public void Pane_With_Zero_Size_Gets_Redistributed_Share()
    {
        // Two panes, both size 0 → should each get 50% after distribution
        var cut = RenderWithPane(size: 0);

        var panes = cut.FindAll("[style*='flex:']");
        Assert.Equal(2, panes.Count);
        // Both should have ~50 in the style (as the grow ratio)
        Assert.All(panes, p =>
        {
            var s = p.GetAttribute("style") ?? "";
            Assert.Contains("50", s);
        });
    }

    [Fact]
    public void Pane_Has_Overflow_Hidden_Class()
    {
        var cut = RenderWithPane(size: 50);

        var pane = cut.Find("[data-testid='pane-body']").ParentElement!;
        Assert.Contains("overflow-hidden", pane.GetAttribute("class"));
    }

    [Fact]
    public void Pane_Inline_Style_Includes_Min_Zero()
    {
        var cut = RenderWithPane(size: 50);

        var style = cut.Find("[data-testid='pane-body']").ParentElement!.GetAttribute("style");
        Assert.Contains("min-width: 0", style);
        Assert.Contains("min-height: 0", style);
    }

    [Fact]
    public void Pane_Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Splitter>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SplitterPane>(0);
                b.AddAttribute(1, "Class", "my-pane");
                b.AddAttribute(2, "ChildContent", (RenderFragment)(inner =>
                    inner.AddMarkupContent(0, "<span data-testid='x'>x</span>")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var pane = cut.Find("[data-testid='x']").ParentElement!;
        Assert.Contains("my-pane", pane.GetAttribute("class"));
    }
}
