using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using System.Text.Json;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// Tests for the public layout export/import JSON API and per-column
/// Reorderable drag-and-drop behaviour.
/// </summary>
public class DataGridLayoutJsonTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataGridLayoutJsonTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Row(int Id, string Name, string Email);

    private static List<Row> Data() => new()
    {
        new(1, "Alice", "a@x"),
        new(2, "Bob", "b@x"),
        new(3, "Charlie", "c@x"),
    };

    private static List<DataGridColumn<Row>> Cols() => new()
    {
        new() { Field = "Id", Title = "ID", Sortable = true },
        new() { Field = "Name", Title = "Name", Sortable = true, Filterable = true, Groupable = true },
        new() { Field = "Email", Title = "Email", Groupable = true },
    };

    // -------------------------------------------------------------------------
    // JSON export/import
    // -------------------------------------------------------------------------

    [Fact]
    public void ExportLayout_Returns_Valid_Json_With_Expected_Fields()
    {
        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.Columns, Cols())
            .Add(g => g.PageSize, 7));

        var json = cut.Instance.ExportLayout();
        Assert.False(string.IsNullOrWhiteSpace(json));

        var snap = JsonSerializer.Deserialize<DataGridLayoutSnapshot>(json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            });
        Assert.NotNull(snap);
        Assert.Equal(2, snap!.Version);
        Assert.Equal(3, snap.Columns.Count);
        Assert.Equal(7, snap.PageSize);
        Assert.Contains(snap.Columns, c => c.Field == "Id" && c.Order == 0);
        Assert.Contains(snap.Columns, c => c.Field == "Email" && c.Order == 2);
    }

    [Fact]
    public async Task ApplyLayoutJsonAsync_Round_Trips_Column_Order_And_Page_Size()
    {
        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.Columns, Cols())
            .Add(g => g.PageSize, 5));

        // Mutate state, export, then re-import into a fresh grid
        var exported = cut.Instance.ExportLayout();

        // Build a second grid and apply the JSON
        var cut2 = _ctx.Render<DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.Columns, Cols())
            .Add(g => g.PageSize, 2));

        await cut2.InvokeAsync(async () => await cut2.Instance.ApplyLayoutJsonAsync(exported));

        var snap = cut2.Instance.GetCurrentLayout();
        Assert.Equal(5, snap.PageSize); // page size restored from the JSON (original was 5)
        Assert.Equal(3, snap.Columns.Count);
    }

    [Fact]
    public async Task ApplyLayoutJsonAsync_Throws_JsonException_On_Malformed_Input()
    {
        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.Columns, Cols()));

        await Assert.ThrowsAsync<JsonException>(async () =>
            await cut.InvokeAsync(async () => await cut.Instance.ApplyLayoutJsonAsync("")));

        await Assert.ThrowsAsync<JsonException>(async () =>
            await cut.InvokeAsync(async () => await cut.Instance.ApplyLayoutJsonAsync("not-json!")));
    }

    [Fact]
    public async Task ApplyLayoutJsonAsync_Ignores_Columns_That_No_Longer_Exist()
    {
        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.Columns, Cols())
            .Add(g => g.PageSize, 5));

        // Craft a snapshot that references a column that isn't on the grid
        var snap = new DataGridLayoutSnapshot(
            Version: 1,
            Columns: new List<DataGridColumnLayout>
            {
                new("Id", 0, true, 100, PinDirection.None),
                new("GhostField", 1, true, 50, PinDirection.None),
                new("Name", 2, true, null, PinDirection.None),
                new("Email", 3, false, null, PinDirection.None),
            },
            Sorts: new(),
            Filters: new(),
            GlobalSearch: null,
            CurrentPage: 1,
            PageSize: 5,
            GroupBy: null);

        var json = JsonSerializer.Serialize(snap, new JsonSerializerOptions
        {
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        });

        // Should not throw
        await cut.InvokeAsync(async () => await cut.Instance.ApplyLayoutJsonAsync(json));

        var after = cut.Instance.GetCurrentLayout();
        Assert.Equal(3, after.Columns.Count); // ghost field skipped
        Assert.False(after.Columns.First(c => c.Field == "Email").Visible);
    }

    // -------------------------------------------------------------------------
    // Reorderable = false blocks drag
    // -------------------------------------------------------------------------

    [Fact]
    public void HeaderCell_Not_Draggable_When_Column_Reorderable_False()
    {
        var cols = Cols();
        cols[1].Reorderable = false; // "Name" column locked

        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.Columns, cols)
            .Add(g => g.Reorderable, true));

        var headerCells = cut.FindAll("th[data-slot='datagrid-header-cell']");
        Assert.Equal(3, headerCells.Count);

        // "Name" is the middle header — must NOT be draggable
        var nameTh = headerCells[1];
        Assert.Equal("false", nameTh.GetAttribute("draggable"));

        // The others should be draggable
        Assert.Equal("true", headerCells[0].GetAttribute("draggable"));
        Assert.Equal("true", headerCells[2].GetAttribute("draggable"));
    }

    [Fact]
    public void HeaderCell_Not_Draggable_When_Grid_Reorderable_False()
    {
        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.Columns, Cols())
            .Add(g => g.Reorderable, false));

        var headerCells = cut.FindAll("th[data-slot='datagrid-header-cell']");
        foreach (var th in headerCells)
        {
            Assert.Equal("false", th.GetAttribute("draggable"));
        }
    }

    // -------------------------------------------------------------------------
    // Group-panel runtime field persistence (v2 schema)
    // -------------------------------------------------------------------------

    [Fact]
    public void ExportLayout_Includes_GroupByFields_When_Group_Panel_Active()
    {
        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.Columns, Cols())
            .Add(g => g.ShowGroupPanel, true)
            .Add(g => g.GroupByFields, new[] { "Name" }));

        var json = cut.Instance.ExportLayout();
        var snap = JsonSerializer.Deserialize<DataGridLayoutSnapshot>(json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            });

        Assert.NotNull(snap);
        Assert.NotNull(snap!.GroupByFields);
        Assert.Contains("Name", snap.GroupByFields!);
    }

    [Fact]
    public async Task ApplyLayoutJsonAsync_Restores_Multi_Level_GroupByFields()
    {
        // Grid A: pre-seeded with two grouping levels, in order.
        var cutA = _ctx.Render<DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.Columns, Cols())
            .Add(g => g.ShowGroupPanel, true)
            .Add(g => g.GroupByFields, new[] { "Name", "Email" }));

        var exported = cutA.Instance.ExportLayout();

        // Grid B: fresh, no GroupByFields parameter. Apply the JSON.
        var cutB = _ctx.Render<DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.Columns, Cols())
            .Add(g => g.ShowGroupPanel, true));

        await cutB.InvokeAsync(async () => await cutB.Instance.ApplyLayoutJsonAsync(exported));

        var layout = cutB.Instance.GetCurrentLayout();
        Assert.NotNull(layout.GroupByFields);
        Assert.Equal(new[] { "Name", "Email" }, layout.GroupByFields!);

        // Panel chips render — markup should contain both field names.
        var panel = cutB.Find("[data-slot=\"datagrid-group-panel\"]");
        Assert.Contains("Name", panel.TextContent);
        Assert.Contains("Email", panel.TextContent);
    }

    [Fact]
    public async Task ApplyLayoutJsonAsync_Backcompat_v1_GroupBy_Falls_Back_To_Single_Level()
    {
        // Hand-craft a v1 payload: GroupBy populated, GroupByFields null.
        var v1Snap = new DataGridLayoutSnapshot(
            Version: 1,
            Columns: new List<DataGridColumnLayout>
            {
                new("Id", 0, true, null, PinDirection.None),
                new("Name", 1, true, null, PinDirection.None),
                new("Email", 2, true, null, PinDirection.None),
            },
            Sorts: new(),
            Filters: new(),
            GlobalSearch: null,
            CurrentPage: 1,
            PageSize: 10,
            GroupBy: "Name",
            GroupByFields: null);

        var json = JsonSerializer.Serialize(v1Snap, new JsonSerializerOptions
        {
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        });

        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.Columns, Cols())
            .Add(g => g.ShowGroupPanel, true));

        await cut.InvokeAsync(async () => await cut.Instance.ApplyLayoutJsonAsync(json));

        var layout = cut.Instance.GetCurrentLayout();
        Assert.NotNull(layout.GroupByFields);
        Assert.Equal(new[] { "Name" }, layout.GroupByFields!);
    }

    [Fact]
    public void SnapshotCurrentLayout_Captures_Runtime_GroupFields_From_Panel()
    {
        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.Columns, Cols())
            .Add(g => g.ShowGroupPanel, true));

        // 2.2.0: the group-panel add-level UI is a DropdownMenu now (was a
        // native <select>). Open the trigger and click the Name menu item.
        cut.Find("[data-slot=\"datagrid-group-add-trigger\"]").Click();
        cut.Find("[role=\"menu\"] [data-group-add-field=\"Name\"]").Click();

        var layout = cut.Instance.GetCurrentLayout();
        Assert.NotNull(layout.GroupByFields);
        Assert.Contains("Name", layout.GroupByFields!);
    }

    [Fact]
    public void HeaderCell_Drop_Reorders_Columns()
    {
        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.Columns, Cols())
            .Add(g => g.Reorderable, true));

        // Initial order: Id, Name, Email
        var before = cut.Instance.GetCurrentLayout();
        Assert.Equal("Id", before.Columns[0].Field);
        Assert.Equal("Name", before.Columns[1].Field);
        Assert.Equal("Email", before.Columns[2].Field);

        var headerCells = cut.FindAll("th[data-slot='datagrid-header-cell']");

        // Drag column 0 (Id) onto column 2 (Email)
        headerCells[0].TriggerEvent("ondragstart", new DragEventArgs());
        headerCells[2].TriggerEvent("ondrop", new DragEventArgs());

        var after = cut.Instance.GetCurrentLayout();
        // Id moved to index 2; Name/Email shifted left
        Assert.Equal("Name", after.Columns[0].Field);
        Assert.Equal("Email", after.Columns[1].Field);
        Assert.Equal("Id", after.Columns[2].Field);
    }

    // -------------------------------------------------------------------------
    // Regression: JSON-restored filter values arrive as JsonElement
    // -------------------------------------------------------------------------

    private record NumRow(int Id, string Name, string Email);

    [Fact]
    public async Task ApplyLayout_From_Json_Normalizes_JsonElement_Filter_Values()
    {
        // Ids chosen so numeric vs lexicographic comparison differ: ">5" must
        // keep 7 AND 10 — a JsonElement filter value isn't IComparable, so the
        // old path degraded to string compare and dropped 10 ("10" < "5").
        var data = new List<NumRow> { new(2, "Two", "t@x"), new(7, "Seven", "s@x"), new(10, "Ten", "x@x") };
        var cut = _ctx.Render<DataGrid<NumRow>>(p => p
            .Add(g => g.Items, data)
            .Add(g => g.Columns, new List<DataGridColumn<NumRow>>
            {
                new() { Field = "Id", Title = "ID", Filterable = true, FilterType = DataGridFilterType.Number },
                new() { Field = "Name", Title = "Name" },
            }));

        var layout = new DataGridLayout
        {
            Filters = new() { new FilterDescriptor("Id", FilterOperator.GreaterThan, 5, FilterType: DataGridFilterType.Number) }
        };
        var roundTripped = JsonSerializer.Deserialize<DataGridLayout>(JsonSerializer.Serialize(layout))!;
        Assert.IsType<JsonElement>(roundTripped.Filters![0].Value); // precondition: JSON path produces JsonElement

        await cut.InvokeAsync(() => cut.Instance.ApplyLayoutAsync(roundTripped));

        Assert.Contains("Seven", cut.Markup);
        Assert.Contains("Ten", cut.Markup);
        Assert.DoesNotContain("Two", cut.Markup);
    }

    // -------------------------------------------------------------------------
    // Regression: removing a chip after a layout restore must unhide its column
    // (_groupedColPrevState is empty after a restore — the live-grouping
    // snapshot isn't part of the persisted layout)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RemoveChip_After_Layout_Restore_Unhides_The_Grouped_Column()
    {
        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.Columns, Cols())
            .Add(g => g.ShowGroupPanel, true));

        var layout = new DataGridLayout
        {
            Columns = new()
            {
                new() { Field = "Id", Order = 0, Visible = true },
                // Saved while grouping had auto-hidden the grouped column:
                new() { Field = "Name", Order = 1, Visible = false },
                new() { Field = "Email", Order = 2, Visible = true },
            },
            GroupByFields = new() { "Name" }
        };
        await cut.InvokeAsync(() => cut.Instance.ApplyLayoutAsync(layout));

        Assert.DoesNotContain(cut.FindAll("th"), th => th.TextContent.Contains("Name"));
        Assert.NotEmpty(cut.FindAll("[data-slot=\"datagrid-group-row\"]"));

        // Remove the restored chip — the column must come back.
        cut.Find("[data-slot=\"datagrid-group-panel\"] span button").Click();

        Assert.Empty(cut.FindAll("[data-slot=\"datagrid-group-row\"]"));
        Assert.Contains(cut.FindAll("th"), th => th.TextContent.Contains("Name"));
    }
}
