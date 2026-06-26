using System.Diagnostics;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// Enterprise-scale "battle test" for <see cref="DataGrid{TItem}"/>: proves the grid
/// works against data sets of MILLIONS of rows without materialising them — the load
/// profile Lumeo must survive to be a UI library for large projects.
///
/// Three scale strategies are exercised:
///   1. Server virtualization (<c>Virtualized</c> + <c>OnRangeRequest</c>): the grid
///      pulls only the visible window from a source that is NEVER fully materialised —
///      the backing "table" is 1,000,000 rows generated on demand from an int range.
///   2. Client pipeline perf guard: a 1,000,000-row in-memory set renders within a
///      CI-safe time budget and only a bounded window reaches the DOM (regression
///      guard against accidental O(n^2) and against rendering a million &lt;tr&gt;).
///   3. Column virtualization: a very wide grid renders only a bounded column window.
/// </summary>
public class DataGridScaleTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public DataGridScaleTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Row(int Id, string Name, long Value);

    private const int Millions = 1_000_000;

    private static Row Generate(int i) => new(i, $"Row {i}", (long)i * 7 % 1_000_003);

    private static List<DataGridColumn<Row>> Columns() => new()
    {
        new() { Field = "Id", Title = "ID", Sortable = true },
        new() { Field = "Name", Title = "Name", Sortable = true, Filterable = true },
        new() { Field = "Value", Title = "Value", Sortable = true },
    };

    // -------------------------------------------------------------------------
    // 1. Server virtualization — a 1,000,000-row source is mounted LAZILY.
    //
    // bUnit's headless DOM does not drive <Virtualize>'s IntersectionObserver, so
    // the live, viewport-driven windowing is covered by the Playwright E2E. What
    // IS verifiable here — and a real regression guard — is that wiring a
    // million-row server source does NOT eagerly materialise it on mount, every
    // window the grid asks for stays bounded, and the refresh API dispatches
    // cleanly and quickly.
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Server_virtualized_grid_mounts_a_million_row_source_without_materialising_it()
    {
        var totalRowsServed = 0;
        var maxWindow = 0;

        ValueTask<DataGridRangeResponse<Row>> Provider(DataGridRangeRequest req)
        {
            // Any attempt to pull the whole table (or a huge slice) in one window is a
            // virtualization failure — a real million-row grid must page its source.
            Assert.True(req.Count <= 10_000,
                $"Virtualized grid requested {req.Count} rows in one window — not virtualizing.");
            maxWindow = Math.Max(maxWindow, req.Count);

            // Rows are generated ON DEMAND from the int range — the 1,000,000-row
            // "table" is never allocated as a list anywhere in this test.
            var available = Math.Max(0, Millions - req.StartIndex);
            var slice = Enumerable.Range(req.StartIndex, Math.Min(req.Count, available))
                .Select(Generate)
                .ToList();
            totalRowsServed += slice.Count;
            return ValueTask.FromResult(new DataGridRangeResponse<Row>(slice, Millions));
        }

        var sw = Stopwatch.StartNew();
        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(x => x.Items, Array.Empty<Row>())
            .Add(x => x.Virtualized, true)
            .Add(x => x.OnRangeRequest, (Func<DataGridRangeRequest, ValueTask<DataGridRangeResponse<Row>>>)Provider)
            .Add(x => x.Columns, Columns())
            .Add(x => x.Height, "400px"));
        // The refresh API must dispatch the windowed-fetch pipeline without error.
        await cut.Instance.RefreshVirtualizedAsync();
        sw.Stop();

        // The grid mounts a logically-1,000,000-row source but never eagerly pulls
        // it: the on-demand source is materialised only in bounded windows (≈0 here,
        // since bUnit doesn't scroll). A naive impl that enumerates the source to
        // count it would blow past this guard.
        Assert.True(totalRowsServed < 50_000,
            $"Server-virtualized grid pulled {totalRowsServed} of {Millions} rows on mount — not lazy.");
        Assert.True(maxWindow <= 10_000, $"Largest window was {maxWindow} rows.");
        Assert.True(sw.ElapsedMilliseconds < 3_000,
            $"Mount + refresh of a 1M-row virtualized grid took {sw.ElapsedMilliseconds}ms.");
        Assert.NotNull(cut.Instance);
    }

    // -------------------------------------------------------------------------
    // 2. Client pipeline — 1,000,000 in-memory rows, bounded DOM + time budget.
    // -------------------------------------------------------------------------
    [Fact]
    public void Client_grid_with_a_million_rows_renders_a_bounded_window_within_budget()
    {
        var data = Enumerable.Range(0, Millions).Select(Generate).ToList();

        var sw = Stopwatch.StartNew();
        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(x => x.Items, data)
            .Add(x => x.Columns, Columns())
            .Add(x => x.PageSize, 50)
            .Add(x => x.ShowPagination, true)
            .Add(x => x.Height, "400px"));
        sw.Stop();

        // The 1M-row first render must complete within a generous CI-safe budget;
        // a blow-out here flags an accidental-quadratic regression in the pipeline.
        Assert.True(sw.ElapsedMilliseconds < 10_000,
            $"Client-side 1M-row grid first render took {sw.ElapsedMilliseconds}ms.");

        // Crucially, the DOM holds only a bounded window — NOT a million rows —
        // whether the grid pages or client-virtualizes.
        var rows = cut.FindAll("tbody tr");
        Assert.InRange(rows.Count, 1, 5_000);
    }

    // -------------------------------------------------------------------------
    // 3. Client pipeline — sort over a million rows stays correct + bounded.
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Sorting_a_million_rows_descending_surfaces_the_true_max_within_budget()
    {
        var data = Enumerable.Range(0, Millions).Select(Generate).ToList();
        var expectedMaxId = Millions - 1;

        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(x => x.Items, data)
            .Add(x => x.Columns, Columns())
            .Add(x => x.PageSize, 25)
            .Add(x => x.ShowPagination, true)
            .Add(x => x.Height, "400px"));

        // Click the sortable "ID" header twice: none -> ascending -> descending.
        var idHeader = cut.FindAll("thead th").First(c => c.TextContent.Contains("ID"));
        var sw = Stopwatch.StartNew();
        await cut.InvokeAsync(() => idHeader.QuerySelector("button")!.Click());
        await cut.InvokeAsync(() => cut.FindAll("thead th").First(c => c.TextContent.Contains("ID")).QuerySelector("button")!.Click());
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 12_000,
            $"Two sorts over 1M rows took {sw.ElapsedMilliseconds}ms.");
        // The largest Id (999999) must be on the first descending page — proves the
        // sort actually ran over the whole set, not just the first page.
        Assert.Contains($"Row {expectedMaxId}", cut.Markup);
    }

    // -------------------------------------------------------------------------
    // 4. Column virtualization — a very wide grid renders a bounded column window.
    // -------------------------------------------------------------------------
    [Fact]
    public void Column_virtualization_caps_the_rendered_column_count_on_a_wide_grid()
    {
        const int columnCount = 300;
        var columns = Enumerable.Range(0, columnCount)
            .Select(i => new DataGridColumn<Row> { Field = "Id", Title = $"Col {i}" })
            .ToList();
        var data = Enumerable.Range(0, 100).Select(Generate).ToList();

        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(x => x.Items, data)
            .Add(x => x.Columns, columns)
            .Add(x => x.ColumnVirtualize, true)
            .Add(x => x.MaxVisibleColumns, 30)
            .Add(x => x.ShowPagination, false)
            .Add(x => x.Height, "400px"));

        // Header cells are capped to roughly MaxVisibleColumns — NOT all 300.
        var headerCells = cut.FindAll("thead th");
        Assert.True(headerCells.Count < columnCount,
            $"ColumnVirtualize rendered {headerCells.Count} of {columnCount} columns — not virtualizing.");
    }
}
