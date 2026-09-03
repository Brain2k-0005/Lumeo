using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Tabs;

/// <summary>Field report 4.15: shadcn's Tabs root is <c>flex gap-2</c> (column-wise when
/// horizontal), the list keeps its own width and the panel fills the rest.</summary>
public class TabsRootLayoutTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TabsRootLayoutTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> Render(L.Orientation orientation) => _ctx.Render(builder =>
    {
        builder.OpenComponent<L.Tabs>(0);
        builder.AddAttribute(1, "ActiveValue", "one");
        builder.AddAttribute(2, "Orientation", orientation);
        builder.AddAttribute(3, "ChildContent", (RenderFragment)(b =>
        {
            b.OpenComponent<L.TabsList>(0);
            b.AddAttribute(1, "ChildContent", (RenderFragment)(l =>
            {
                l.OpenComponent<L.TabsTrigger>(0);
                l.AddAttribute(1, "Value", "one");
                l.AddAttribute(2, "ChildContent", (RenderFragment)(t => t.AddContent(0, "One")));
                l.CloseComponent();
            }));
            b.CloseComponent();
            b.OpenComponent<L.TabsContent>(2);
            b.AddAttribute(3, "Value", "one");
            b.AddAttribute(4, "ChildContent", (RenderFragment)(t => t.AddContent(0, "Panel")));
            b.CloseComponent();
        }));
        builder.CloseComponent();
    });

    [Fact]
    public void Horizontal_Root_Is_A_Flex_Column_With_Gap_2()
    {
        var cut = Render(L.Orientation.Horizontal);
        var cls = cut.Find("[data-slot=\"tabs\"]").GetAttribute("class") ?? "";
        Assert.Contains("flex", cls.Split(' '));
        Assert.Contains("flex-col", cls.Split(' '));
        Assert.Contains("gap-2", cls.Split(' '));
    }

    [Fact]
    public void Vertical_Root_Is_A_Flex_Row_With_Gap_2()
    {
        var cut = Render(L.Orientation.Vertical);
        var cls = cut.Find("[data-slot=\"tabs\"]").GetAttribute("class") ?? "";
        Assert.Contains("flex", cls.Split(' '));
        Assert.Contains("flex-row", cls.Split(' '));
        Assert.Contains("gap-2", cls.Split(' '));
    }

    [Fact]
    public void List_Keeps_Its_Own_Width_And_Panel_Fills_The_Rest()
    {
        var cut = Render(L.Orientation.Horizontal);
        Assert.Contains("w-fit", (cut.Find("[data-slot=\"tabs-list\"]").GetAttribute("class") ?? "").Split(' '));
        Assert.Contains("flex-1", (cut.Find("[data-slot=\"tabs-content\"]").GetAttribute("class") ?? "").Split(' '));
    }
}
