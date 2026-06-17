using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Icon;

public class IconTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public IconTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // --- Decorative-by-default a11y (#286) ---

    [Fact]
    public void Decorative_By_Default_Is_Aria_Hidden()
    {
        var cut = _ctx.Render<L.Icon>(p => p.Add(i => i.Name, "Search"));
        var svg = cut.Find("svg");
        Assert.Equal("true", svg.GetAttribute("aria-hidden"));
        Assert.Null(svg.GetAttribute("role"));
    }

    [Fact]
    public void Title_Promotes_To_Role_Img_With_Aria_Label()
    {
        var cut = _ctx.Render<L.Icon>(p => p
            .Add(i => i.Name, "Search")
            .Add(i => i.Title, "Search"));
        var svg = cut.Find("svg");
        Assert.Equal("img", svg.GetAttribute("role"));
        Assert.Equal("Search", svg.GetAttribute("aria-label"));
        // Not hidden when it has an accessible name.
        Assert.Null(svg.GetAttribute("aria-hidden"));
    }

    [Fact]
    public void Title_Also_Sets_Title_Attribute()
    {
        var cut = _ctx.Render<L.Icon>(p => p
            .Add(i => i.Name, "Search")
            .Add(i => i.Title, "Find things"));
        Assert.Equal("Find things", cut.Find("svg").GetAttribute("title"));
    }

    [Fact]
    public void Consumer_Aria_Label_Wins_Over_Default_Hidden()
    {
        var cut = _ctx.Render<L.Icon>(p => p
            .Add(i => i.Name, "Search")
            .Add(i => i.AdditionalAttributes, new Dictionary<string, object>
            {
                ["aria-label"] = "Custom",
            }));
        var svg = cut.Find("svg");
        Assert.Equal("Custom", svg.GetAttribute("aria-label"));
        // We did NOT also stamp aria-hidden when the consumer provided a label.
        Assert.Null(svg.GetAttribute("aria-hidden"));
    }

    [Fact]
    public void Consumer_Aria_Hidden_Wins_Over_Title()
    {
        var cut = _ctx.Render<L.Icon>(p => p
            .Add(i => i.Name, "Search")
            .Add(i => i.Title, "ignored-because-consumer-hid-it")
            .Add(i => i.AdditionalAttributes, new Dictionary<string, object>
            {
                ["aria-hidden"] = "true",
            }));
        var svg = cut.Find("svg");
        Assert.Equal("true", svg.GetAttribute("aria-hidden"));
        // Our role=img default is suppressed because the consumer took control.
        Assert.Null(svg.GetAttribute("role"));
    }

    [Fact]
    public void Size_Class_Still_Applies_With_A11y()
    {
        var cut = _ctx.Render<L.Icon>(p => p
            .Add(i => i.Name, "Search")
            .Add(i => i.Size, L.Size.Lg));
        var cls = cut.Find("svg").GetAttribute("class") ?? "";
        Assert.Contains("h-5", cls);
        Assert.Contains("w-5", cls);
    }
}
