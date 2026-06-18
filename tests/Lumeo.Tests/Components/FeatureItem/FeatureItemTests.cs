using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.FeatureItem;

public class FeatureItemTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public FeatureItemTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Title_And_Description()
    {
        var cut = _ctx.Render<L.FeatureItem>(p => p
            .Add(f => f.Title, "Theming")
            .Add(f => f.Description, "CSS variables"));
        Assert.Contains("Theming", cut.Markup);
        Assert.Contains("CSS variables", cut.Markup);
    }

    // --- Icon param shadow fix (#300) ---

    [Fact]
    public void IconContent_Slot_Renders_Badge()
    {
        var cut = _ctx.Render<L.FeatureItem>(p => p
            .Add(f => f.Title, "X")
            .Add(f => f.IconContent, (RenderFragment)(b => b.AddMarkupContent(0, "<i data-test='badge'></i>"))));
        Assert.NotNull(cut.Find("[data-test='badge']"));
    }

    [Fact]
    public void No_Icon_Badge_When_IconContent_Null()
    {
        var cut = _ctx.Render<L.FeatureItem>(p => p.Add(f => f.Title, "X"));
        // The tinted icon badge div uses bg-primary/10 — absent without IconContent.
        Assert.DoesNotContain("bg-primary/10", cut.Markup);
    }

    [Fact]
    public void Lumeo_Icon_Component_Usable_Inside_IconContent()
    {
        // The whole point of #300: the Icon component is no longer shadowed by
        // the slot, so it renders as an <svg>, not as the slot marker.
        var cut = _ctx.Render<L.FeatureItem>(p => p
            .Add(f => f.Title, "X")
            .Add(f => f.IconContent, (RenderFragment)(b =>
            {
                b.OpenComponent<L.Icon>(0);
                b.AddAttribute(1, "Name", "Palette");
                b.CloseComponent();
            })));
        Assert.NotNull(cut.Find("svg"));
    }
}
