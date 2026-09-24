using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Smokes;

/// <summary>
/// SQL Analyst field report against 5.11.0, LU-12 and LU-13 (both
/// <c>Virtualized="true"</c> + <c>OnRangeRequest</c> — server virtualization). Real-browser
/// timing effects DataGridVirtualizedLoadingStateTests (bUnit) reaches at the mechanism level
/// (the gate / the branch guard) but can't fully observe end-to-end — an actual mounted
/// <c>&lt;Virtualize&gt;</c>, an actual localStorage round-trip, an actual page reload.
///
/// LU-12: <c>IsLoading="true"</c> used to unmount <c>&lt;Virtualize&gt;</c> (and its
/// ItemsProvider) entirely instead of overlaying a loading cue beside it.
/// LU-13: a <c>LayoutStorageKey</c>-persisted sort used to reach the FIRST range request only
/// on a second fetch, after <c>&lt;Virtualize&gt;</c>'s own default-sorted one.
///
/// See docs/Lumeo.Docs/Pages/E2E/DataGridVirtualizedLoadingPreview.razor for the harness:
/// #request-log records every OnRangeRequest call ("n:sortField:sortDirection" per line), so
/// order — not just final state — is directly inspectable.
/// </summary>
public class DataGridVirtualizedLoadingTests : PlaywrightTestBase
{
    private async Task<string> RequestLogAsync() =>
        await Page.Locator("#request-log").InnerTextAsync();

    private async Task<int> RequestCountAsync()
    {
        var attr = await Page.Locator("#request-count").GetAttributeAsync("data-count");
        return int.Parse(attr ?? "0");
    }

    // --- LU-12: IsLoading must not unmount <Virtualize> — rows render once it's toggled
    // back off, proving the ItemsProvider kept working underneath the loading state. ---

    [Fact]
    public async Task Toggling_IsLoading_Does_Not_Break_Subsequent_Rendering()
    {
        await Goto("/e2e/datagrid-virtualized-loading");
        await Page.WaitForSelectorAsync("#grid-wrapper table");
        await Page.WaitForTimeoutAsync(300);

        var before = await RequestCountAsync();
        Assert.True(before > 0, "no initial range request was ever made");

        await Page.ClickAsync("#toggle-loading"); // IsLoading -> true
        await Page.WaitForTimeoutAsync(300);

        // Skeleton must be visible while loading...
        Assert.True(await Page.Locator("[data-slot='skeleton']").CountAsync() > 0,
            "no loading cue shown while IsLoading=true");

        await Page.ClickAsync("#toggle-loading"); // IsLoading -> false
        await Page.WaitForTimeoutAsync(300);

        // ...and real rows must still be there afterwards — if <Virtualize> had been
        // unmounted and its ItemsProvider lost, the grid would be stuck empty here.
        Assert.True(await Page.Locator("#grid-wrapper td").CountAsync() > 0,
            "grid never recovered real rows after IsLoading toggled back off (LU-12 regression)");
    }

    // --- LU-13: a sort saved via LayoutStorageKey must be present on the FIRST logged
    // request after a reload, not only a later one. ---

    [Fact]
    public async Task Persisted_Sort_Is_Present_On_The_First_Request_After_Reload()
    {
        await Goto("/e2e/datagrid-virtualized-loading");
        await Page.WaitForSelectorAsync("#grid-wrapper table");
        await Page.WaitForTimeoutAsync(300);

        // Sort by Name descending (two clicks: ascending, then descending) via a real header
        // click, so LayoutStorageKey's auto-save picks it up.
        var nameHeader = Page.Locator("#grid-wrapper th[data-slot='datagrid-header-cell']").Filter(new() { HasText = "Name" });
        await nameHeader.Locator("button[data-slot='datagrid-sort-button']").ClickAsync();
        await Page.WaitForTimeoutAsync(150);
        await nameHeader.Locator("button[data-slot='datagrid-sort-button']").ClickAsync();
        await Page.WaitForTimeoutAsync(1000); // 500ms autosave debounce + localStorage write

        await Page.ReloadAsync();
        await Page.WaitForSelectorAsync("#grid-wrapper table");
        await Page.WaitForTimeoutAsync(500);

        var log = await RequestLogAsync();
        var lines = log.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.NotEmpty(lines);

        // The FIRST line after reload must already carry the restored sort — the pre-fix
        // behavior logged "1:none:none" first and the restored sort only on a later line.
        Assert.StartsWith("1:Name:Descending", lines[0]);
    }
}
