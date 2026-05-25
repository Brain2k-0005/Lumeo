using System.Collections.Generic;
using Bunit;
using Lumeo.Docs.Shared;
using Lumeo.Docs.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace Lumeo.Docs.Tests.Catalog;

public class CatalogFilterChipsTests : IDisposable
{
    private readonly BunitContext _ctx = new();

    public CatalogFilterChipsTests() => _ctx.AddDocsServices();
    public void Dispose() => _ctx.Dispose();

    [Fact]
    public void Renders_All_chip_plus_one_per_category()
    {
        var cats = new[] { "Forms", "Layout", "Data Display" };
        var cut = _ctx.Render<CatalogFilterChips>(p => p
            .Add(c => c.Categories, cats));

        Assert.Contains("All", cut.Markup);
        Assert.Contains("Forms", cut.Markup);
        Assert.Contains("Layout", cut.Markup);
        Assert.Contains("Data Display", cut.Markup);
    }

    [Fact]
    public void Clicking_chip_invokes_callback_with_category()
    {
        var cats = new[] { "Forms", "Layout" };
        IReadOnlyCollection<string> latest = Array.Empty<string>();
        var cb = EventCallback.Factory.Create<IReadOnlyCollection<string>>(
            this, value => latest = value);

        var cut = _ctx.Render<CatalogFilterChips>(p => p
            .Add(c => c.Categories, cats)
            .Add(c => c.SelectedChanged, cb));

        cut.Find("[data-chip='Forms']").Click();
        Assert.Contains("Forms", latest);
    }

    [Fact]
    public void Initial_selected_set_marks_chips_active()
    {
        var cats = new[] { "Forms", "Layout", "Data Display" };
        IReadOnlyCollection<string> selected = new[] { "Forms", "Layout" };

        var cut = _ctx.Render<CatalogFilterChips>(p => p
            .Add(c => c.Categories, cats)
            .Add(c => c.Selected, selected));

        Assert.Equal("true",  cut.Find("[data-chip='Forms']").GetAttribute("aria-pressed"));
        Assert.Equal("true",  cut.Find("[data-chip='Layout']").GetAttribute("aria-pressed"));
        Assert.Equal("false", cut.Find("[data-chip='Data Display']").GetAttribute("aria-pressed"));
        Assert.Equal("false", cut.Find("[data-chip='All']").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Clicking_All_chip_clears_selection()
    {
        var cats = new[] { "Forms", "Layout" };
        IReadOnlyCollection<string> latest = new[] { "Forms" };
        var cb = EventCallback.Factory.Create<IReadOnlyCollection<string>>(
            this, value => latest = value);

        var cut = _ctx.Render<CatalogFilterChips>(p => p
            .Add(c => c.Categories, cats)
            .Add(c => c.Selected, (IReadOnlyCollection<string>)new[] { "Forms" })
            .Add(c => c.SelectedChanged, cb));

        cut.Find("[data-chip='All']").Click();
        Assert.Empty(latest);
    }
}
