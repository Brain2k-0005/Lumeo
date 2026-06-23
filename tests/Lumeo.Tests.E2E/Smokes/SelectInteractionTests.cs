using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Smokes;

/// <summary>
/// Real-browser smoke for the Select listbox — bUnit can't open the popover and
/// see the portaled, position:fixed listbox render. Exercises the heavyweight
/// form primitive that the unit suite covers logically but never in a browser.
/// Requires the docs dev-server (see project README.md).
/// </summary>
public class SelectInteractionTests : PlaywrightTestBase
{
    [Fact]
    public async Task Select_Opens_The_Listbox_On_Trigger_Click()
    {
        await Goto("/components/select");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var trigger = Page.Locator("button[role='combobox']").First;
        await trigger.ClickAsync();

        var listbox = Page.Locator("[role='listbox']").First;
        await listbox.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
        Assert.True(await listbox.IsVisibleAsync());
    }

    [Fact]
    public async Task Open_Select_Renders_Selectable_Options()
    {
        await Goto("/components/select");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Page.Locator("button[role='combobox']").First.ClickAsync();

        var options = Page.Locator("[role='option']");
        await options.First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
        Assert.True(await options.CountAsync() > 0);
    }
}
