using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.List;

public class ListItemTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ListItemTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Href_Renders_Anchor_With_Href()
    {
        var cut = _ctx.Render<L.ListItem>(p => p
            .Add(i => i.Href, "/details")
            .Add(i => i.Title, "Item"));

        Assert.Equal("/details", cut.Find("a").GetAttribute("href"));
    }

    [Fact]
    public void OnClick_Fires_When_Href_Is_Set()
    {
        // Regression: the anchor branch silently dropped OnClick.
        var clicked = false;
        var cut = _ctx.Render<L.ListItem>(p => p
            .Add(i => i.Href, "/details")
            .Add(i => i.Title, "Item")
            .Add(i => i.OnClick, () => clicked = true));

        cut.Find("a").Click();

        Assert.True(clicked);
        // Navigation must not be suppressed — the href stays intact.
        Assert.Equal("/details", cut.Find("a").GetAttribute("href"));
    }

    [Fact]
    public void OnClick_Does_Not_Fire_When_Disabled_With_Href()
    {
        var clicked = false;
        var cut = _ctx.Render<L.ListItem>(p => p
            .Add(i => i.Href, "/details")
            .Add(i => i.Disabled, true)
            .Add(i => i.OnClick, () => clicked = true));

        cut.Find("a").Click();

        Assert.False(clicked);
    }

    [Fact]
    public void OnClick_Fires_Without_Href()
    {
        var clicked = false;
        var cut = _ctx.Render<L.ListItem>(p => p
            .Add(i => i.Title, "Item")
            .Add(i => i.OnClick, () => clicked = true));

        cut.Find("li").Click();

        Assert.True(clicked);
    }
}
