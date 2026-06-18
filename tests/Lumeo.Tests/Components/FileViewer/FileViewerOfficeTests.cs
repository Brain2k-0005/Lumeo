using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.FileViewerComponent;

/// <summary>
/// Rendering tests for the Office-document graceful fallback (#321). Office docs
/// can't render inline, so FileViewer must show a clear, format-specific panel
/// with a Download action — and an "Open in viewer" action only when explicitly
/// enabled and the URL is a publicly reachable http(s) URL.
/// </summary>
public class FileViewerOfficeTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public Task InitializeAsync()
    {
        _ctx.AddLumeoServices();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Word_Document_Shows_Format_Specific_Fallback()
    {
        var cut = _ctx.Render<L.FileViewer>(p => p
            .Add(c => c.Src, "https://files.example.com/report.docx"));

        Assert.Contains("Word document", cut.Markup);
        Assert.Contains("can't be previewed inline", cut.Markup);
    }

    [Fact]
    public void Excel_And_PowerPoint_Get_Their_Own_Titles()
    {
        var xlsx = _ctx.Render<L.FileViewer>(p => p.Add(c => c.Src, "https://x/budget.xlsx"));
        Assert.Contains("Excel spreadsheet", xlsx.Markup);

        var pptx = _ctx.Render<L.FileViewer>(p => p.Add(c => c.Src, "https://x/deck.pptx"));
        Assert.Contains("PowerPoint presentation", pptx.Markup);
    }

    [Fact]
    public void Office_Fallback_Has_Download_Link()
    {
        var cut = _ctx.Render<L.FileViewer>(p => p
            .Add(c => c.Src, "https://files.example.com/report.docx"));

        var download = cut.FindAll("a").FirstOrDefault(a => a.GetAttribute("download") is not null);
        Assert.NotNull(download);
        Assert.Equal("https://files.example.com/report.docx", download!.GetAttribute("href"));
    }

    [Fact]
    public void Online_Viewer_Link_Hidden_By_Default()
    {
        var cut = _ctx.Render<L.FileViewer>(p => p
            .Add(c => c.Src, "https://files.example.com/report.docx"));

        Assert.DoesNotContain("view.officeapps.live.com", cut.Markup);
    }

    [Fact]
    public void Online_Viewer_Link_Shown_When_Enabled_For_Http_Url()
    {
        var cut = _ctx.Render<L.FileViewer>(p => p
            .Add(c => c.Src, "https://files.example.com/report.docx")
            .Add(c => c.EnableOfficeOnlineViewer, true));

        var viewer = cut.FindAll("a")
            .FirstOrDefault(a => a.GetAttribute("href")?.Contains("view.officeapps.live.com") == true);
        Assert.NotNull(viewer);
        // The document URL must be URL-encoded into the src query param.
        Assert.Contains("src=https%3A%2F%2Ffiles.example.com%2Freport.docx", viewer!.GetAttribute("href"));
    }

    [Fact]
    public void Online_Viewer_Link_Suppressed_For_Non_Http_Url()
    {
        // blob: URLs aren't reachable by Microsoft's server-side viewer, so even
        // with the viewer enabled the link must not render.
        var cut = _ctx.Render<L.FileViewer>(p => p
            .Add(c => c.Src, "blob:https://app.local/9f8c-uuid")
            .Add(c => c.Kind, L.FileKind.Office)
            .Add(c => c.EnableOfficeOnlineViewer, true));

        Assert.DoesNotContain("view.officeapps.live.com", cut.Markup);
    }
}
