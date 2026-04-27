using System.Net;
using System.Text;
using Bunit;
using Lumeo.Docs.Layout;
using Lumeo.Docs.Services;
using Lumeo.Docs.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Lumeo.Docs.Tests.Layout;

public class NavMenuTests : IDisposable
{
    private readonly BunitContext _ctx = new();

    public NavMenuTests()
    {
        _ctx.Services.AddSingleton(new HttpClient(new InMemoryNavConfigHandler())
        {
            BaseAddress = new Uri("https://test/")
        });
        _ctx.AddDocsServices();
        // NavMenu calls lumeoNavScrollActiveIntoView in OnAfterRenderAsync
        _ctx.JSInterop.SetupVoid("lumeoNavScrollActiveIntoView", _ => true);
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public void Renders_groups_from_nav_config()
    {
        var cut = _ctx.Render<NavMenu>();
        cut.WaitForAssertion(() => Assert.Contains("Form", cut.Markup));
        Assert.Contains("Inputs", cut.Markup);
    }

    [Fact]
    public void Form_group_lists_five_subgroups()
    {
        var cut = _ctx.Render<NavMenu>();
        cut.WaitForAssertion(() =>
        {
            foreach (var sub in new[] { "Inputs", "Selection", "Buttons &amp; Actions", "Form Composition", "Specialized" })
            {
                Assert.Contains(sub, cut.Markup);
            }
        });
    }

    private sealed class InMemoryNavConfigHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            // Read the actual nav-config.json from the docs project so the test exercises real config.
            var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                "docs", "Lumeo.Docs", "wwwroot", "Layout", "nav-config.json");
            var json = File.ReadAllText(Path.GetFullPath(path));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }
}
