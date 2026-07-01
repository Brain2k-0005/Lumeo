using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Card;

/// <summary>
/// CardTitle + CardDescription complete the shadcn Card composition
/// (Card &gt; CardHeader &gt; CardTitle + CardDescription) — CardTitle is a heading,
/// CardDescription is muted secondary text.
/// </summary>
public class CardTitleDescriptionTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public CardTitleDescriptionTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void CardTitle_Renders_A_Heading_With_The_Text()
    {
        var cut = _ctx.Render<L.CardTitle>(p => p.AddChildContent("Account"));
        var h3 = cut.Find("h3");
        Assert.Equal("Account", h3.TextContent.Trim());
        Assert.Contains("font-semibold", h3.GetAttribute("class") ?? "");
    }

    [Fact]
    public void CardDescription_Renders_Muted_Paragraph_Text()
    {
        var cut = _ctx.Render<L.CardDescription>(p => p.AddChildContent("Manage your account."));
        var pEl = cut.Find("p");
        Assert.Equal("Manage your account.", pEl.TextContent.Trim());
        Assert.Contains("text-muted-foreground", pEl.GetAttribute("class") ?? "");
    }

    [Fact]
    public void Consumer_Class_Wins_On_The_Title_Via_Cx_Merge()
    {
        var cut = _ctx.Render<L.CardTitle>(p => p
            .Add(t => t.Class, "text-2xl")
            .AddChildContent("Big"));
        Assert.Contains("text-2xl", cut.Find("h3").GetAttribute("class") ?? "");
    }
}
