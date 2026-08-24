using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Smokes;

/// <summary>
/// Codex review of PR #386 (shadcn-wave0), round 3, finding 3 on
/// <c>src/Lumeo.Motion/UI/ShimmerButton/ShimmerButton.razor</c> — not observable from
/// bUnit, which never applies real CSS and so can't see the COMPUTED <c>gap</c> on the
/// actual flex container laying out the icon/label children.
///
/// Before the fix: the Xs arm's <c>gap-1</c> override landed on the OUTER
/// <c>&lt;button&gt;</c> (single child — the inner content <c>&lt;span&gt;</c> — so any
/// <c>gap-*</c> there is inert), while the inner span kept a hardcoded <c>gap-2</c>. A
/// class-string assertion (<c>Assert.Contains("gap-1", cls)</c> against the outer
/// button's class attribute) PASSED even though the rendered icon-to-label spacing
/// never moved off 8px. This suite reads the actual computed <c>column-gap</c> on the
/// inner span (the real flex container, found as the button's only child), and
/// cross-checks it against the measured pixel distance between the icon and label
/// bounding boxes — genuinely rendered/computed values, not class strings.
///
/// Drives <c>tests/Lumeo.Tests.ServerHost</c>'s <c>/e2e/shimmer-button-gap</c> page the
/// same way <see cref="InputSizingTests"/> drives <c>/e2e/input-sizing</c> — see that
/// class's remarks for why a separate base URL / no shared Gantt collection is used.
///
/// CI flake fix (master e2e red, 2026-08-12): this suite intermittently failed with
/// <c>Cannot read properties of null (reading 'querySelector')</c> — the exact same
/// symptom <see cref="InputSizingTests"/>'s class-level remarks document under "CI
/// flake fix, item 1". Root cause is identical: <c>tests/Lumeo.Tests.ServerHost</c>
/// renders with <c>InteractiveServerRenderMode(prerender: false)</c> (see
/// <c>App.razor</c>), so the initial HTML is an empty shell and every element only
/// appears once the SignalR circuit's first render batch arrives over the
/// already-open WebSocket. <c>Page.WaitForLoadStateAsync(NetworkIdle)</c> in
/// <c>InitializeAsync</c> only observes HTTP/JS asset downloads settling, not that
/// render batch — so a raw <c>document.querySelector</c> inside <c>Page.EvaluateAsync</c>
/// can run before the button/icon/label elements attach, especially under the shared
/// ServerHost process's resource contention from other Smokes classes running in
/// xUnit's default parallel collection. Verified NOT a missing-CSS-utility issue: the
/// actual CI error text was the null-querySelector TypeError above, not a wrong
/// computed-gap value, and a literal substring scan of the shipped
/// <c>src/Lumeo/wwwroot/css/lumeo-utilities.css</c> bundle confirms <c>.gap-1{</c>,
/// <c>.gap-1\.5{</c> and <c>.gap-2{</c> are all present. Fixed the same way
/// <see cref="InputSizingTests"/> was: every element lookup now goes through
/// <c>Locator.WaitForAsync</c> (which waits, up to its own timeout, for the element to
/// attach) before evaluating against it, instead of a one-shot
/// <c>document.querySelector</c> that assumes the element already exists.
/// </summary>
public class ShimmerButtonGapTests : IAsyncLifetime
{
    private static string HostBaseUrl { get; } =
        Environment.GetEnvironmentVariable("LUMEO_GANTT_E2E_BASE_URL")
        ?? "http://localhost:5299";

    private IPlaywright _playwright = default!;
    private IBrowser _browser = default!;
    private IPage _page = default!;

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        _page = await _browser.NewPageAsync();
        await _page.GotoAsync($"{HostBaseUrl}/e2e/shimmer-button-gap");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public async Task DisposeAsync()
    {
        await _page.CloseAsync();
        await _browser.CloseAsync();
        _playwright.Dispose();
    }

    // Resolves a data-testid to a Playwright Locator that WAITS (up to its default
    // timeout) for a matching element to attach to the DOM, instead of a one-shot
    // `document.querySelector` that assumes it already exists. See the class-level
    // remarks (CI flake fix) for why this matters on this specific host.
    private ILocator ByTestId(string testId) => _page.Locator($"[data-testid=\"{testId}\"]");

    private async Task<double> ComputedColumnGapPxAsync(string size)
    {
        var btn = ByTestId($"shimmer-{size}");
        await btn.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached });
        return await btn.EvaluateAsync<double>(
            @"(el) => {
                const span = el.querySelector(':scope > span');
                return parseFloat(getComputedStyle(span).columnGap);
            }");
    }

    private async Task<double> MeasuredIconToLabelGapPxAsync(string size)
    {
        var icon = ByTestId($"shimmer-{size}-icon");
        var label = ByTestId($"shimmer-{size}-label");
        await icon.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached });
        await label.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached });

        return await icon.EvaluateAsync<double>(
            @"(iconEl, testid) => {
                const label = document.querySelector(`[data-testid=""${testid}""]`);
                const iconRect = iconEl.getBoundingClientRect();
                const labelRect = label.getBoundingClientRect();
                return labelRect.left - iconRect.right;
            }",
            $"shimmer-{size}-label");
    }

    // Predicted-vs-actual (disable check): reverting InnerContentClass back to the
    // pre-fix hardcoded "gap-2" on the inner span (with the outer button's SizeClasses
    // still carrying the inert gap-1) would make this assertion FAIL for Xs — computed
    // column-gap would read 8px, not the expected 4px — proving the fixture actually
    // exercises the regression rather than trivially passing.
    [Fact]
    public async Task Xs_Inner_Span_Computed_Gap_Is_4px_Not_8px()
    {
        var gap = await ComputedColumnGapPxAsync("Xs");
        Assert.Equal(4, gap);

        var measured = await MeasuredIconToLabelGapPxAsync("Xs");
        Assert.Equal(4, measured, 1); // 1px tolerance for sub-pixel layout rounding
    }

    // 4px, not 6: Button moved its Sm gap from gap-1.5 to gap-1 in the 5.0 scale
    // alignment, and this component mirrors Button by contract. The "not 8" half of
    // the check is what still matters - 8px is what falls out if the size override is
    // dropped and BaseClasses' gap-2 wins.
    [Fact]
    public async Task Sm_Inner_Span_Computed_Gap_Is_4px_Not_8px()
    {
        var gap = await ComputedColumnGapPxAsync("Sm");
        Assert.Equal(4, gap);

        var measured = await MeasuredIconToLabelGapPxAsync("Sm");
        Assert.Equal(4, measured, 1);
    }

    [Theory]
    [InlineData("Default")]
    [InlineData("Lg")]
    public async Task Default_And_Lg_Inner_Span_Computed_Gap_Is_8px(string size)
    {
        var gap = await ComputedColumnGapPxAsync(size);
        Assert.Equal(8, gap);

        var measured = await MeasuredIconToLabelGapPxAsync(size);
        Assert.Equal(8, measured, 1);
    }
}
