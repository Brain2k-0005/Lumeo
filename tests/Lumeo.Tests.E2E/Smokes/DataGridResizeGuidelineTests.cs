using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Smokes;

/// <summary>
/// Field report §18.4: the column-resize guideline is position:fixed and used to take the
/// table's rectangle. Under virtualization the table is as tall as every row and its top sits
/// far above the viewport once scrolled, so the line ran from above the toolbar to below the
/// window. It spans the grid's scroll container now, clamped to the viewport.
/// </summary>
public class DataGridResizeGuidelineTests : PlaywrightTestBase
{
    private const string Grid = "[data-testid=datagrid-virtualized]";

    private const string ScrollInside =
        "() => { const g = document.querySelector('" + Grid + "'); " +
        "const s = [...g.querySelectorAll('*')].find(el => el.scrollHeight > el.clientHeight + 50 && getComputedStyle(el).overflowY !== 'visible'); " +
        "if (s) { s.scrollTop = 6000; s.dispatchEvent(new Event('scroll')); } }";

    // [line top, line height, expected top, expected height, table height]
    private const string Measure =
        "() => { const g = document.querySelector('" + Grid + "'); " +
        "const s = [...g.querySelectorAll('*')].find(el => el.scrollHeight > el.clientHeight + 50 && getComputedStyle(el).overflowY !== 'visible'); " +
        "const l = document.querySelector('[data-slot=datagrid-resize-guideline]').getBoundingClientRect(); " +
        "const r = s.getBoundingClientRect(); const t = g.querySelector('table').getBoundingClientRect(); " +
        "const top = Math.max(r.top, 0); const bottom = Math.min(r.bottom, innerHeight); " +
        "return [Math.round(l.top), Math.round(l.height), Math.round(top), Math.round(bottom - top), Math.round(t.height)]; }";

    [Fact]
    public async Task Resize_Guideline_Spans_The_Scroll_Container_Not_The_Virtualized_Table()
    {
        await Goto("/components/data-grid");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var grid = Page.Locator(Grid);
        await grid.ScrollIntoViewIfNeededAsync();
        await Page.WaitForFunctionAsync("() => document.querySelectorAll('" + Grid + " tbody tr').length > 3", null, new() { Timeout = 20000 });

        await Page.EvaluateAsync(ScrollInside);
        await Page.WaitForTimeoutAsync(500);

        var handle = grid.Locator("[data-slot='datagrid-resize-handle']").First;
        var box = await handle.BoundingBoxAsync();
        Assert.NotNull(box);
        await Page.Mouse.MoveAsync(box!.X + box.Width / 2, box.Y + box.Height / 2);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(box.X + 30, box.Y + 5, new() { Steps = 5 });
        await Page.WaitForTimeoutAsync(150);

        var m = await Page.EvaluateAsync<int[]>(Measure);
        await Page.Mouse.UpAsync();

        Assert.True(m[4] > 5000, $"the virtualized table should be far taller than the viewport, was {m[4]}px");
        Assert.Equal(m[2], m[0]);
        Assert.Equal(m[3], m[1]);
    }
}
