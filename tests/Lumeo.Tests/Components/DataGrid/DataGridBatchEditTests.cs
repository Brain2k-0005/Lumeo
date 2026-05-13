using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// Tests for the rc.36 DataGrid batch / buffered edit mode UI.
/// Covers: buffered cell edits, dirty markers, Save all + Discard,
/// HasPendingChanges, ShowAddRow + NewItemFactory, ColumnVirtualize.
/// </summary>
public class DataGridBatchEditTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataGridBatchEditTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private class Person
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Department { get; set; } = "";
    }

    private static List<Person> Sample() => new()
    {
        new Person { Id = 1, Name = "Alice", Department = "Eng" },
        new Person { Id = 2, Name = "Bob",   Department = "Sales" },
    };

    private static List<DataGridColumn<Person>> MakeColumns() => new()
    {
        new() { Field = "Name",       Title = "Name" },
        new() { Field = "Department", Title = "Department" },
    };

    /// <summary>
    /// Reflection helpers: SetBatchValue is internal, _visibleColumns is private.
    /// Tests reach in this way to simulate cell commits without depending on the
    /// (separately-tested) Cell edit machinery.
    /// </summary>
    private static System.Reflection.FieldInfo VisibleColumnsField =>
        typeof(DataGrid<Person>).GetField("_visibleColumns",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;

    private static System.Reflection.MethodInfo SetBatchValueMethod =>
        typeof(DataGrid<Person>).GetMethod("SetBatchValue",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;

    private static DataGridColumn<Person> GetVisibleColumn(DataGrid<Person> grid, string field)
    {
        var cols = (System.Collections.IEnumerable)VisibleColumnsField.GetValue(grid)!;
        foreach (var c in cols)
        {
            var col = (DataGridColumn<Person>)c;
            if (col.Field == field) return col;
        }
        throw new InvalidOperationException($"Column '{field}' not visible");
    }

    [Fact]
    public void BatchMode_NoEdits_RendersNoPendingStrip()
    {
        var cut = _ctx.Render<DataGrid<Person>>(p => p
            .Add(x => x.Items, Sample())
            .Add(x => x.Columns, MakeColumns())
            .Add(x => x.EditMode, DataGridEditMode.Batch));

        Assert.DoesNotContain("datagrid-batch-strip", cut.Markup);
        Assert.False(cut.Instance.HasPendingChanges);
    }

    [Fact]
    public async Task BatchMode_BufferedEdit_MarksCellDirtyAndDoesNotFireOnBatchSave()
    {
        DataGridBatchSaveEventArgs<Person>? captured = null;
        var cut = _ctx.Render<DataGrid<Person>>(p => p
            .Add(x => x.Items, Sample())
            .Add(x => x.Columns, MakeColumns())
            .Add(x => x.EditMode, DataGridEditMode.Batch)
            .Add(x => x.OnBatchSave, EventCallback.Factory.Create<DataGridBatchSaveEventArgs<Person>>(
                this, args => captured = args)));

        var grid = cut.Instance;
        // Items is a parameter — round-trip through the grid's actual list.
        var sample = (IEnumerable<Person>)typeof(DataGrid<Person>)
            .GetProperty("Items")!.GetValue(grid)!;
        var actualAlice = sample.First(p => p.Name == "Alice");
        var nameCol = GetVisibleColumn(grid, "Name");

        await cut.InvokeAsync(() =>
            SetBatchValueMethod.Invoke(grid, new object?[] { actualAlice, nameCol, "Alice2" }));

        Assert.True(grid.HasPendingChanges);
        Assert.Contains("datagrid-batch-strip", cut.Markup);
        // OnBatchSave must NOT fire until "Save all" is clicked.
        Assert.Null(captured);
        // Cell display should reflect the buffered value.
        Assert.Contains("Alice2", cut.Markup);
        // Dirty marker (border-l-2 border-warning) should be applied.
        Assert.Contains("border-warning", cut.Markup);
    }

    [Fact]
    public async Task SaveAll_FiresOnBatchSave_WithModifiedItem_AndClearsBuffer()
    {
        DataGridBatchSaveEventArgs<Person>? captured = null;
        var cut = _ctx.Render<DataGrid<Person>>(p => p
            .Add(x => x.Items, Sample())
            .Add(x => x.Columns, MakeColumns())
            .Add(x => x.EditMode, DataGridEditMode.Batch)
            .Add(x => x.OnBatchSave, EventCallback.Factory.Create<DataGridBatchSaveEventArgs<Person>>(
                this, args => captured = args)));

        var grid = cut.Instance;
        var sample = (IEnumerable<Person>)typeof(DataGrid<Person>)
            .GetProperty("Items")!.GetValue(grid)!;
        var actualAlice = sample.First(p => p.Name == "Alice");
        var nameCol = GetVisibleColumn(grid, "Name");

        await cut.InvokeAsync(() =>
            SetBatchValueMethod.Invoke(grid, new object?[] { actualAlice, nameCol, "Alice2" }));
        Assert.True(grid.HasPendingChanges);

        var saveBtn = cut.Find("button[data-slot='datagrid-batch-save']");
        await saveBtn.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.NotNull(captured);
        Assert.Single(captured!.Modified);
        Assert.Empty(captured.Added);
        Assert.Same(actualAlice, captured.Modified[0].Item);
        Assert.Equal("Alice2", captured.Modified[0].ChangedFields["Name"]);
        Assert.False(captured.Modified[0].IsNew);
        Assert.False(grid.HasPendingChanges);
    }

    [Fact]
    public async Task Discard_ClearsBuffer_WithoutFiringOnBatchSave()
    {
        var saveCount = 0;
        var cut = _ctx.Render<DataGrid<Person>>(p => p
            .Add(x => x.Items, Sample())
            .Add(x => x.Columns, MakeColumns())
            .Add(x => x.EditMode, DataGridEditMode.Batch)
            .Add(x => x.OnBatchSave, EventCallback.Factory.Create<DataGridBatchSaveEventArgs<Person>>(
                this, _ => saveCount++)));

        var grid = cut.Instance;
        var sample = (IEnumerable<Person>)typeof(DataGrid<Person>)
            .GetProperty("Items")!.GetValue(grid)!;
        var actualAlice = sample.First(p => p.Name == "Alice");
        var nameCol = GetVisibleColumn(grid, "Name");

        await cut.InvokeAsync(() =>
            SetBatchValueMethod.Invoke(grid, new object?[] { actualAlice, nameCol, "AliceX" }));
        Assert.True(grid.HasPendingChanges);

        var discardBtn = cut.Find("button[data-slot='datagrid-batch-discard']");
        await discardBtn.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.False(grid.HasPendingChanges);
        Assert.Equal(0, saveCount);
        Assert.DoesNotContain("AliceX", cut.Markup);
    }

    [Fact]
    public async Task HasPendingChanges_ReflectsBufferState()
    {
        var cut = _ctx.Render<DataGrid<Person>>(p => p
            .Add(x => x.Items, Sample())
            .Add(x => x.Columns, MakeColumns())
            .Add(x => x.EditMode, DataGridEditMode.Batch));

        var grid = cut.Instance;
        Assert.False(grid.HasPendingChanges);

        var sample = (IEnumerable<Person>)typeof(DataGrid<Person>)
            .GetProperty("Items")!.GetValue(grid)!;
        var actualBob = sample.First(p => p.Name == "Bob");
        var deptCol = GetVisibleColumn(grid, "Department");

        await cut.InvokeAsync(() =>
            SetBatchValueMethod.Invoke(grid, new object?[] { actualBob, deptCol, "Ops" }));
        Assert.True(grid.HasPendingChanges);
        Assert.Single(grid.PendingModifiedItems);
        Assert.Empty(grid.PendingAddedItems);
    }

    [Fact]
    public async Task ShowAddRow_AddsItemToAddedBuffer_OnSave()
    {
        DataGridBatchSaveEventArgs<Person>? captured = null;
        var cut = _ctx.Render<DataGrid<Person>>(p => p
            .Add(x => x.Items, Sample())
            .Add(x => x.Columns, MakeColumns())
            .Add(x => x.EditMode, DataGridEditMode.Batch)
            .Add(x => x.ShowAddRow, true)
            .Add(x => x.NewItemFactory, (Func<Person>)(() => new Person { Id = 99, Name = "(new)" }))
            .Add(x => x.OnBatchSave, EventCallback.Factory.Create<DataGridBatchSaveEventArgs<Person>>(
                this, args => captured = args)));

        var grid = cut.Instance;
        Assert.False(grid.HasPendingChanges);

        var addBtn = cut.Find("button[data-slot='datagrid-add-row-button']");
        await addBtn.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.True(grid.HasPendingChanges);
        Assert.Single(grid.PendingAddedItems);

        var saveBtn = cut.Find("button[data-slot='datagrid-batch-save']");
        await saveBtn.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.NotNull(captured);
        Assert.Empty(captured!.Modified);
        Assert.Single(captured.Added);
        Assert.True(captured.Added[0].IsNew);
        Assert.Equal(99, captured.Added[0].Item.Id);
    }

    [Fact]
    public void BatchMode_AddRowTrigger_HiddenWhenShowAddRowFalse()
    {
        var cut = _ctx.Render<DataGrid<Person>>(p => p
            .Add(x => x.Items, Sample())
            .Add(x => x.Columns, MakeColumns())
            .Add(x => x.EditMode, DataGridEditMode.Batch)
            .Add(x => x.ShowAddRow, false)
            .Add(x => x.NewItemFactory, (Func<Person>)(() => new Person())));

        Assert.DoesNotContain("datagrid-add-row-button", cut.Markup);
    }

    [Fact]
    public void ColumnVirtualize_CapsRenderedColumns()
    {
        var cols = new List<DataGridColumn<Person>>();
        for (int i = 0; i < 40; i++)
        {
            cols.Add(new DataGridColumn<Person>
            {
                Field = "Name",
                Title = $"Col{i}"
            });
        }
        var cut = _ctx.Render<DataGrid<Person>>(p => p
            .Add(x => x.Items, Sample())
            .Add(x => x.Columns, cols)
            .Add(x => x.ColumnVirtualize, true)
            .Add(x => x.MaxVisibleColumns, 5));

        Assert.Contains("Col0", cut.Markup);
        Assert.Contains("Col4", cut.Markup);
        Assert.DoesNotContain("Col10", cut.Markup);
        Assert.DoesNotContain("Col39", cut.Markup);
    }
}
