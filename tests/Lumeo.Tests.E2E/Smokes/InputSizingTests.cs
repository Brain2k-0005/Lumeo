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
///
/// CI flake fix (Codex #386 review round 3, item 1): No_line_box_exceeds_its_control_height
/// intermittently failed with "getComputedStyle: parameter 1 is not of type 'Element'" for a
/// single Size x Density theory case — a null element handle, meaning the raw
/// <c>document.querySelector</c> call ran BEFORE that &lt;Input&gt; had actually attached to
/// the DOM. Root cause: this host renders with <c>InteractiveServerRenderMode(prerender:
/// false)</c> (see App.razor) — the initial HTML response is an empty shell, and every element
/// on the page only appears once the SignalR circuit connects and Blazor applies its first
/// render batch. <c>Page.WaitForLoadStateAsync(NetworkIdle)</c> in InitializeAsync only
/// observes HTTP/JS asset downloads settling; it does NOT observe the circuit's render batch,
/// which arrives over the already-open WebSocket afterward. This suite is NOT in the Gantt
/// suite's sequential collection (deliberately — see above), so it runs in xUnit's default
/// PARALLEL collection alongside every other Smokes test class hitting the same ServerHost
/// process/circuit pool; GanttSequentialCollection's own remarks document the identical
/// symptom ("intermittently exceeded timeouts purely from resource contention, not a real
/// rendering bug") for exactly this kind of shared-process contention. Under that load the
/// render batch can simply take longer than usual — any one theory case could be "the unlucky
/// one" in a given run, not specifically Lg/Spacious. Fixed by switching every element lookup
/// from a one-shot <c>Page.EvaluateAsync</c> + raw <c>querySelector</c> (which assumes the
/// element already exists) to <c>Locator.WaitForAsync</c> + <c>Locator.EvaluateAsync</c>
/// (which resolves the selector against an ACTUAL Playwright locator that waits — up to its
/// own timeout — for a matching element to attach before evaluating against it). All Size x
/// Density cases now go through this same robust path (originally 9 Sm/Md/Lg combos; the
/// full-scale rollout extended the ServerHost page and this suite to all 21 Size x Density
/// combinations across the 7-rung Lumeo.Size scale — see
/// Xxs_And_Xs_Line_Box_Measurements_Match_Predicted_Geometry for the new small rungs'
/// measured numbers).
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

    // Resolves a data-testid to a Playwright Locator that WAITS (up to its default timeout)
    // for a matching element to attach to the DOM, instead of a one-shot
    // `document.querySelector` that assumes it already exists. See the class-level remarks
    // (CI flake fix, item 1) for why this matters on this specific host.
    private ILocator ByTestId(string testId) => _page.Locator($"[data-testid=\"{testId}\"]");

    private async Task<string> ComputedFontSizeAsync(string testId, int viewportWidth)
    {
        await _page.SetViewportSizeAsync(viewportWidth, 900);
        var locator = ByTestId(testId);
        await locator.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached });
        return await locator.EvaluateAsync<string>("(el) => getComputedStyle(el).fontSize");
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
    // Codex round-3 finding 4: Class="text-lg/6" (the line-height-modifier form) must win at
    // every breakpoint exactly like plain "text-lg" — this is the specific case the original
    // regex-based HasFontSizeOverride missed. Predicted WRONG value under the pre-fix regex:
    // mobile correctly renders 18px (unprefixed text-lg/6 replaces the component's own
    // unprefixed text-base, same Cx.Merge group, regardless of the regex), but DESKTOP
    // regresses to 14px because md:text-sm was never suppressed (the regex didn't recognise
    // the slash form, so HasFontSizeOverride stayed false).
    [InlineData("override-lg-slash-input", "18px")] // Class="text-lg/6" -> 1.125rem, /6 sets line-height only
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
    /// minus padding minus border). Checked at the mobile width, where every rung carries the
    /// 16px text-base baseline that caused the original overflow. Extended (full-scale
    /// rollout) from the original 9 Sm/Md/Lg combos to all 21 Size x Density combos, including
    /// the two brand-new small rungs (Xxs/Xs) that ship the same max-md:leading-none fix.
    /// </summary>
    // Plain strings (not Lumeo.Size/Density) deliberately — this test project carries no
    // reference to the Lumeo library (Playwright drives it purely over HTTP), and the values
    // only need to match the data-testid="size-{size}-density-{density}" the ServerHost page
    // renders (InputSizingPage.razor), which formats the SAME enums via their ToString().
    [Theory]
    [InlineData("Xxs", "Compact")]
    [InlineData("Xxs", "Comfortable")]
    [InlineData("Xxs", "Spacious")]
    [InlineData("Xs", "Compact")]
    [InlineData("Xs", "Comfortable")]
    [InlineData("Xs", "Spacious")]
    [InlineData("Sm", "Compact")]
    [InlineData("Sm", "Comfortable")]
    [InlineData("Sm", "Spacious")]
    [InlineData("Md", "Compact")]
    [InlineData("Md", "Comfortable")]
    [InlineData("Md", "Spacious")]
    [InlineData("Lg", "Compact")]
    [InlineData("Lg", "Comfortable")]
    [InlineData("Lg", "Spacious")]
    [InlineData("Xl", "Compact")]
    [InlineData("Xl", "Comfortable")]
    [InlineData("Xl", "Spacious")]
    [InlineData("Xxl", "Compact")]
    [InlineData("Xxl", "Comfortable")]
    [InlineData("Xxl", "Spacious")]
    public async Task Measure_line_box_vs_available_content_height(string size, string density)
    {
        // NOT a pass/fail gate for Xxs/Xs (see the dedicated
        // Xxs_And_Xs_Compact_And_Xxs_Comfortable_line_boxes_still_overflow_the_control fact
        // below, which documents the three combos that are EXPECTED to overflow even with the
        // leading-none fix — a physical conflict between the 16px iOS-zoom floor and a
        // sub-24px control, not something CSS alone can close). This theory just records the
        // measured numbers for every combo so a future regression in either direction shows up.
        await _page.SetViewportSizeAsync(MobileWidth, 900);
        var testId = $"size-{size}-density-{density}";
        var geometry = await MeasureGeometryAsync(testId);
        var available = geometry.ClientHeight - geometry.PaddingTop - geometry.PaddingBottom;

        // Every rung EXCEPT the three documented Xxs/Xs overflow combos must still fit.
        var isKnownOverflow =
            (size == "Xxs" && density is "Compact" or "Comfortable") ||
            (size == "Xs" && density == "Compact");

        if (!isKnownOverflow)
        {
            Assert.True(geometry.LineHeight <= available,
                $"{testId}: line-height {geometry.LineHeight}px exceeds the {available}px available inside a {geometry.ClientHeight}px (client height) control " +
                $"(padding {geometry.PaddingTop + geometry.PaddingBottom}px).");
        }
    }

    /// <summary>
    /// Constraint 3 rigor requirement: measure the resulting line box against each control's
    /// height and show the numbers, specifically for the new Xxs/Xs rungs. Input.razor's
    /// SizeClasses remarks predict the EXACT figures asserted here (available = clientHeight -
    /// 8px padding, since clientHeight already excludes the 2px border): Xxs/Compact
    /// (h-5=20px border-box -> 18px client height -> 10px available) and Xxs/Comfortable
    /// (h-6=24px -> 22px client height -> 14px available) both overflow a 16px leading-none
    /// line box; Xxs/Spacious (h-7=28px -> 26px client height -> 18px available) and every Xs
    /// combo except Compact fit with margin to spare. This is a physical conflict between the
    /// 16px iOS-zoom floor (text-base, mandatory on mobile) and a sub-24px control height, not
    /// a bug the leading-none fix can close on its own — flagged for the owner per the
    /// campaign's "flag loudly, don't silently invent a fix beyond what was asked" convention.
    /// </summary>
    [Theory]
    [InlineData("Xxs", "Compact", 18.0, 10.0)]
    [InlineData("Xxs", "Comfortable", 22.0, 14.0)]
    [InlineData("Xxs", "Spacious", 26.0, 18.0)]
    [InlineData("Xs", "Compact", 22.0, 14.0)]
    [InlineData("Xs", "Comfortable", 26.0, 18.0)]
    [InlineData("Xs", "Spacious", 30.0, 22.0)]
    public async Task Xxs_And_Xs_Line_Box_Measurements_Match_Predicted_Geometry(string size, string density, double expectedClientHeight, double expectedAvailable)
    {
        await _page.SetViewportSizeAsync(MobileWidth, 900);
        var geometry = await MeasureGeometryAsync($"size-{size}-density-{density}");
        var available = geometry.ClientHeight - geometry.PaddingTop - geometry.PaddingBottom;
        var lineBoxFits = geometry.LineHeight <= available;
        var expectedFits = expectedAvailable >= 16.0; // leading-none line box = 1 * 16px text-base

        Assert.Equal(expectedClientHeight, geometry.ClientHeight);
        Assert.Equal(expectedAvailable, available);
        Assert.Equal(16.0, geometry.LineHeight); // max-md:leading-none -> line-height: 1 * 16px
        Assert.Equal(expectedFits, lineBoxFits);
    }

    private async Task<BoxGeometry> MeasureGeometryAsync(string testId)
    {
        // Wait for THIS specific element to attach before touching it — see the class-level
        // remarks (CI flake fix, item 1) for why a raw querySelector isn't safe here.
        var locator = ByTestId(testId);
        await locator.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached });

        // el.clientHeight is the border-box height MINUS border (it already excludes border,
        // per the DOM spec) but still INCLUDES padding — so the content area available for the
        // line box is clientHeight minus padding only. Border must NOT be subtracted a second
        // time here (an earlier draft of this test double-subtracted it, silently making the
        // assertion 2px stricter than the real available space).
        return await locator.EvaluateAsync<BoxGeometry>(
            @"(el) => {
                const cs = getComputedStyle(el);
                return {
                    LineHeight: parseFloat(cs.lineHeight),
                    ClientHeight: el.clientHeight,
                    PaddingTop: parseFloat(cs.paddingTop),
                    PaddingBottom: parseFloat(cs.paddingBottom),
                };
            }");
    }

    private sealed class BoxGeometry
    {
        public double LineHeight { get; set; }
        public double ClientHeight { get; set; }
        public double PaddingTop { get; set; }
        public double PaddingBottom { get; set; }
    }
}
