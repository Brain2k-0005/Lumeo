using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.MegaMenu;

/// <summary>
/// #577 — a full-width panel inside a shrink-wrapped nav was squeezed to the nav width,
/// so 1fr grid columns became narrower than their link text and the hover highlight
/// (the link's own box) was shorter than its content. The grid columns now carry a
/// minimum track size and the panel a min-content floor, and the link's text column
/// can shrink/wrap inside the highlighted box.
/// </summary>
public class MegaMenuHoverWidthTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public MegaMenuHoverWidthTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void Grid_columns_have_a_minimum_track_size(int columns)
    {
        var cut = _ctx.Render<L.MegaMenuPanel>(p => p.Add(c => c.Columns, columns));
        Assert.Contains($"grid-cols-[repeat({columns},minmax(10rem,1fr))]", cut.Find("[data-slot=mega-menu-panel] > div").ClassName!);
    }

    [Fact]
    public void Full_width_panel_keeps_a_min_content_floor()
    {
        var cut = _ctx.Render<L.MegaMenuPanel>();
        Assert.Contains("min-w-min", cut.Find("[data-slot=mega-menu-panel]").ClassName!);
    }

    [Fact]
    public void Link_text_column_can_shrink_inside_the_highlight()
    {
        var cut = _ctx.Render<L.MegaMenuLink>(p => p.Add(c => c.Title, "T").Add(c => c.Description, "D"));
        Assert.Contains("min-w-0", cut.Find("a > div").ClassName!);
    }
}
