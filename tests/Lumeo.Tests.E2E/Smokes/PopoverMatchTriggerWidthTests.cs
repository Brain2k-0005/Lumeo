using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Smokes;

/// <summary>
/// #518: <c>PopoverContent.MatchTriggerWidth</c> follows the trigger's REAL rendered width
/// (Radix's <c>--radix-popover-trigger-width</c> pattern). bUnit can only assert the JSInterop
/// call's arguments (see <c>PopoverMatchTriggerWidthTests</c> in the unit suite) — it can't
/// measure real layout, so this drives the "Match Trigger Width" demo on
/// <c>/components/popover</c> in a real browser and compares actual bounding boxes. Requires
/// the docs dev-server (see project README.md).
/// </summary>
public class PopoverMatchTriggerWidthTests : PlaywrightTestBase
{
    [Fact]
    public async Task Popover_Width_Matches_The_Trigger_Button_Width()
    {
        await Goto("/components/popover");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var trigger = Page.GetByRole(AriaRole.Button, new() { Name = "Framework" });
        await trigger.ScrollIntoViewIfNeededAsync();
        var triggerBox = await trigger.BoundingBoxAsync();
        Assert.NotNull(triggerBox);

        await trigger.ClickAsync();

        var content = Page.Locator("[data-slot='popover-content']").Filter(new()
        {
            HasText = "always matches the button above",
        });
        await content.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
        var contentBox = await content.BoundingBoxAsync();
        Assert.NotNull(contentBox);

        Assert.True(System.Math.Abs(contentBox!.Width - triggerBox!.Width) < 2,
            $"popover width {contentBox.Width} should match trigger width {triggerBox.Width}");
    }
}
