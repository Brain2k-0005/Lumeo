using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Smokes;

/// <summary>
/// The enterprise battle test bUnit fundamentally cannot do: a REAL browser scrolling
/// a virtualized DataGrid backed by 50,000 client-side rows (the docs "Client
/// virtualisation" demo). Proves the live windowing — only a small slice of rows is
/// ever in the DOM (virtual scroll height ≈ 1.8M px over a 320px viewport), and
/// scrolling renders a COMPLETELY different window of rows rather than ballooning the
/// DOM. bUnit can't drive &lt;Virtualize&gt;'s scroll windowing, so this is the only
/// place it is actually verified.
///
/// Requires the docs dev-server. See project README.md.
/// </summary>
public class DataGridVirtualScrollTests : PlaywrightTestBase
{
    // Data rows carry a unique "#N" marker; spacer rows the virtualizer inserts have
    // no cells — filter to real data rows so spacers don't skew the assertions.
    private const string CountDataRows =
        "() => { const g = document.querySelector('[data-testid=datagrid-virtualized]'); " +
        "return g ? [...g.querySelectorAll('tbody tr')].filter(tr => tr.querySelector('td') && tr.innerText.includes('#')).length : 0; }";

    private const string FirstDataRow =
        "() => { const g = document.querySelector('[data-testid=datagrid-virtualized]'); if (!g) return ''; " +
        "const r = [...g.querySelectorAll('tbody tr')].filter(tr => tr.querySelector('td') && tr.innerText.includes('#'))[0]; " +
        "return r ? r.innerText.trim() : ''; }";

    private const string ScrollDown =
        "() => { const g = document.querySelector('[data-testid=datagrid-virtualized]'); " +
        "const s = [...g.querySelectorAll('*')].find(el => el.scrollHeight > el.clientHeight + 50 && getComputedStyle(el).overflowY !== 'visible'); " +
        "if (s) { s.scrollTop = Math.round(s.scrollHeight * 0.4); s.dispatchEvent(new Event('scroll')); } }";

    [Fact]
    public async Task Virtualized_grid_keeps_the_dom_windowed_and_shows_new_rows_on_scroll()
    {
        await Goto("/components/data-grid");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Page.Locator("[data-testid=datagrid-virtualized]").ScrollIntoViewIfNeededAsync();

        // Wait for the virtualizer's first window of data rows.
        await Page.WaitForFunctionAsync("() => (" + CountDataRows + ")() > 3", null, new() { Timeout = 20000 });

        var beforeCount = await Page.EvaluateAsync<int>(CountDataRows);
        var beforeFirst = await Page.EvaluateAsync<string>(FirstDataRow);

        // Only a small window is in the DOM — NOT all 50,000 rows.
        Assert.InRange(beforeCount, 4, 100);
        Assert.Contains("#", beforeFirst);

        // Scroll ~40% down the virtual height.
        await Page.EvaluateAsync(ScrollDown);

        // The virtualizer must render a DIFFERENT window (top data row changes).
        await Page.WaitForFunctionAsync(
            "(before) => { const g = document.querySelector('[data-testid=datagrid-virtualized]'); " +
            "const r = [...g.querySelectorAll('tbody tr')].filter(tr => tr.querySelector('td') && tr.innerText.includes('#'))[0]; " +
            "return r && r.innerText.trim() !== before; }",
            beforeFirst, new() { Timeout = 20000 });

        var afterCount = await Page.EvaluateAsync<int>(CountDataRows);
        var afterFirst = await Page.EvaluateAsync<string>(FirstDataRow);

        // Still windowed (DOM did not balloon toward 50k) and showing new rows.
        Assert.InRange(afterCount, 4, 100);
        Assert.NotEqual(beforeFirst, afterFirst);
    }
}
