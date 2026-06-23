using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Chip;

public class ChipTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public ChipTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Child_Content()
    {
        var cut = _ctx.Render<L.Chip>(p => p.AddChildContent("Tag"));
        Assert.Contains("Tag", cut.Markup);
    }

    [Fact]
    public void Clickable_Gets_Button_Role_And_Is_Focusable()
    {
        var cut = _ctx.Render<L.Chip>(p => p.Add(c => c.Clickable, true).AddChildContent("X"));
        var root = cut.Find("div");
        Assert.Equal("button", root.GetAttribute("role"));
        Assert.Equal("0", root.GetAttribute("tabindex"));
    }

    [Fact]
    public void Non_Clickable_Chip_Has_No_Button_Role()
    {
        var cut = _ctx.Render<L.Chip>(p => p.AddChildContent("X"));
        Assert.False(cut.Find("div").HasAttribute("role"));
    }

    [Fact]
    public void Clickable_Fires_OnClick()
    {
        var clicked = false;
        var cut = _ctx.Render<L.Chip>(p => p
            .Add(c => c.Clickable, true)
            .Add(c => c.OnClick, EventCallback.Factory.Create(this, () => clicked = true))
            .AddChildContent("X"));

        cut.Find("[role='button']").Click();

        Assert.True(clicked);
    }

    [Fact]
    public void Closable_Renders_A_Close_Button_That_Fires_OnClose()
    {
        var closed = false;
        var cut = _ctx.Render<L.Chip>(p => p
            .Add(c => c.Closable, true)
            .Add(c => c.OnClose, EventCallback.Factory.Create(this, () => closed = true))
            .AddChildContent("X"));

        cut.Find("button").Click();   // the close (X) button

        Assert.True(closed);
    }

    [Fact]
    public void Disabled_Clickable_Chip_Announces_Aria_Disabled()
    {
        var cut = _ctx.Render<L.Chip>(p => p
            .Add(c => c.Clickable, true).Add(c => c.Disabled, true).AddChildContent("X"));
        Assert.Equal("true", cut.Find("div").GetAttribute("aria-disabled"));
    }
}
