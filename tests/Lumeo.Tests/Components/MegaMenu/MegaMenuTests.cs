using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.MegaMenu;

public class MegaMenuTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public MegaMenuTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_default()
    {
        var cut = _ctx.Render<L.MegaMenu>();
        var nav = cut.Find("nav");
        Assert.NotNull(nav);
    }

    [Fact]
    public void Merges_class_parameter()
    {
        var cut = _ctx.Render<L.MegaMenu>(p => p.Add(c => c.Class, "mega-cls"));
        Assert.Contains("mega-cls", cut.Markup);
    }

    [Fact]
    public void Forwards_additional_attributes()
    {
        var cut = _ctx.Render<L.MegaMenu>(p => p
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object> { ["data-testid"] = "mega" }));
        Assert.Contains("data-testid=\"mega\"", cut.Markup);
    }

    [Fact]
    public void Vertical_orientation_applies_flex_col()
    {
        var cut = _ctx.Render<L.MegaMenu>(p => p
            .Add(c => c.Orientation, L.MegaMenu.MegaMenuOrientation.Vertical));
        Assert.Contains("flex-col", cut.Markup);
    }

    [Fact]
    public void Renders_child_items()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.MegaMenu>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenElement(0, "li");
                b.AddAttribute(1, "class", "menu-item-test");
                b.AddContent(2, "Products");
                b.CloseElement();
            }));
            builder.CloseComponent();
        });
        Assert.Contains("menu-item-test", cut.Markup);
        Assert.Contains("Products", cut.Markup);
    }
}
