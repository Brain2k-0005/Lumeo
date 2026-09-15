using System.Net;
using System.Text;
using Bunit;
using Lumeo.Docs.Pages;
using Lumeo.Docs.Pages.Patterns;
using Microsoft.JSInterop;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Lumeo.Docs.Tests;

// The home page was rewritten to a shadcn-scale landing: announcement pill, headline,
// two buttons, one full-width live dashboard-01 example (shared with /blocks/dashboard
// via Shared/Blocks/Dashboard01.razor), and nothing else. Guards the new structure and
// that the old, deleted sections don't silently reappear.
public class HomePageTests
{
    private sealed class EmptyRegistryHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"components\":{}}", Encoding.UTF8, "application/json")
            });
    }

    // await using (not a shared IDisposable field): ResponsiveService — pulled in by
    // Dashboard01's SidebarProvider — only implements IAsyncDisposable, and a sync
    // ctx.Dispose() throws trying to dispose it. Mirrors AllComponentPagesRenderTests.
    private static BunitContext NewContext()
    {
        var ctx = new BunitContext();

        // Loose mode: the dashboard's AreaChart imports the ECharts JS module on
        // OnAfterRenderAsync (real _content/Lumeo.Charts/js/echarts-interop.js import)
        // that bUnit has no browser to satisfy — same as AllComponentPagesRenderTests.
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        // IdleMount's own JS.InvokeVoidAsync("lumeo.onIdle", ...) call is explicitly
        // configured to throw (Loose mode alone would silently no-op it and never
        // deliver the JS -> .NET OnIdle() callback that swaps the placeholder out).
        // IdleMount's catch-all treats any failure as "JS unavailable" and mounts
        // ChildContent immediately — the same fallback it uses for SSR/bUnit in prod.
        ctx.JSInterop.SetupVoid("lumeo.onIdle", _ => true).SetException(new JSException("bUnit: no idle callback"));

        ctx.Services.AddLumeo();
        ctx.Services.AddSingleton<Lumeo.Docs.Services.IconService>();
        ctx.Services.AddSingleton<Lumeo.Docs.Services.DynamicIconResolver>();
        ctx.Services.AddSingleton<Lumeo.Docs.Services.PatternFilterService>();
        ctx.Services.AddSingleton<Lumeo.Docs.Services.NavConfigService>();
        ctx.Services.AddSingleton(new HttpClient(new EmptyRegistryHandler()) { BaseAddress = new Uri("https://test/") });
        ctx.Services.AddSingleton<Lumeo.Docs.Services.RegistryService>();
        return ctx;
    }

    [Fact]
    public async Task Renders_the_headline_and_both_buttons_with_their_hrefs()
    {
        await using var ctx = NewContext();
        var cut = ctx.Render<Home>();

        Assert.Contains("Own your Blazor UI.", cut.Markup);

        var getStarted = cut.Find("a[href='docs/introduction']");
        Assert.Contains("Get Started", getStarted.TextContent);

        var viewComponents = cut.Find("a[href='components']");
        Assert.Contains("View Components", viewComponents.TextContent);
    }

    [Fact]
    public async Task Renders_the_dashboard_01_live_example()
    {
        await using var ctx = NewContext();
        var cut = ctx.Render<Home>();

        // IdleMount falls back to an immediate mount (see NewContext comment), so the
        // real Dashboard01 markup — not the Skeleton placeholder — should be present.
        Assert.Contains("Acme Inc.", cut.Markup);
    }

    [Fact]
    public async Task Does_not_render_the_removed_sections()
    {
        await using var ctx = NewContext();
        var cut = ctx.Render<Home>();

        Assert.DoesNotContain("A fraction of the size.", cut.Markup);
        Assert.DoesNotContain("Everything included.", cut.Markup);
        Assert.DoesNotContain("Built for real surfaces.", cut.Markup);
        Assert.DoesNotContain("Real apps, built entirely with Lumeo.", cut.Markup);
    }

    [Fact]
    public async Task Blocks_dashboard_page_still_renders_the_block()
    {
        await using var ctx = NewContext();
        var cut = ctx.Render<DashboardPattern>();

        Assert.Contains("Acme Inc.", cut.Markup);
    }
}
