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

    private async Task<double> ComputedColumnGapPxAsync(string size)
        => await _page.EvaluateAsync<double>(
            @"(size) => {
                const btn = document.querySelector(`[data-testid=""shimmer-${size}""]`);
                const span = btn.querySelector(':scope > span');
                return parseFloat(getComputedStyle(span).columnGap);
            }",
            size);

    private async Task<double> MeasuredIconToLabelGapPxAsync(string size)
        => await _page.EvaluateAsync<double>(
            @"(size) => {
                const icon = document.querySelector(`[data-testid=""shimmer-${size}-icon""]`);
                const label = document.querySelector(`[data-testid=""shimmer-${size}-label""]`);
                const iconRect = icon.getBoundingClientRect();
                const labelRect = label.getBoundingClientRect();
                return labelRect.left - iconRect.right;
            }",
            size);

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

    [Fact]
    public async Task Sm_Inner_Span_Computed_Gap_Is_6px_Not_8px()
    {
        var gap = await ComputedColumnGapPxAsync("Sm");
        Assert.Equal(6, gap);

        var measured = await MeasuredIconToLabelGapPxAsync("Sm");
        Assert.Equal(6, measured, 1);
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
