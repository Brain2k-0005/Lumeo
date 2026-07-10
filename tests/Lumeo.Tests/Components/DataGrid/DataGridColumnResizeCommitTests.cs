using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// State/API-side coverage for the column-resize commit path — the browser owns
/// the live pointer drag, so here we drive the *committed* width the JS layer
/// reports (via the captured commit handler on the tracking interop) and assert
/// the grid clamps it, persists it, and raises <see cref="ColumnResizeEventArgs"/>
/// with the right AutoFit flag. Also asserts the keyboard-resize handler forwards
/// an arrow-key nudge to NudgeColumnResize with the correct signed step (the JS
/// side clamps + writes the DOM; that's browser-verified separately).
/// </summary>
public class DataGridColumnResizeCommitTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public DataGridColumnResizeCommitTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddScoped<IComponentInteropService>(_ => _interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Row(int Id, string Name);

    private static List<Row> Data() => new() { new(1, "Alice"), new(2, "Bob") };

    [Fact]
    public async Task ResizeCommit_Updates_Width_And_Raises_OnColumnResize_NotAutoFit()
    {
        var col = new DataGridColumn<Row> { Field = "Id", Title = "ID", Resizable = true };
        ColumnResizeEventArgs? captured = null;

        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.Columns, new List<DataGridColumn<Row>> { col })
            .Add(g => g.OnColumnResize, args => captured = args));

        var handleId = cut.Find("[data-slot='datagrid-resize-handle']").GetAttribute("id")!;

        // JS reports a committed drag width of 372px (dispatched on the renderer).
        var handled = false;
        await cut.InvokeAsync(async () => handled = await _interop.SimulateColumnResizeCommit(handleId, 372, autoFit: false));
        Assert.True(handled);

        // Width persisted onto the live column.
        Assert.Equal(372, col.Width);
        // Event raised with the committed width and AutoFit=false.
        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(captured);
            Assert.Equal(col.Id, captured!.ColumnId);
            Assert.Equal(372, captured.Width);
            Assert.False(captured.AutoFit);
        });
    }

    [Fact]
    public async Task AutoFitCommit_Raises_OnColumnResize_With_AutoFit_True()
    {
        var col = new DataGridColumn<Row> { Field = "Id", Title = "ID", Resizable = true };
        ColumnResizeEventArgs? captured = null;

        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.Columns, new List<DataGridColumn<Row>> { col })
            .Add(g => g.OnColumnResize, args => captured = args));

        var handleId = cut.Find("[data-slot='datagrid-resize-handle']").GetAttribute("id")!;

        // Double-click auto-fit reports the measured content width with autoFit=true.
        await cut.InvokeAsync(() => _interop.SimulateColumnResizeCommit(handleId, 210, autoFit: true));

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(captured);
            Assert.True(captured!.AutoFit);
            Assert.Equal(210, captured.Width);
        });
    }

    [Fact]
    public async Task ResizeCommit_Clamps_To_Column_MinMax()
    {
        // A committed width above Max must be clamped down before persist/emit — the
        // grid is the source of truth for the constraint, not JS alone.
        var col = new DataGridColumn<Row> { Field = "Id", Title = "ID", Resizable = true, MinWidth = 80, MaxWidth = 300 };
        ColumnResizeEventArgs? captured = null;

        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.Columns, new List<DataGridColumn<Row>> { col })
            .Add(g => g.OnColumnResize, args => captured = args));

        var handleId = cut.Find("[data-slot='datagrid-resize-handle']").GetAttribute("id")!;

        await cut.InvokeAsync(() => _interop.SimulateColumnResizeCommit(handleId, 999, autoFit: false));
        Assert.Equal(300, col.Width);

        await cut.InvokeAsync(() => _interop.SimulateColumnResizeCommit(handleId, 10, autoFit: false));
        Assert.Equal(80, col.Width);

        cut.WaitForAssertion(() => Assert.Equal(80, captured!.Width));
    }

    [Fact]
    public void ResizeHandle_Is_Separator_With_Aria_And_Not_A_Tab_Stop()
    {
        var col = new DataGridColumn<Row> { Field = "Id", Title = "ID", Resizable = true, MinWidth = 60, MaxWidth = 400, Width = 150 };

        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.Columns, new List<DataGridColumn<Row>> { col }));

        var handle = cut.Find("[data-slot='datagrid-resize-handle']");
        Assert.Equal("separator", handle.GetAttribute("role"));
        Assert.Equal("vertical", handle.GetAttribute("aria-orientation"));
        // NOT a Tab stop — the grid keeps one roving tab stop; the handle is reached
        // via the focused header cell (Ctrl+Arrow) or a click.
        Assert.Equal("-1", handle.GetAttribute("tabindex"));
        Assert.False(string.IsNullOrWhiteSpace(handle.GetAttribute("aria-label")));
        Assert.Equal("60", handle.GetAttribute("aria-valuemin"));
        Assert.Equal("400", handle.GetAttribute("aria-valuemax"));
        Assert.Equal("150", handle.GetAttribute("aria-valuenow"));
    }

    [Theory]
    [InlineData("ArrowRight", false, 10)]
    [InlineData("ArrowLeft", false, -10)]
    [InlineData("ArrowRight", true, 1)]
    public void Header_CtrlArrow_Resizes_Focused_Column(string key, bool shift, double expectedDelta)
    {
        var col = new DataGridColumn<Row> { Field = "Id", Title = "ID", Resizable = true };

        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.Columns, new List<DataGridColumn<Row>> { col }));

        var header = cut.Find("th[data-slot='datagrid-header-cell']");
        var handleId = cut.Find("[data-slot='datagrid-resize-handle']").GetAttribute("id");

        header.TriggerEvent("onkeydown", new KeyboardEventArgs { Key = key, CtrlKey = true, ShiftKey = shift });

        var nudge = Assert.Single(_interop.NudgeColumnResizeCalls);
        Assert.Equal(handleId, nudge.HandleId);
        Assert.Equal(expectedDelta, nudge.Delta);
    }

    [Fact]
    public void Header_PlainArrow_Does_Not_Resize()
    {
        // Without Ctrl, ArrowLeft/Right is column navigation, not resize.
        var col = new DataGridColumn<Row> { Field = "Id", Title = "ID", Resizable = true };
        var col2 = new DataGridColumn<Row> { Field = "Name", Title = "Name", Resizable = true };

        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.Columns, new List<DataGridColumn<Row>> { col, col2 }));

        cut.Find("th[data-slot='datagrid-header-cell']")
           .TriggerEvent("onkeydown", new KeyboardEventArgs { Key = "ArrowRight" });

        Assert.Empty(_interop.NudgeColumnResizeCalls);
    }

    [Theory]
    [InlineData("ArrowRight", false, 10)]
    [InlineData("ArrowLeft", false, -10)]
    [InlineData("ArrowRight", true, 1)]   // Shift = fine step
    [InlineData("ArrowLeft", true, -1)]
    public void Keyboard_Arrow_Forwards_Signed_Nudge(string key, bool shift, double expectedDelta)
    {
        var col = new DataGridColumn<Row> { Field = "Id", Title = "ID", Resizable = true };

        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.Columns, new List<DataGridColumn<Row>> { col }));

        var handle = cut.Find("[data-slot='datagrid-resize-handle']");
        var handleId = handle.GetAttribute("id");

        handle.TriggerEvent("onkeydown", new KeyboardEventArgs { Key = key, ShiftKey = shift });

        var nudge = Assert.Single(_interop.NudgeColumnResizeCalls);
        Assert.Equal(handleId, nudge.HandleId);
        Assert.Equal(expectedDelta, nudge.Delta);
    }

    [Fact]
    public void Keyboard_NonArrow_Key_Does_Not_Nudge()
    {
        var col = new DataGridColumn<Row> { Field = "Id", Title = "ID", Resizable = true };

        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.Columns, new List<DataGridColumn<Row>> { col }));

        cut.Find("[data-slot='datagrid-resize-handle']")
           .TriggerEvent("onkeydown", new KeyboardEventArgs { Key = "Enter" });

        Assert.Empty(_interop.NudgeColumnResizeCalls);
    }
}
