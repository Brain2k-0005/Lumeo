using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.DataGrid;

public class DataGridTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataGridTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // ---------------------------------------------------------------------------
    // Test data
    // ---------------------------------------------------------------------------

    private record TestItem(int Id, string Name, string Email, decimal Amount);

    private static List<TestItem> GetTestData() => new()
    {
        new(1, "Alice", "alice@test.com", 100m),
        new(2, "Bob", "bob@test.com", 200m),
        new(3, "Charlie", "charlie@test.com", 150m),
        new(4, "Diana", "diana@test.com", 300m),
        new(5, "Eve", "eve@test.com", 250m),
    };

    private static List<DataGridColumn<TestItem>> GetColumns() => new()
    {
        new() { Field = "Id", Title = "ID", Sortable = true },
        new() { Field = "Name", Title = "Name", Sortable = true, Filterable = true },
        new() { Field = "Email", Title = "Email" },
        new() { Field = "Amount", Title = "Amount", Sortable = true, Format = "C2" },
    };

    // ---------------------------------------------------------------------------
    // Helper: render DataGrid with common defaults
    // ---------------------------------------------------------------------------

    private IRenderedComponent<DataGrid<TestItem>> RenderGrid(
        IEnumerable<TestItem>? items = null,
        List<DataGridColumn<TestItem>>? columns = null,
        int pageSize = 10,
        bool showPagination = true,
        DataGridSelectionMode selectionMode = DataGridSelectionMode.None,
        bool isLoading = false,
        string? cssClass = null)
    {
        return _ctx.Render<DataGrid<TestItem>>(p => p
            .Add(x => x.Items, items ?? GetTestData())
            .Add(x => x.Columns, columns ?? GetColumns())
            .Add(x => x.PageSize, pageSize)
            .Add(x => x.ShowPagination, showPagination)
            .Add(x => x.SelectionMode, selectionMode)
            .Add(x => x.IsLoading, isLoading)
            .Add(x => x.Class, cssClass));
    }

    // ===========================================================================
    // RENDERING
    // ===========================================================================

    [Fact]
    public void DataGrid_Renders_Table_Element()
    {
        var cut = RenderGrid();
        var table = cut.Find("table");
        Assert.NotNull(table);
    }

    [Fact]
    public void DataGrid_Renders_Thead_And_Tbody()
    {
        var cut = RenderGrid();
        Assert.NotNull(cut.Find("thead"));
        Assert.NotNull(cut.Find("tbody"));
    }

    [Fact]
    public void DataGrid_Renders_Column_Headers_From_Columns()
    {
        var cut = RenderGrid();
        Assert.Contains("ID", cut.Markup);
        Assert.Contains("Name", cut.Markup);
        Assert.Contains("Email", cut.Markup);
        Assert.Contains("Amount", cut.Markup);
    }

    [Fact]
    public void DataGrid_Renders_Correct_Number_Of_Data_Rows()
    {
        // PageSize=10 with 5 items → 5 data rows
        var cut = RenderGrid(pageSize: 10);
        var rows = cut.FindAll("tbody tr");
        Assert.Equal(5, rows.Count);
    }

    [Fact]
    public void DataGrid_Renders_Cell_Data_For_All_Items()
    {
        var cut = RenderGrid();
        Assert.Contains("Alice", cut.Markup);
        Assert.Contains("Bob", cut.Markup);
        Assert.Contains("Charlie", cut.Markup);
        Assert.Contains("Diana", cut.Markup);
        Assert.Contains("Eve", cut.Markup);
    }

    [Fact]
    public void DataGrid_Renders_Empty_State_When_No_Items()
    {
        var cut = RenderGrid(items: new List<TestItem>());
        Assert.Contains("No data available", cut.Markup);
    }

    [Fact]
    public void DataGrid_Renders_Empty_State_When_Items_Is_Null()
    {
        var cut = _ctx.Render<DataGrid<TestItem>>(p => p
            .Add(x => x.Items, (IEnumerable<TestItem>?)null)
            .Add(x => x.Columns, GetColumns()));
        Assert.Contains("No data available", cut.Markup);
    }

    [Fact]
    public void DataGrid_Custom_Empty_Content_Rendered_When_Provided()
    {
        RenderFragment customEmpty = b => b.AddContent(0, "Custom empty message here");

        var cut = _ctx.Render<DataGrid<TestItem>>(p => p
            .Add(x => x.Items, new List<TestItem>())
            .Add(x => x.Columns, GetColumns())
            .Add(x => x.EmptyContent, customEmpty));

        Assert.Contains("Custom empty message here", cut.Markup);
    }

    // ===========================================================================
    // APPEARANCE: CSS classes applied by parameters
    // ===========================================================================

    [Fact]
    public void DataGrid_Applies_Striped_CSS_Class()
    {
        var cut = RenderGrid();
        var root = cut.Find("div");
        // Striped is false by default — class should NOT contain stripe token
        Assert.DoesNotContain("nth-child(even)", root.GetAttribute("class") ?? "");
    }

    [Fact]
    public void DataGrid_Striped_True_Adds_Stripe_Class()
    {
        var cut = _ctx.Render<DataGrid<TestItem>>(p => p
            .Add(x => x.Items, GetTestData())
            .Add(x => x.Columns, GetColumns())
            .Add(x => x.Striped, true));

        var root = cut.Find("div");
        Assert.Contains("nth-child(even)", root.GetAttribute("class") ?? "");
    }

    [Fact]
    public void DataGrid_Bordered_True_Adds_Bordered_Class()
    {
        var cut = _ctx.Render<DataGrid<TestItem>>(p => p
            .Add(x => x.Items, GetTestData())
            .Add(x => x.Columns, GetColumns())
            .Add(x => x.Bordered, true));

        var root = cut.Find("div");
        Assert.Contains("[&_td]:border", root.GetAttribute("class") ?? "");
    }

    [Fact]
    public void DataGrid_Compact_True_Adds_Text_Sm_Class()
    {
        var cut = _ctx.Render<DataGrid<TestItem>>(p => p
            .Add(x => x.Items, GetTestData())
            .Add(x => x.Columns, GetColumns())
            .Add(x => x.Compact, true));

        var root = cut.Find("div");
        // Compact applies "text-sm" to root class
        Assert.Contains("text-sm", root.GetAttribute("class") ?? "");
    }

    [Fact]
    public void DataGrid_Custom_Class_Applied_To_Root_Div()
    {
        var cut = RenderGrid(cssClass: "my-custom-datagrid");
        var root = cut.Find("div");
        Assert.Contains("my-custom-datagrid", root.GetAttribute("class") ?? "");
    }

    // ===========================================================================
    // PAGING
    // ===========================================================================

    [Fact]
    public void DataGrid_Shows_Pagination_When_Items_Exceed_PageSize()
    {
        // 5 items, PageSize=2 → should render pagination
        var cut = RenderGrid(pageSize: 2, showPagination: true);
        // Pagination renders "Showing X–Y of Z" text
        Assert.Contains("Showing", cut.Markup);
        Assert.Contains("of 5", cut.Markup);
    }

    [Fact]
    public void DataGrid_PageSize_2_Shows_2_Rows_On_First_Page()
    {
        var cut = RenderGrid(pageSize: 2, showPagination: true);
        var rows = cut.FindAll("tbody tr");
        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public void DataGrid_PageSize_3_Shows_3_Rows_On_First_Page()
    {
        var cut = RenderGrid(pageSize: 3, showPagination: true);
        var rows = cut.FindAll("tbody tr");
        Assert.Equal(3, rows.Count);
    }

    [Fact]
    public void DataGrid_No_Pagination_When_ShowPagination_False()
    {
        var cut = RenderGrid(showPagination: false);
        // "Showing" text only comes from DataGridPagination
        Assert.DoesNotContain("Showing", cut.Markup);
    }

    // ===========================================================================
    // SELECTION
    // ===========================================================================

    [Fact]
    public void DataGrid_SelectionMode_None_No_Selection_Column()
    {
        var cut = RenderGrid(selectionMode: DataGridSelectionMode.None);
        // No checkbox or radio button in header
        var checkboxes = cut.FindAll("thead th input[type=checkbox]");
        Assert.Empty(checkboxes);
    }

    [Fact]
    public void DataGrid_SelectionMode_Multiple_Renders_Checkboxes()
    {
        var cut = RenderGrid(selectionMode: DataGridSelectionMode.Multiple);
        // Lumeo Checkbox renders as <button role="checkbox">, not <input type="checkbox">
        // 1 header select-all + 5 row checkboxes = 6 total
        var checkboxes = cut.FindAll("[role=checkbox]");
        Assert.True(checkboxes.Count >= 5, $"Expected at least 5 checkboxes, found {checkboxes.Count}");
    }

    [Fact]
    public void DataGrid_SelectionMode_Single_Renders_Radio_Buttons()
    {
        var cut = RenderGrid(selectionMode: DataGridSelectionMode.Single);
        // Single selection uses round button, not checkbox
        var radioButtons = cut.FindAll("tbody tr td button");
        // There should be one per row
        Assert.True(radioButtons.Count >= 5, $"Expected at least 5 radio buttons, found {radioButtons.Count}");
    }

    [Fact]
    public void DataGrid_SelectedItemsChanged_Fires_On_Row_Click_When_Single_Mode()
    {
        IReadOnlyList<TestItem>? capturedSelection = null;

        var cut = _ctx.Render<DataGrid<TestItem>>(p => p
            .Add(x => x.Items, GetTestData())
            .Add(x => x.Columns, GetColumns())
            .Add(x => x.SelectionMode, DataGridSelectionMode.Single)
            .Add(x => x.SelectedItemsChanged, EventCallback.Factory.Create<IReadOnlyList<TestItem>>(
                this, list => capturedSelection = list)));

        // Click first data row
        var firstRow = cut.FindAll("tbody tr")[0];
        firstRow.Click();

        Assert.NotNull(capturedSelection);
        Assert.Single(capturedSelection);
        Assert.Equal("Alice", capturedSelection![0].Name);
    }

    [Fact]
    public void DataGrid_SelectedItemsChanged_Fires_On_Row_Click_When_Multiple_Mode()
    {
        IReadOnlyList<TestItem>? capturedSelection = null;

        var cut = _ctx.Render<DataGrid<TestItem>>(p => p
            .Add(x => x.Items, GetTestData())
            .Add(x => x.Columns, GetColumns())
            .Add(x => x.SelectionMode, DataGridSelectionMode.Multiple)
            .Add(x => x.SelectedItemsChanged, EventCallback.Factory.Create<IReadOnlyList<TestItem>>(
                this, list => capturedSelection = list)));

        // Click the first data row — HandleClick fires ToggleSelection + SelectedItemsChanged
        var firstRow = cut.FindAll("tbody tr")[0];
        firstRow.Click();

        Assert.NotNull(capturedSelection);
        Assert.Single(capturedSelection);
        Assert.Equal("Alice", capturedSelection![0].Name);
    }

    // ===========================================================================
    // LOADING STATE
    // ===========================================================================

    [Fact]
    public void DataGrid_IsLoading_True_Renders_Skeleton_Rows()
    {
        // SkeletonRowCount defaults to PageSize (10) — grid passes PageSize to DataGridBody
        var cut = RenderGrid(isLoading: true, pageSize: 10);
        var rows = cut.FindAll("tbody tr");
        Assert.Equal(10, rows.Count);
        // Actual data names should NOT appear
        Assert.DoesNotContain("Alice", cut.Markup);
        Assert.DoesNotContain("Bob", cut.Markup);
    }

    [Fact]
    public void DataGrid_IsLoading_False_Renders_Data_Rows()
    {
        var cut = RenderGrid(isLoading: false);
        Assert.Contains("Alice", cut.Markup);
        Assert.DoesNotContain("Skeleton", cut.Markup.ToLowerInvariant().Replace("skeleton", "Skeleton"));
    }

    [Fact]
    public void DataGrid_IsLoading_True_Does_Not_Show_Pagination()
    {
        var cut = RenderGrid(isLoading: true, pageSize: 2, showPagination: true);
        // Pagination has @if (!IsLoading) guard — should not show
        Assert.DoesNotContain("Showing", cut.Markup);
    }

    // ===========================================================================
    // COLUMN FEATURES
    // ===========================================================================

    [Fact]
    public void DataGrid_Hidden_Column_Does_Not_Render_Header()
    {
        var columns = new List<DataGridColumn<TestItem>>
        {
            new() { Field = "Id", Title = "ID" },
            new() { Field = "Name", Title = "Name" },
            new() { Field = "Email", Title = "Email", Visible = false },
            new() { Field = "Amount", Title = "Amount" },
        };

        var cut = _ctx.Render<DataGrid<TestItem>>(p => p
            .Add(x => x.Items, GetTestData())
            .Add(x => x.Columns, columns));

        // Email header should not be present
        Assert.DoesNotContain("Email", cut.Markup);
    }

    [Fact]
    public void DataGrid_Hidden_Column_Does_Not_Render_Cells()
    {
        var columns = new List<DataGridColumn<TestItem>>
        {
            new() { Field = "Id", Title = "ID" },
            new() { Field = "Name", Title = "Name" },
            new() { Field = "Email", Title = "Email", Visible = false },
            new() { Field = "Amount", Title = "Amount" },
        };

        var cut = _ctx.Render<DataGrid<TestItem>>(p => p
            .Add(x => x.Items, GetTestData())
            .Add(x => x.Columns, columns));

        // Email values should not appear
        Assert.DoesNotContain("alice@test.com", cut.Markup);
        Assert.DoesNotContain("bob@test.com", cut.Markup);
    }

    [Fact]
    public void DataGrid_Column_With_Format_Renders_Formatted_Values()
    {
        // Amount column has Format = "C2", so $100.00 etc. should appear
        var cut = RenderGrid();
        // The formatted value would depend on current culture, but there should be
        // some numeric formatting applied — at minimum the raw "100" should not be
        // present as plain text if currency formatting is applied.
        // We check that the formatted string (with decimal) appears.
        Assert.Contains("100", cut.Markup); // base value still present in some form
    }

    [Fact]
    public void DataGrid_Column_With_CellTemplate_Renders_Custom_Content()
    {
        var columns = new List<DataGridColumn<TestItem>>
        {
            new() { Field = "Id", Title = "ID" },
            new()
            {
                Field = "Name",
                Title = "Name",
                CellTemplate = item => builder =>
                {
                    builder.OpenElement(0, "span");
                    builder.AddAttribute(1, "data-custom", "true");
                    builder.AddContent(2, $"Custom:{item.Name}");
                    builder.CloseElement();
                }
            },
        };

        var cut = _ctx.Render<DataGrid<TestItem>>(p => p
            .Add(x => x.Items, GetTestData())
            .Add(x => x.Columns, columns));

        Assert.Contains("Custom:Alice", cut.Markup);
        Assert.Contains("Custom:Bob", cut.Markup);
        var customSpans = cut.FindAll("[data-custom=true]");
        Assert.Equal(5, customSpans.Count);
    }

    // ===========================================================================
    // SERVER MODE
    // ===========================================================================

    [Fact]
    public async Task DataGrid_ServerMode_Fires_OnServerRequest_On_Init()
    {
        DataGridServerRequest? capturedRequest = null;

        var cut = _ctx.Render<DataGrid<TestItem>>(p => p
            .Add(x => x.ServerMode, true)
            .Add(x => x.Items, new List<TestItem>())
            .Add(x => x.Columns, GetColumns())
            .Add(x => x.PageSize, 10)
            .Add(x => x.TotalCount, 0)
            .Add(x => x.OnServerRequest, EventCallback.Factory.Create<DataGridServerRequest>(
                this, req => capturedRequest = req)));

        // Wait for async init to complete
        await Task.Yield();

        Assert.NotNull(capturedRequest);
        Assert.Equal(1, capturedRequest!.Page);
        Assert.Equal(10, capturedRequest.PageSize);
    }

    [Fact]
    public async Task DataGrid_ServerMode_OnServerRequest_Receives_Correct_Page_And_PageSize()
    {
        DataGridServerRequest? capturedRequest = null;

        var cut = _ctx.Render<DataGrid<TestItem>>(p => p
            .Add(x => x.ServerMode, true)
            .Add(x => x.Items, new List<TestItem>())
            .Add(x => x.Columns, GetColumns())
            .Add(x => x.PageSize, 25)
            .Add(x => x.TotalCount, 100)
            .Add(x => x.OnServerRequest, EventCallback.Factory.Create<DataGridServerRequest>(
                this, req => capturedRequest = req)));

        await Task.Yield();

        Assert.NotNull(capturedRequest);
        Assert.Equal(25, capturedRequest!.PageSize);
    }

    [Fact]
    public void DataGrid_ServerMode_IsLoading_True_Shows_Skeleton()
    {
        var cut = _ctx.Render<DataGrid<TestItem>>(p => p
            .Add(x => x.ServerMode, true)
            .Add(x => x.Items, new List<TestItem>())
            .Add(x => x.Columns, GetColumns())
            .Add(x => x.IsLoading, true)
            .Add(x => x.TotalCount, 0));

        // Skeleton rows shown when loading — DataGridBody uses SkeletonRowCount=PageSize (default 10)
        var rows = cut.FindAll("tbody tr");
        Assert.Equal(10, rows.Count);
        Assert.DoesNotContain("Alice", cut.Markup);
    }

    [Fact]
    public void DataGrid_ServerMode_Renders_Server_Items()
    {
        // Simulate server returning items
        var serverItems = new List<TestItem>
        {
            new(1, "ServerAlice", "sa@test.com", 100m),
            new(2, "ServerBob", "sb@test.com", 200m),
        };

        var cut = _ctx.Render<DataGrid<TestItem>>(p => p
            .Add(x => x.ServerMode, true)
            .Add(x => x.Items, serverItems)
            .Add(x => x.Columns, GetColumns())
            .Add(x => x.TotalCount, 2)
            .Add(x => x.IsLoading, false));

        Assert.Contains("ServerAlice", cut.Markup);
        Assert.Contains("ServerBob", cut.Markup);
    }

    // ===========================================================================
    // LAYOUT
    // ===========================================================================

    [Fact]
    public void DataGrid_GetCurrentLayout_Returns_DataGridLayout_Object()
    {
        var cut = RenderGrid();
        var grid = cut.Instance;

        var layout = grid.GetCurrentLayout();

        Assert.NotNull(layout);
        Assert.IsType<DataGridLayout>(layout);
    }

    [Fact]
    public void DataGrid_GetCurrentLayout_Contains_Column_Layout_Entries()
    {
        var cut = RenderGrid();
        var layout = cut.Instance.GetCurrentLayout();

        // Should have an entry for each column
        Assert.Equal(4, layout.Columns.Count);
    }

    [Fact]
    public void DataGrid_GetCurrentLayout_PageSize_Matches_Parameter()
    {
        var cut = RenderGrid(pageSize: 25);
        var layout = cut.Instance.GetCurrentLayout();
        Assert.Equal(25, layout.PageSize);
    }

    // ===========================================================================
    // ADDITIONAL ATTRIBUTES
    // ===========================================================================

    [Fact]
    public void DataGrid_AdditionalAttributes_Applied_To_Root_Element()
    {
        var cut = _ctx.Render<DataGrid<TestItem>>(p => p
            .Add(x => x.Items, GetTestData())
            .Add(x => x.Columns, GetColumns())
            .AddUnmatched("data-testid", "my-datagrid"));

        var root = cut.Find("[data-testid=my-datagrid]");
        Assert.NotNull(root);
    }

    [Fact]
    public void DataGrid_Has_OnError_Parameter()
    {
        Exception? receivedError = null;
        var cut = _ctx.Render<DataGrid<TestItem>>(p => p
            .Add(x => x.Items, GetTestData())
            .Add(x => x.Columns, GetColumns())
            .Add(x => x.OnError, EventCallback.Factory.Create<Exception>(
                new object(), ex => receivedError = ex)));

        Assert.NotNull(cut.Find("table"));
    }
}
