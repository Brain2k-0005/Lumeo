using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Flow;

/// <summary>
/// Real-browser coverage for <c>/components/flow-canvas</c> (the docs WASM host, via
/// <see cref="PlaywrightTestBase"/> — LUMEO_E2E_BASE_URL, not the ServerHost the rest of the Flow
/// suite drives). Deliberately separate from <see cref="FlowCanvasTests"/>: RegistryGen's
/// <c>testCoverage.e2e</c> only credits a spec that navigates to a component's own docs route
/// (<c>/components/&lt;kebab&gt;</c>), and the rest of the suite drives <c>/e2e/flow</c> instead.
///
/// Asserts per-section presence rather than just a 200 response, same discipline as
/// SchedulerDocsPageRenderTests: a Blazor render exception drops everything after it silently.
/// </summary>
public class FlowCanvasDocsPageTests : PlaywrightTestBase
{
    private const float LongTimeoutMs = 30000;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        // The page has 15 ComponentDemo cards (5 from phase 1/2, auto-layout/anchored-fit
        // (LU-19)/undo-redo/reconnect from phase 3a, resize+helper-lines/clipboard/loose-connections/export
        // from phase 4, sub-flows and the
        // 1,500-node virtualized canvas from phase 5),
        // each wrapping its FlowCanvas in LazyRender
        // (docs/Lumeo.Docs/Shared/LazyRender.razor — IntersectionObserver, 200px root margin).
        // A normal-height viewport would leave the later demos unmounted until scrolled; a tall
        // viewport brings the whole page within the observer's bounds on first paint instead.
        await Page.SetViewportSizeAsync(1280, 11000);
    }

    [Fact]
    public async Task Page_Header_And_First_Demo_Canvas_Render()
    {
        await Goto("/components/flow-canvas");

        await Assertions.Expect(Page.Locator("h1", new() { HasTextString = "Flow Canvas" })).ToBeVisibleAsync(new() { Timeout = LongTimeoutMs });
        var canvas = Page.Locator("[data-slot='flow-canvas']").First;
        await Assertions.Expect(canvas).ToBeVisibleAsync(new() { Timeout = LongTimeoutMs });
        await Assertions.Expect(canvas).ToHaveAttributeAsync("data-flow-ready", "done", new() { Timeout = LongTimeoutMs });
    }

    [Fact]
    public async Task The_Connect_Selection_And_Overlay_Demos_All_Render_A_Canvas()
    {
        await Goto("/components/flow-canvas");
        await Assertions.Expect(Page.Locator("h1", new() { HasTextString = "Flow Canvas" })).ToBeVisibleAsync(new() { Timeout = LongTimeoutMs });

        // Fifteen ComponentDemo cards on the page each hold one FlowCanvas, plus the minimap overlay
        // and virtualization demos also render FlowMiniMap.
        await Assertions.Expect(Page.Locator("[data-slot='flow-canvas']")).ToHaveCountAsync(15, new() { Timeout = LongTimeoutMs });
        await Assertions.Expect(Page.Locator("[data-slot='flow-minimap']").First).ToBeVisibleAsync(new() { Timeout = LongTimeoutMs });
    }
}
