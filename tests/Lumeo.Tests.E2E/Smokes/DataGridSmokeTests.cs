using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Smokes;

/// <summary>
/// Real-browser smoke for DataGrid — the heaviest interactive component, with zero
/// browser coverage before. Asserts the grid actually renders a populated table on
/// its docs page (catches a regression that bUnit's logical tests can miss, e.g. a
/// JS/virtualization break that leaves the table empty). Requires the docs
/// dev-server (see project README.md).
/// </summary>
public class DataGridSmokeTests : PlaywrightTestBase
{
    [Fact]
    public async Task DataGrid_Renders_A_Populated_Table()
    {
        await Goto("/components/data-grid");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var table = Page.Locator("table").First;
        await table.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
        Assert.True(await table.IsVisibleAsync());

        // A header row plus at least one data row.
        Assert.True(await Page.Locator("table tr").CountAsync() > 1);
    }
}
