using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Tabs;

/// <summary>
/// Tabs IconReveal mode: inactive triggers are icon-only, the active trigger reveals
/// its text label via the animated <c>.lumeo-tab-label</c> wrapper (open variant on the
/// active one). The label text always stays in the DOM (reachable to AT); only its width
/// animates. With IconReveal off, the label renders bare (no wrapper).
/// </summary>
public class TabsIconRevealTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public TabsIconRevealTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderTabs(bool iconReveal, string active = "one")
        => _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Tabs>(0);
            builder.AddAttribute(1, "ActiveValue", active);
            builder.AddAttribute(2, "IconReveal", iconReveal);
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.TabsList>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner =>
                {
                    void Trigger(int seq, string val, string label)
                    {
                        inner.OpenComponent<L.TabsTrigger>(seq);
                        inner.AddAttribute(seq + 1, "Value", val);
                        inner.AddAttribute(seq + 2, "IconContent", (RenderFragment)(i => i.AddMarkupContent(0, "<svg class=\"icon\"></svg>")));
                        inner.AddAttribute(seq + 3, "ChildContent", (RenderFragment)(t => t.AddContent(0, label)));
                        inner.CloseComponent();
                    }
                    Trigger(0, "one", "First");
                    Trigger(10, "two", "Second");
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

    [Fact]
    public void Only_The_Active_Trigger_Label_Is_Open()
    {
        var cut = RenderTabs(iconReveal: true, active: "one");

        // Both labels are wrapped (always in the DOM); only the active one is "open".
        Assert.Equal(2, cut.FindAll(".lumeo-tab-label").Count);
        var open = cut.FindAll(".lumeo-tab-label-open");
        Assert.Single(open);
        Assert.Equal("First", open[0].TextContent.Trim());

        // The inactive label text is still present (reachable to AT, just collapsed).
        Assert.Contains("Second", cut.Markup);
    }

    [Fact]
    public void Switching_Active_Moves_The_Open_Label()
    {
        var cut = RenderTabs(iconReveal: true, active: "two");
        var open = cut.FindAll(".lumeo-tab-label-open");
        Assert.Single(open);
        Assert.Equal("Second", open[0].TextContent.Trim());
    }

    [Fact]
    public void Without_IconReveal_Labels_Render_Bare()
    {
        var cut = RenderTabs(iconReveal: false);

        // No reveal wrapper at all — labels are plain text.
        Assert.Empty(cut.FindAll(".lumeo-tab-label"));
        Assert.Contains("First", cut.Markup);
        Assert.Contains("Second", cut.Markup);
    }
}
