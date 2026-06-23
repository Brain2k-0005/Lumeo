using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.PdfViewer;

public class PdfViewerTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public PdfViewerTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_The_Toolbar_With_Page_Nav_By_Default()
    {
        var cut = _ctx.Render<L.PdfViewer>();
        Assert.Contains("Previous page", cut.Markup); // page-nav button aria-label
    }

    [Fact]
    public void Toolbar_Is_Hidden_When_ShowToolbar_Is_False()
    {
        var cut = _ctx.Render<L.PdfViewer>(p => p.Add(v => v.ShowToolbar, false));
        Assert.DoesNotContain("Previous page", cut.Markup);
    }
}
