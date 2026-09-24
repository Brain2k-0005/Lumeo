using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// Coverage for the bulk/programmatic auto-size API added for issue #519
/// (field report DocFlow W1 — "a single action that runs auto-fit for EVERY
/// visible column at once", plus the already-existing per-column "Fit to
/// content" menu entry's public-API counterpart).
///
/// <see cref="Lumeo.DataGrid{TItem}.AutoSizeColumnAsync"/> and
/// <see cref="Lumeo.DataGrid{TItem}.AutoSizeAllColumnsAsync"/> both measure via
/// <see cref="IComponentInteropService.MeasureColumnContentWidth"/> (a DOM
/// measurement in the browser — verified separately in JS/E2E) and commit the
/// result through the exact same <c>CommitColumnWidthAsync</c> path a manual
/// resize/keyboard-nudge/double-click auto-fit uses, so clamping + persistence
/// + the <c>OnColumnResize</c> event are already covered by
/// <see cref="DataGridColumnResizeCommitTests"/>; these tests focus on WHICH
/// columns get measured/committed and the AutoFit flag.
/// </summary>
public class DataGridAutoSizeTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public DataGridAutoSizeTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddScoped<IComponentInteropService>(_ => _interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Row(int Id, string Name);
    private static List<Row> Data() => new() { new(1, "Alice"), new(2, "Bob") };

    [Fact]
    public async Task AutoSizeColumnAsync_Commits_Measured_Width_Clamped_To_MinMax()
    {
        var col = new DataGridColumn<Row> { Field = "Name", Title = "Name", Resizable = true, MinWidth = 60, MaxWidth = 200 };
        ColumnResizeEventArgs? captured = null;

        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.Columns, new List<DataGridColumn<Row>> { col })
            .Add(g => g.OnColumnResize, args => captured = args));

        _interop.MeasureColumnContentWidthByColumnId[col.Id] = 500; // above MaxWidth

        await cut.InvokeAsync(() => cut.Instance.AutoSizeColumnAsync("Name"));

        Assert.Equal(200, col.Width); // clamped down to MaxWidth
        Assert.Contains(_interop.MeasureColumnContentWidthCalls, c => c.ColumnId == col.Id);
        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(captured);
            Assert.True(captured!.AutoFit);
            Assert.Equal(200, captured.Width);
        });
    }

    [Fact]
    public async Task AutoSizeColumnAsync_Unknown_Field_Is_NoOp()
    {
        var col = new DataGridColumn<Row> { Field = "Name", Title = "Name", Resizable = true };
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.Columns, new List<DataGridColumn<Row>> { col }));

        await cut.InvokeAsync(() => cut.Instance.AutoSizeColumnAsync("DoesNotExist"));

        Assert.Empty(_interop.MeasureColumnContentWidthCalls);
        Assert.Null(col.Width);
    }

    [Fact]
    public async Task AutoSizeColumnAsync_NonResizable_Column_Is_NoOp()
    {
        var col = new DataGridColumn<Row> { Field = "Name", Title = "Name", Resizable = false };
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.Columns, new List<DataGridColumn<Row>> { col }));

        _interop.MeasureColumnContentWidthByColumnId[col.Id] = 500;

        await cut.InvokeAsync(() => cut.Instance.AutoSizeColumnAsync("Name"));

        Assert.Empty(_interop.MeasureColumnContentWidthCalls);
        Assert.Null(col.Width);
    }

    [Fact]
    public async Task AutoSizeColumnAsync_Zero_Measurement_Does_Not_Commit()
    {
        // The JS side returns 0 when the column can't be found/measured — the C#
        // side must treat that as "nothing to do", not as a literal 0px width.
        var col = new DataGridColumn<Row> { Field = "Name", Title = "Name", Resizable = true };
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.Columns, new List<DataGridColumn<Row>> { col }));

        // TrackingInteropService.MeasureColumnContentWidthResult defaults to 0.
        await cut.InvokeAsync(() => cut.Instance.AutoSizeColumnAsync("Name"));

        Assert.Single(_interop.MeasureColumnContentWidthCalls);
        Assert.Null(col.Width);
    }

    [Fact]
    public async Task AutoSizeAllColumnsAsync_Sizes_Only_Visible_Resizable_Columns()
    {
        var resizable = new DataGridColumn<Row> { Field = "Id", Title = "ID", Resizable = true, Visible = true };
        var fixedCol = new DataGridColumn<Row> { Field = "Name", Title = "Name", Resizable = false, Visible = true };
        var hidden = new DataGridColumn<Row> { Field = "Hidden", Title = "Hidden", Resizable = true, Visible = false };

        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.Columns, new List<DataGridColumn<Row>> { resizable, fixedCol, hidden }));

        _interop.MeasureColumnContentWidthByColumnId[resizable.Id] = 120;

        await cut.InvokeAsync(() => cut.Instance.AutoSizeAllColumnsAsync());

        var measuredIds = _interop.MeasureColumnContentWidthCalls.Select(c => c.ColumnId).ToList();
        Assert.Contains(resizable.Id, measuredIds);
        Assert.DoesNotContain(fixedCol.Id, measuredIds);   // not resizable
        Assert.DoesNotContain(hidden.Id, measuredIds);     // not visible
        Assert.Equal(120, resizable.Width);
    }

    [Fact]
    public async Task ResetColumnWidthsAsync_Restores_Declared_Widths_Without_Touching_Sort_Or_Filter()
    {
        var col = new DataGridColumn<Row> { Field = "Id", Title = "ID", Resizable = true, Sortable = true, Width = 100 };

        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.Columns, new List<DataGridColumn<Row>> { col }));

        // Commit a resize away from the declared width (simulating a manual drag).
        var handleId = cut.Find("[data-slot='datagrid-resize-handle']").GetAttribute("id")!;
        await cut.InvokeAsync(() => _interop.SimulateColumnResizeCommit(handleId, 350, autoFit: false));
        Assert.Equal(350, col.Width);

        // Apply a sort via a real header click — ResetColumnWidthsAsync must leave it
        // alone (unlike ResetLayoutAsync, which resets the whole layout).
        cut.Find("button[data-slot='datagrid-sort-button']").Click();
        var sortsBeforeReset = cut.Instance.GetCurrentLayout().Sorts;
        Assert.NotNull(sortsBeforeReset);
        Assert.Single(sortsBeforeReset!);

        await cut.InvokeAsync(() => cut.Instance.ResetColumnWidthsAsync());

        Assert.Equal(100, col.Width);
        var sortsAfterReset = cut.Instance.GetCurrentLayout().Sorts;
        Assert.NotNull(sortsAfterReset);
        Assert.Single(sortsAfterReset!);
    }

    [Fact]
    public async Task ToolbarButton_AutoSizeAllColumns_Invokes_AutoSizeAllColumnsAsync()
    {
        var col = new DataGridColumn<Row> { Field = "Name", Title = "Name", Resizable = true };

        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.ShowToolbar, true)
            .Add(g => g.ShowColumnChooser, true)
            .Add(g => g.Columns, new List<DataGridColumn<Row>> { col }));

        _interop.MeasureColumnContentWidthByColumnId[col.Id] = 175;

        // Open the column chooser popover, same as DataGridColumnVisibilityToggleTests.
        var columnsBtn = cut.FindAll("button")
            .First(b => (b.GetAttribute("id") ?? "").StartsWith("dg-columns-trigger"));
        columnsBtn.Click();

        var autoSizeBtn = cut.FindAll("button")
            .Single(b => b.GetAttribute("aria-label") == "Autosize all columns");
        await cut.InvokeAsync(() => autoSizeBtn.Click());

        cut.WaitForAssertion(() => Assert.Equal(175, col.Width));
    }
}
