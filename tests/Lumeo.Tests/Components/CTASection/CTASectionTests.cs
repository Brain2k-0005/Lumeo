using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.CTASection;

public class CTASectionTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public CTASectionTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Title_Renders_In_An_H2_That_Labels_The_Section()
    {
        var cut = _ctx.Render<L.CTASection>(p => p.Add(c => c.Title, "Get started"));

        var h2 = cut.Find("h2");
        Assert.Equal("Get started", h2.TextContent.Trim());
        Assert.Equal(h2.GetAttribute("id"), cut.Find("section").GetAttribute("aria-labelledby"));
    }

    [Fact]
    public void Subtitle_Renders()
    {
        var cut = _ctx.Render<L.CTASection>(p => p
            .Add(c => c.Title, "T")
            .Add(c => c.Subtitle, "Sign up in seconds"));
        Assert.Contains("Sign up in seconds", cut.Markup);
    }
}
