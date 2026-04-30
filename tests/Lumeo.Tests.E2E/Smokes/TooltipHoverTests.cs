using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Smokes;

/// <summary>
/// Verifies Tooltip shows on hover using real mouse events.
///
/// bUnit cannot test hover-triggered visibility — it has no real mouse or CSS
/// pointer event simulation. These tests confirm the actual JS interop path.
///
/// Requires the docs dev-server to be running. See project README.md.
/// </summary>
public class TooltipHoverTests : PlaywrightTestBase
{
    [Fact]
    public async Task Tooltip_is_hidden_initially()
    {
        await Goto("/components/tooltip");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Tooltip content should not be visible before hovering
        var tooltipContent = Page.Locator("[role='tooltip']").First;
        var isVisible = await tooltipContent.IsVisibleAsync();

        // Tooltips may not render at all until hover — both hidden and absent are acceptable
        Assert.False(isVisible, "Tooltip content was visible before any hover interaction.");
    }

    [Fact]
    public async Task Tooltip_shows_on_hover()
    {
        await Goto("/components/tooltip");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Find the first tooltip trigger button
        var trigger = Page.Locator("[data-tooltip-trigger], button[aria-describedby], button:has-text('Hover')").First;

        // Hover over the trigger
        await trigger.HoverAsync();

        // Tooltip should appear
        var tooltipContent = Page.Locator("[role='tooltip']").First;
        try
        {
            await tooltipContent.WaitForAsync(new()
            {
                State = WaitForSelectorState.Visible,
                Timeout = 3000,
            });
            Assert.True(await tooltipContent.IsVisibleAsync());
        }
        catch (TimeoutException)
        {
            // Tooltip trigger selector might not match the demo layout exactly.
            // Log a diagnostic rather than failing hard — this is a smoke test.
            var pageContent = await Page.InnerTextAsync("body");
            throw new Exception(
                $"Tooltip did not appear within 3 s after hover. " +
                $"Check that the trigger selector matches the demo. " +
                $"Page body (truncated): {pageContent[..Math.Min(500, pageContent.Length)]}");
        }
    }
}
