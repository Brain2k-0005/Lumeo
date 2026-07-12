using System.Net;
using System.Net.Http;
using Bunit;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.FileViewerComponent;

/// <summary>
/// Wave 4 composition audit — FileViewer renders native &lt;a href&gt;
/// download/open-in-new-tab links (Tab/Enter-accessible for free) plus a
/// native "retry" &lt;button @onclick="RefreshAsync"&gt; on load failure.
/// FileViewerStateOnDataChangeTests already covers RefreshAsync() recovering
/// from an error when called programmatically; this file fills the remaining
/// neededTests gaps: the toolbar download link is reachable/native-anchor
/// (Tab order + href), the Retry button (not just the RefreshAsync method)
/// actually re-triggers the load when ACTIVATED, and the role="document"
/// region carries a real aria-label for the current file.
/// </summary>
public class FileViewerKeyboardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public FileViewerKeyboardTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private sealed class StubHandler : HttpMessageHandler
    {
        private Func<HttpRequestMessage, HttpResponseMessage> _responder = _ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(string.Empty) };
        public int CallCount { get; private set; }
        public void RespondWith(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            CallCount++;
            return Task.FromResult(_responder(request));
        }
    }

    [Fact]
    public void Toolbar_Download_Link_Is_A_Native_Anchor_With_Href_And_No_Tabindex_Override()
    {
        // Image kind needs no text fetch, so the toolbar renders immediately
        // without waiting on an HttpClient.
        var cut = _ctx.Render<L.FileViewer>(p => p
            .Add(c => c.Src, "https://files.example.com/photo.png")
            .Add(c => c.Kind, L.FileKind.Image));

        var link = cut.Find("a[aria-label='Download file']");
        Assert.Equal("https://files.example.com/photo.png", link.GetAttribute("href"));
        Assert.False(link.HasAttribute("tabindex"));
    }

    [Fact]
    public async Task Activating_Retry_Button_Re_Fetches_And_Recovers_From_Error()
    {
        var handler = new StubHandler();
        _ctx.Services.AddSingleton(_ => new HttpClient(handler));
        handler.RespondWith(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var cut = _ctx.Render<L.FileViewer>(p => p
            .Add(c => c.Src, "https://files.example.com/data.csv"));

        cut.WaitForAssertion(() => Assert.Contains("Could not display file", cut.Markup));
        var callsAfterFirstLoad = handler.CallCount;

        handler.RespondWith(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("h\n1\n2\n3")
        });

        // Enter/Space activation of a native <button> is free via the browser's
        // default semantics — .Click() exercises the exact RefreshAsync a
        // synthesized keydown would run.
        await cut.InvokeAsync(() => cut.Find("button").Click());

        Assert.True(handler.CallCount > callsAfterFirstLoad);
        cut.WaitForAssertion(() => Assert.DoesNotContain("Could not display file", cut.Markup));
    }

    [Fact]
    public void Body_Region_Has_Document_Role_With_A_Real_Aria_Label()
    {
        var cut = _ctx.Render<L.FileViewer>(p => p
            .Add(c => c.Src, "https://files.example.com/report.png")
            .Add(c => c.Kind, L.FileKind.Image)
            .Add(c => c.FileName, "report.png"));

        var region = cut.Find("[role='document']");
        Assert.Equal("report.png", region.GetAttribute("aria-label"));
    }
}
