using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Smokes;

/// <summary>
/// The shared docs demo/preview container (<c>Shared/ComponentDemo.razor</c>,
/// used by every <c>/components/*</c> page) used to combine an unbounded
/// <c>min-h-[160px]</c> box with <c>overflow-y-auto</c>. That combination is
/// harmless for genuinely tall VISIBLE content (the box has no max-height, so
/// it simply grows) — but on chart pages it clipped content that was never
/// visible in the first place: <c>Chart.razor</c>'s screen-reader-only data
/// table is an absolutely-positioned <c>&lt;table&gt;</c>, and the CSS table
/// sizing algorithm treats its <c>sr-only</c> <c>width/height: 1px</c> as a
/// MINIMUM rather than an exact size — so the invisible table still occupies
/// real layout space below the chart, inflating the container's scrollable
/// area (scrollHeight) far past its visible content (clientHeight) and
/// popping an unwanted inner scrollbar on a plain component demo. Fixed by
/// dropping <c>overflow-y-auto</c> to <c>overflow-y-hidden</c> — real in-flow
/// (visible) content still grows the box exactly as before; only the
/// off-screen, already-invisible overflow is now silently clipped instead of
/// spawning a scrollbar. <c>overflow-x-auto</c> is untouched (legitimate for
/// genuinely wide content like data tables).
///
/// Requires the docs dev-server. See project README.md.
/// </summary>
public class ComponentDemoOverflowTests : PlaywrightTestBase
{
    private const string PreviewContainerScript =
        "() => { const sec = document.querySelectorAll('section[data-toc-entry]')[0]; " +
        "const c = sec.querySelector('div[class*=\"bg-muted/20\"][class*=\"overflow\"]'); " +
        "const cs = getComputedStyle(c); " +
        "return { overflowX: cs.overflowX, overflowY: cs.overflowY }; }";

    // Real geometric containment check (NOT a class-string match): walks every
    // in-flow (non sr-only, non absolutely-positioned) descendant of the preview
    // container and returns how far the furthest VISIBLE content extends past
    // the container's own bottom edge. A positive value means real, on-screen
    // content is being clipped — the actual visual defect a class-string
    // assertion can't see.
    private const string VisibleOverflowScript =
        "() => { const sec = document.querySelectorAll('section[data-toc-entry]')[0]; " +
        "const c = sec.querySelector('div[class*=\"bg-muted/20\"][class*=\"overflow\"]'); " +
        "const r = c.getBoundingClientRect(); " +
        "let maxBottom = 0; " +
        "const walk = (el) => { for (const child of el.children) { " +
        "  if (child.closest('.sr-only')) continue; " +
        "  const ccs = getComputedStyle(child); " +
        "  if (ccs.visibility !== 'hidden' && ccs.display !== 'none' && ccs.position !== 'absolute' && ccs.position !== 'fixed') { " +
        "    const cr = child.getBoundingClientRect(); " +
        "    if (cr.height > 0 && cr.bottom > maxBottom) maxBottom = cr.bottom; " +
        "  } " +
        "  walk(child); " +
        "} }; " +
        "walk(c); " +
        "return maxBottom - r.bottom; }";

    [Fact]
    public async Task Bar_chart_demo_container_does_not_force_a_vertical_scrollbar()
    {
        await Goto("/components/charts/bar");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Wait for the first demo's preview container to actually have size —
        // the WASM boot must be complete before ECharts has mounted a canvas.
        await Page.WaitForFunctionAsync(
            "() => { const els = document.querySelectorAll('section .bg-muted\\\\/20'); " +
            "for (const el of els) { const r = el.getBoundingClientRect(); if (r.width > 0 && r.height > 0) return true; } " +
            "return false; }",
            null, new() { Timeout = 20000 });

        // Give the chart's screen-reader table (built after the real ECharts
        // option resolves) a moment to actually be in the DOM.
        await Page.WaitForSelectorAsync("section table.sr-only", new() { State = WaitForSelectorState.Attached, Timeout = 10000 });
        await Page.WaitForTimeoutAsync(300);

        var overflow = await Page.EvaluateAsync<OverflowResult>(PreviewContainerScript);

        // Predicted (this is the FIX under test): vertical auto-scroll is gone —
        // computed overflow-y must be "hidden", never "auto". Horizontal
        // auto-scroll stays, for genuinely wide content elsewhere on the site.
        // A disable-check (reverting ComponentDemo.razor's overflow-y-hidden
        // back to overflow-y-auto) reproduces the ORIGINAL bug: overflowY comes
        // back as "auto" here, and the assertion below fails.
        Assert.Equal("hidden", overflow.OverflowY);
        Assert.Equal("auto", overflow.OverflowX);

        // Real geometry: no VISIBLE content may be clipped by the box. Before
        // the fix, the invisible sr-only table inflated scrollHeight (measured
        // live: 717px vs a 414px box at a 1280x900 viewport) — but no actually
        // visible content was ever cut, so this must hold both before AND
        // after the overflow-y fix; it's here to prove the fix didn't newly
        // clip something real while removing the phantom scrollbar.
        var visibleOverflow = await Page.EvaluateAsync<double>(VisibleOverflowScript);
        Assert.True(visibleOverflow <= 1,
            $"Expected no visible content clipped by the demo box, but content extends {visibleOverflow:F1}px past its bottom edge.");
    }

    private sealed class OverflowResult
    {
        public string OverflowX { get; set; } = "";
        public string OverflowY { get; set; } = "";
    }
}
