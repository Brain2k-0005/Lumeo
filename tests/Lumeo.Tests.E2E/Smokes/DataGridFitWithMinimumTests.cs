using System.Text.Json;
using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Smokes;

/// <summary>
/// DocFlow field report against 5.11.0: with <c>ColumnSizing="FitWithMinimum"</c>,
/// <c>DataGridHeaderCell.StyleString</c> emitted ONLY a CSS <c>min-width</c> under
/// <c>table-layout: auto</c>. Two ways that failed the mode's own contract, both real-browser
/// layout effects bUnit's headless DOM diff cannot see:
///
/// 1. No ceiling — a `white-space: nowrap` cell's minimum content width always won regardless
///    of any width the cell declared, so a 12-column search grid rendered 1512px wide in a
///    1310px container instead of filling it.
/// 2. Resized widths ignored — `MinWidth ?? Width` unconditionally preferred the declared
///    MinWidth, so neither a live drag nor a `LayoutStorageKey`-restored width was ever
///    reflected in any CSS property; a drag "jumped back" on reload.
///
/// Fixed by measuring the grid's scroll container (a ResizeObserver reporting back to .NET)
/// and rendering `table-layout: fixed` at an explicit, computed pixel width per column — see
/// DataGridColumnFitTests (pure algorithm) and DataGridFitWithMinimumWidthsTests (bUnit,
/// JSInvokable-driven header style strings) for the rest of the coverage. This class measures
/// real rendered pixels against docs/Lumeo.Docs/Pages/E2E/DataGridFitWithMinimumPreview.razor.
/// </summary>
public class DataGridFitWithMinimumTests : PlaywrightTestBase
{
    private sealed record Rect(double Left, double Right, double Width);

    private async Task<List<Rect>> HeaderRectsAsync(string rootSelector)
    {
        var json = await Page.EvaluateAsync<string>($@"() => {{
            const ths = [...document.querySelectorAll('{rootSelector} th[data-slot=""datagrid-header-cell""]')];
            return JSON.stringify(ths.map(th => {{
                const r = th.getBoundingClientRect();
                return {{ Left: r.left, Right: r.right, Width: r.width }};
            }}));
        }}");
        return JsonSerializer.Deserialize<List<Rect>>(json)!;
    }

    private async Task<double> TableWidthAsync(string rootSelector) =>
        await Page.EvaluateAsync<double>(
            $@"() => document.querySelector('{rootSelector} table').getBoundingClientRect().width");

    /// <summary>
    /// The grid's own horizontal scroll wrapper's clientWidth — the exact element (and exact
    /// property) DataGrid's ResizeObserver measures (components.js's registerColumnFitObserver,
    /// via <c>_viewportId</c>). This can differ from the outer `#…-repro` wrapper's declared
    /// CSS width by a couple of px (the scroll wrapper's own border/scrollbar), so the "table
    /// fills the container exactly" assertions compare against THIS, not the outer div's
    /// literal style width, to stay a real invariant rather than tied to box-model incidentals.
    /// </summary>
    private async Task<double> ScrollWrapperClientWidthAsync(string rootSelector) =>
        await Page.EvaluateAsync<double>($@"() => {{
            const table = document.querySelector('{rootSelector} table');
            const wrap = document.querySelector('{rootSelector} .overflow-auto') ?? table.closest('.overflow-auto');
            return wrap.clientWidth;
        }}");

    // --- The exact DocFlow repro: two columns (Wide 150/100, Narrow 100/80), nowrap content
    // longer than either MinWidth, 300px container — must fill exactly, never below MinWidth. ---

    [Fact]
    public async Task Two_Column_Repro_Fills_300px_Container_Neither_Column_Below_MinWidth()
    {
        await Goto("/e2e/datagrid-fit-with-minimum");
        await Page.WaitForSelectorAsync("#two-col-repro table");
        await Page.WaitForTimeoutAsync(300); // let the ResizeObserver's first report land

        var tableWidth = await TableWidthAsync("#two-col-repro");
        var containerWidth = await ScrollWrapperClientWidthAsync("#two-col-repro");
        Assert.True(Math.Abs(tableWidth - containerWidth) < 1,
            $"table width {tableWidth} != measured container width {containerWidth}");

        var headers = await HeaderRectsAsync("#two-col-repro");
        Assert.Equal(2, headers.Count);
        Assert.True(headers[0].Width >= 100 - 1, $"Wide column {headers[0].Width} < its MinWidth 100");
        Assert.True(headers[1].Width >= 80 - 1, $"Narrow column {headers[1].Width} < its MinWidth 80");
    }

    // --- sum(MinWidth) = 180 > a shrunk 120px container: both columns pinned at their floor,
    // table grows past the container and the grid scrolls horizontally. ---

    [Fact]
    public async Task Sum_Of_MinWidth_Exceeding_Container_Scrolls_Horizontally()
    {
        await Goto("/e2e/datagrid-fit-with-minimum");
        await Page.WaitForSelectorAsync("#two-col-repro table");
        await Page.WaitForTimeoutAsync(300);

        await Page.EvaluateAsync(@"() => {
            document.querySelector('#two-col-repro').style.width = '120px';
        }");
        await Page.WaitForTimeoutAsync(400); // ResizeObserver round-trip + re-render

        var headers = await HeaderRectsAsync("#two-col-repro");
        Assert.True(Math.Abs(headers[0].Width - 100) < 1, $"Wide column {headers[0].Width}, expected its floor 100");
        Assert.True(Math.Abs(headers[1].Width - 80) < 1, $"Narrow column {headers[1].Width}, expected its floor 80");

        var scrolls = await Page.EvaluateAsync<bool>(@"() => {
            const wrap = document.querySelector('#two-col-repro .overflow-auto') ??
                         document.querySelector('#two-col-repro table').closest('.overflow-auto');
            return wrap.scrollWidth > wrap.clientWidth + 1;
        }");
        Assert.True(scrolls, "grid did not grow past its container / scroll horizontally");
    }

    // --- A drag-resize must survive a reload (LayoutStorageKey), clamped to MinWidth — not
    // "jump back" to content/declared width (DocFlow finding 2). ---

    [Fact]
    public async Task Drag_Resize_Survives_Reload_Via_LayoutStorageKey()
    {
        await Goto("/e2e/datagrid-fit-with-minimum");
        await Page.WaitForSelectorAsync("#two-col-repro table");
        await Page.WaitForTimeoutAsync(300);

        // Widen the wrapper first so there's slack to drag into (matches
        // DataGridColumnResizeTests' own setup rationale).
        await Page.EvaluateAsync(@"() => { document.querySelector('#two-col-repro').style.width = '500px'; }");
        await Page.WaitForTimeoutAsync(300);

        var handle = Page.Locator("#two-col-repro [aria-label*='esize']").First;
        var box = await handle.BoundingBoxAsync();
        Assert.NotNull(box);

        await Page.Mouse.MoveAsync(box!.X + box.Width / 2, box.Y + box.Height / 2);
        await Page.Mouse.DownAsync();
        // Drag the Narrow column's handle so its final width lands at ~90px — inside its
        // MinWidth (80) so the drag itself isn't the thing clamping it.
        var headersBefore = await HeaderRectsAsync("#two-col-repro");
        var targetRight = (float)(headersBefore[1].Left + 90);
        await Page.Mouse.MoveAsync(targetRight, box.Y + box.Height / 2, new() { Steps = 12 });
        await Page.Mouse.UpAsync();
        await Page.WaitForTimeoutAsync(1000); // 500ms autosave debounce + persist round-trip

        await Page.ReloadAsync();
        await Page.WaitForSelectorAsync("#two-col-repro table");
        await Page.WaitForTimeoutAsync(500);

        var headersAfter = await HeaderRectsAsync("#two-col-repro");
        Assert.True(Math.Abs(headersAfter[1].Width - 90) < 5,
            $"Narrow column width after reload: {headersAfter[1].Width}, expected ~90 (the persisted resize)");
    }

    // --- The 12-column finding: fills the container exactly instead of the DocFlow numbers
    // (1512/2035px in a 1310px container). ---

    [Fact]
    public async Task Twelve_Column_Repro_Fills_1310px_Container_Exactly()
    {
        await Goto("/e2e/datagrid-fit-with-minimum");
        await Page.WaitForSelectorAsync("#twelve-col-repro table");
        await Page.WaitForTimeoutAsync(300);

        var tableWidth = await TableWidthAsync("#twelve-col-repro");
        var containerWidth = await ScrollWrapperClientWidthAsync("#twelve-col-repro");
        Assert.True(Math.Abs(tableWidth - containerWidth) < 1,
            $"table width {tableWidth} != measured container width {containerWidth} (was 1512/2035px pre-fix against a 1310px wrapper)");
        Assert.True(Math.Abs(containerWidth - 1310) < 3, $"unexpected container width {containerWidth}, wrapper declared 1310px");

        var headers = await HeaderRectsAsync("#twelve-col-repro");
        Assert.Equal(12, headers.Count);
        Assert.All(headers, h => Assert.True(h.Width >= 80 - 1, $"column {h.Width} < its MinWidth 80"));
    }
}
