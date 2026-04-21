using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;

namespace Lumeo.Tests.Components.BottomNav;

public class BottomNavItemTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public BottomNavItemTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Without_Href_Renders_As_Button()
    {
        var cut = _ctx.Render<Lumeo.BottomNavItem>(p => p
            .Add(i => i.Label, "Home"));

        Assert.NotNull(cut.Find("button"));
        Assert.Empty(cut.FindAll("a"));
    }

    [Fact]
    public void With_Href_Renders_As_Anchor()
    {
        var cut = _ctx.Render<Lumeo.BottomNavItem>(p => p
            .Add(i => i.Href, "/home")
            .Add(i => i.Label, "Home"));

        var a = cut.Find("a");
        Assert.Equal("/home", a.GetAttribute("href"));
    }

    [Fact]
    public void Label_Is_Rendered()
    {
        var cut = _ctx.Render<Lumeo.BottomNavItem>(p => p
            .Add(i => i.Label, "Home"));

        Assert.Contains("Home", cut.Markup);
    }

    [Fact]
    public void Label_Is_Used_As_AriaLabel()
    {
        var cut = _ctx.Render<Lumeo.BottomNavItem>(p => p
            .Add(i => i.Label, "Profile"));

        Assert.Equal("Profile", cut.Find("button").GetAttribute("aria-label"));
    }

    [Fact]
    public void IsActive_True_Sets_AriaCurrent_Page()
    {
        var cut = _ctx.Render<Lumeo.BottomNavItem>(p => p
            .Add(i => i.IsActive, true)
            .Add(i => i.Label, "Home"));

        Assert.Equal("page", cut.Find("button").GetAttribute("aria-current"));
    }

    [Fact]
    public void IsActive_False_Has_No_AriaCurrent()
    {
        var cut = _ctx.Render<Lumeo.BottomNavItem>(p => p
            .Add(i => i.IsActive, false)
            .Add(i => i.Label, "Home"));

        Assert.Null(cut.Find("button").GetAttribute("aria-current"));
    }

    [Fact]
    public void Badge_Slot_Renders_When_Provided()
    {
        var cut = _ctx.Render<Lumeo.BottomNavItem>(p => p
            .Add(i => i.Label, "Inbox")
            .Add(i => i.Badge, (RenderFragment)(b => b.AddContent(0, "3"))));

        Assert.Contains("3", cut.Markup);
    }

    [Fact]
    public void IconContent_Renders_When_Provided()
    {
        var cut = _ctx.Render<Lumeo.BottomNavItem>(p => p
            .Add(i => i.IconContent, (RenderFragment)(b =>
            {
                b.OpenElement(0, "svg");
                b.AddAttribute(1, "data-testid", "icon");
                b.CloseElement();
            })));

        Assert.NotNull(cut.Find("[data-testid='icon']"));
    }

    [Fact]
    public async Task OnClick_Fires_When_Button_Clicked()
    {
        var fired = false;
        var cut = _ctx.Render<Lumeo.BottomNavItem>(p => p
            .Add(i => i.Label, "Home")
            .Add(i => i.OnClick, EventCallback.Factory.Create(this, () => { fired = true; })));

        await cut.Find("button").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.True(fired);
    }

    [Fact]
    public void Class_Is_Appended_To_Root()
    {
        var cut = _ctx.Render<Lumeo.BottomNavItem>(p => p
            .Add(i => i.Label, "X")
            .Add(i => i.Class, "custom-item"));

        Assert.Contains("custom-item", cut.Find("button").GetAttribute("class"));
    }

    [Fact]
    public void Additional_Attributes_Forward_To_Button()
    {
        var cut = _ctx.Render<Lumeo.BottomNavItem>(p => p
            .Add(i => i.Label, "X")
            .Add(i => i.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "item"
            }));

        Assert.Equal("item", cut.Find("button").GetAttribute("data-testid"));
    }
}
