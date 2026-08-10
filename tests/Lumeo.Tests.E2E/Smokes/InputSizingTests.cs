using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Smokes;

/// <summary>
/// Codex review of PR #386 (shadcn-wave0), findings 3 + 4 on <c>Input.razor</c> — neither
/// is observable from bUnit, which never applies real CSS, so both need a genuine browser
/// computing real font-size / line-height / box geometry at real viewport widths.
///
/// Drives <c>tests/Lumeo.Tests.ServerHost</c>'s <c>/e2e/input-sizing</c> page (a Blazor
/// SERVER host, NOT the docs WASM site) the same way the Gantt parity suite does — see
/// <c>Gantt/GanttParityTestBase.cs</c>'s remarks for why a separate base URL is needed.
/// This class deliberately does NOT inherit that base (its <c>[Collection]</c> serializes
/// against the shared Gantt circuit, which this suite has no reason to share) but resolves
/// the SAME <c>LUMEO_GANTT_E2E_BASE_URL</c> env var / port-5299 default, since that is
/// already how the CI workflow (.github/workflows/e2e.yml) starts and exposes the
/// ServerHost process — introducing a second env var would need a workflow change this
/// task's scope doesn't call for.
///
/// Finding 3 (line-height overflow): <c>text-base</c>'s bundled line-height is 24px
/// (<c>--text-base--line-height: calc(1.5/1)</c>). Measured against the actual rendered
/// geometry (py-1 = 8px total padding, border = 2px total, fixed across every size/density),
/// three combos had LESS than 24px of content-box height available: Sm/Compact (h-7=28px,
/// 18px avail), Sm/Comfortable (h-8=32px, 22px avail), Default/Compact (h-8=32px, 22px
/// avail). The fix (<c>max-md:leading-none</c>, verified at 16px/1.0 line-height) is applied
/// uniformly across all Sm/Default combos; this suite asserts NONE of the 9 rendered
/// Size x Density combinations exceeds its own control's content-box height, not just the
/// three that were measured to overflow.
///
/// Finding 4 (Class override defeated at >=768px): Cx.Merge/tailwind-merge correctly keys
/// conflict groups by variant chain, so a caller's unprefixed <c>Class="text-lg"</c> only
/// replaced Input's OWN unprefixed <c>text-base</c> (same group) — the separate
/// <c>md:text-sm</c> token (a different group) survived untouched and silently won back at
/// desktop widths. The fix suppresses Input's own responsive pair entirely whenever an
/// unprefixed font-size override is detected in <c>Class</c>. This suite asserts the
/// COMPUTED font-size (not the class string) for both a default (no-override) input and two
/// override inputs, at both a mobile and a desktop viewport width.
/// </summary>
public class InputSizingTests : IAsyncLifetime
{
    private static string HostBaseUrl { get; } =
        Environment.GetEnvironmentVariable("LUMEO_GANTT_E2E_BASE_URL")
        ?? "http://localhost:5299";

    // Below/above Tailwind's `md` breakpoint (768px) — the exact boundary the
    // iOS-zoom-guard's md:text-sm/md:text-xs pair and the Finding-4 fix both key off.
    private const int MobileWidth = 390;
    private const int DesktopWidth = 1280;

    private IPlaywright _playwright = default!;
    private IBrowser _browser = default!;
    private IPage _page = default!;

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        _page = await _browser.NewPageAsync();
        await _page.GotoAsync($"{HostBaseUrl}/e2e/input-sizing");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public async Task DisposeAsync()
    {
        await _page.CloseAsync();
        await _browser.CloseAsync();
        _playwright.Dispose();
    }

    private async Task<string> ComputedFontSizeAsync(string testId, int viewportWidth)
    {
        await _page.SetViewportSizeAsync(viewportWidth, 900);
        return await _page.EvaluateAsync<string>(
            @"(id) => { const el = document.querySelector(`[data-testid=""${id}""]`); return getComputedStyle(el).fontSize; }",
            testId);
    }

    /// <summary>
    /// Finding 4a: an Input with NO Class override must land on the 16px iOS-zoom-guard
    /// floor below the md breakpoint, and shrink back to the size-appropriate desktop value
    /// at/above it — the behaviour the responsive pair was introduced for in the first place.
    /// </summary>
    [Fact]
    public async Task Default_input_is_16px_on_mobile_and_shrinks_back_on_desktop()
    {
        var mobile = await ComputedFontSizeAsync("default-input", MobileWidth);
        var desktop = await ComputedFontSizeAsync("default-input", DesktopWidth);

        Assert.Equal("16px", mobile);
        Assert.Equal("14px", desktop); // Default/Comfortable's md:text-sm (0.875rem)
    }

    /// <summary>
    /// Finding 4b: an EXPLICIT unprefixed font-size override in Class must win at every
    /// breakpoint — the whole point of the fix. Asserts the COMPUTED value, not the class
    /// string: the bug this closes is exactly a case where the class string looked correct
    /// (both text-lg AND md:text-sm present) while the rendered value was wrong at desktop.
    /// </summary>
    [Theory]
    [InlineData("override-lg-input", "18px")] // Class="text-lg" -> 1.125rem
    [InlineData("override-xs-input", "12px")] // Class="text-xs" -> 0.75rem
    public async Task Class_font_size_override_wins_at_every_breakpoint(string testId, string expectedPx)
    {
        var mobile = await ComputedFontSizeAsync(testId, MobileWidth);
        var desktop = await ComputedFontSizeAsync(testId, DesktopWidth);

        Assert.Equal(expectedPx, mobile);
        Assert.Equal(expectedPx, desktop);
    }

    /// <summary>
    /// Finding 3: no rendered Input, at any Size x Density combination, may have a line box
    /// taller than the content area actually available inside its control (client height
    /// minus padding minus border). Checked at the mobile width, where every Sm/Default combo
    /// carries the 16px text-base baseline that caused the original overflow.
    /// </summary>
    // Plain strings (not Lumeo.Size/Density) deliberately — this test project carries no
    // reference to the Lumeo library (Playwright drives it purely over HTTP), and the values
    // only need to match the data-testid="size-{size}-density-{density}" the ServerHost page
    // renders (InputSizingPage.razor), which formats the SAME enums via their ToString().
    [Theory]
    [InlineData("Sm", "Compact")]
    [InlineData("Sm", "Comfortable")]
    [InlineData("Sm", "Spacious")]
    [InlineData("Md", "Compact")]
    [InlineData("Md", "Comfortable")]
    [InlineData("Md", "Spacious")]
    [InlineData("Lg", "Compact")]
    [InlineData("Lg", "Comfortable")]
    [InlineData("Lg", "Spacious")]
    public async Task No_line_box_exceeds_its_control_height(string size, string density)
    {
        await _page.SetViewportSizeAsync(MobileWidth, 900);
        var testId = $"size-{size}-density-{density}";

        // el.clientHeight is the border-box height MINUS border (it already excludes border,
        // per the DOM spec) but still INCLUDES padding — so the content area available for the
        // line box is clientHeight minus padding only. Border must NOT be subtracted a second
        // time here (an earlier draft of this test double-subtracted it, silently making the
        // assertion 2px stricter than the real available space).
        var geometry = await _page.EvaluateAsync<BoxGeometry>(
            @"(id) => {
                const el = document.querySelector(`[data-testid=""${id}""]`);
                const cs = getComputedStyle(el);
                return {
                    LineHeight: parseFloat(cs.lineHeight),
                    ClientHeight: el.clientHeight,
                    PaddingTop: parseFloat(cs.paddingTop),
                    PaddingBottom: parseFloat(cs.paddingBottom),
                };
            }",
            testId);

        var available = geometry.ClientHeight - geometry.PaddingTop - geometry.PaddingBottom;

        Assert.True(geometry.LineHeight <= available,
            $"{testId}: line-height {geometry.LineHeight}px exceeds the {available}px available inside a {geometry.ClientHeight}px (client height) control " +
            $"(padding {geometry.PaddingTop + geometry.PaddingBottom}px).");
    }

    private sealed class BoxGeometry
    {
        public double LineHeight { get; set; }
        public double ClientHeight { get; set; }
        public double PaddingTop { get; set; }
        public double PaddingBottom { get; set; }
    }
}
