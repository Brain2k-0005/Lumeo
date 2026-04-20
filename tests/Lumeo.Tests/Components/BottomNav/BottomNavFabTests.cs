using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;

namespace Lumeo.Tests.Components.BottomNav;

public class BottomNavFabTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public BottomNavFabTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Without_Href_Renders_As_Button()
    {
        var cut = _ctx.Render<Lumeo.BottomNavFab>(p => p
            .Add(f => f.AriaLabel, "Add"));

        Assert.NotNull(cut.Find("button"));
        Assert.Empty(cut.FindAll("a"));
    }

    [Fact]
    public void With_Href_Renders_As_Anchor()
    {
        var cut = _ctx.Render<Lumeo.BottomNavFab>(p => p
            .Add(f => f.AriaLabel, "Add")
            .Add(f => f.Href, "/new"));

        Assert.Equal("/new", cut.Find("a").GetAttribute("href"));
    }

    [Fact]
    public void AriaLabel_Is_Applied_To_Button()
    {
        var cut = _ctx.Render<Lumeo.BottomNavFab>(p => p
            .Add(f => f.AriaLabel, "Create"));

        Assert.Equal("Create", cut.Find("button").GetAttribute("aria-label"));
    }

    [Fact]
    public void AriaLabel_Is_Applied_To_Anchor()
    {
        var cut = _ctx.Render<Lumeo.BottomNavFab>(p => p
            .Add(f => f.AriaLabel, "Create")
            .Add(f => f.Href, "/new"));

        Assert.Equal("Create", cut.Find("a").GetAttribute("aria-label"));
    }

    [Fact]
    public async Task OnClick_Fires_When_Clicked()
    {
        var fired = false;
        var cut = _ctx.Render<Lumeo.BottomNavFab>(p => p
            .Add(f => f.AriaLabel, "X")
            .Add(f => f.OnClick, EventCallback.Factory.Create(this, () => { fired = true; })));

        await cut.Find("button").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.True(fired);
    }

    [Fact]
    public void ChildContent_Renders()
    {
        var cut = _ctx.Render<Lumeo.BottomNavFab>(p => p
            .Add(f => f.AriaLabel, "X")
            .AddChildContent("<span data-testid='plus'>+</span>"));

        Assert.NotNull(cut.Find("[data-testid='plus']"));
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.BottomNavFab>(p => p
            .Add(f => f.AriaLabel, "X")
            .Add(f => f.Class, "my-fab"));

        Assert.Contains("my-fab", cut.Find("button").GetAttribute("class"));
    }

    [Fact]
    public void Default_Classes_Include_Primary_Styling()
    {
        var cut = _ctx.Render<Lumeo.BottomNavFab>(p => p
            .Add(f => f.AriaLabel, "X"));

        var cls = cut.Find("button").GetAttribute("class");
        Assert.Contains("rounded-full", cls);
        Assert.Contains("bg-primary", cls);
    }

    [Fact]
    public void Additional_Attributes_Forward()
    {
        var cut = _ctx.Render<Lumeo.BottomNavFab>(p => p
            .Add(f => f.AriaLabel, "X")
            .Add(f => f.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "fab"
            }));

        Assert.Equal("fab", cut.Find("button").GetAttribute("data-testid"));
    }
}
