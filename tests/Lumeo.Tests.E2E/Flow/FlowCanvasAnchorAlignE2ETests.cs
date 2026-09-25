using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Flow;

/// <summary>
/// LU-19 (SQL Analyst field report against 5.11.1): <c>FlowFitViewOptions.AnchorAlign</c> controls
/// where a clamped anchor lands. bUnit (<see cref="Lumeo.Tests.Components.Flow.FlowCanvasSqlAnalystRound2FindingsTests"/>)
/// covers the math end to end through <c>FlowCanvas.FitViewAsync</c>, but only flow.js applies the
/// resulting viewport as a real screen transform — this drives the docs "Anchored fit" demo
/// (<c>/components/flow-canvas</c>, a 36-node left-to-right tree) in a real browser and measures
/// where the root node's left edge actually lands. Requires the docs dev-server — see project
/// README.md.
/// </summary>
public class FlowCanvasAnchorAlignE2ETests : PlaywrightTestBase
{
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        // Same reasoning as FlowCanvasDocsPageTests: a tall viewport keeps every LazyRender demo
        // (including this one, further down the page) within the IntersectionObserver's bounds on
        // first paint instead of leaving it unmounted until scrolled.
        await Page.SetViewportSizeAsync(1280, 11000);
    }

    [Fact]
    public async Task AnchorAlign_Start_Puts_The_Roots_Left_Edge_At_The_Fit_Padding()
    {
        await Goto("/components/flow-canvas");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var demo = Page.Locator("[data-testid='anchor-fit-demo']");
        await demo.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 20000 });

        // The engine has registered this canvas (flow.js sets data-flow-ready="done" once
        // registered, immediately for a FitViewOnInit=false canvas).
        await Page.Locator("[data-testid='anchor-fit-demo'] [data-flow-ready='done']")
            .WaitForAsync(new() { State = WaitForSelectorState.Attached, Timeout = 20000 });

        var pane = Page.Locator("[data-testid='anchor-fit-demo'] [data-slot='flow-pane']");
        var paneBox = await pane.BoundingBoxAsync();
        Assert.NotNull(paneBox);
        // FlowCanvas.FitViewPadding defaults to 0.1 — AnchorAlignedViewport reads it as a fraction
        // of the pane's own width for the horizontal placement (see FlowGeometry.cs).
        var expectedPaddingPx = paneBox!.Width * 0.1;

        await Page.Locator("[data-testid='anchor-fit-start']").ClickAsync();

        // Poll for the root node's screen-space left edge to settle at the expected padding —
        // the fit is a single Blazor round trip + JS interop call, not an animation, but give it
        // a little room rather than asserting on the very next frame.
        await Page.WaitForFunctionAsync(
            @"([paneSel, nodeSel, expected]) => {
                const paneEl = document.querySelector(paneSel);
                const nodeEl = document.querySelector(nodeSel);
                if (!paneEl || !nodeEl) return false;
                const paneBox = paneEl.getBoundingClientRect();
                const nodeBox = nodeEl.getBoundingClientRect();
                return Math.abs((nodeBox.left - paneBox.left) - expected) < 2;
            }",
            new object[]
            {
                "[data-testid='anchor-fit-demo'] [data-slot='flow-pane']",
                "[data-testid='anchor-fit-demo'] [data-flow-node='root']",
                expectedPaddingPx,
            },
            new() { Timeout = 5000 });

        var rootBox = await Page.Locator("[data-testid='anchor-fit-demo'] [data-flow-node='root']").BoundingBoxAsync();
        Assert.NotNull(rootBox);
        var actualOffset = rootBox!.X - paneBox.X;
        Assert.True(Math.Abs(actualOffset - expectedPaddingPx) < 2,
            $"root's left edge should be within 2px of the fit padding: offset={actualOffset}, expected={expectedPaddingPx}");
    }
}
