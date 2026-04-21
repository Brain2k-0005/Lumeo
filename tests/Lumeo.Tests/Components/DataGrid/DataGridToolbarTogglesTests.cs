using AngleSharp.Dom;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// Tests for the DataGrid toolbar visibility toggles (<see cref="DataGrid{TItem}.ShowSearch"/>,
/// <see cref="DataGrid{TItem}.ShowColumnChooser"/>, <see cref="DataGrid{TItem}.ShowExport"/>) and
/// the per-format export flags enum (<see cref="DataGrid{TItem}.ExportFormats"/>).
///
/// These were added so WASM consumers can hide the PDF export item (which requires server-only
/// libraries like QuestPDF) without disabling the whole toolbar.
/// </summary>
public class DataGridToolbarTogglesTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataGridToolbarTogglesTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record TestItem(int Id, string Name);

    private static List<TestItem> GetData() => new()
    {
        new(1, "Alice"),
        new(2, "Bob"),
    };

    private static List<DataGridColumn<TestItem>> GetColumns() => new()
    {
        new() { Field = "Id", Title = "ID", Sortable = true },
        new() { Field = "Name", Title = "Name", Sortable = true },
    };

    private IRenderedComponent<DataGrid<TestItem>> RenderToolbar(
        bool? showSearch = null,
        bool? showColumnChooser = null,
        bool? showExport = null,
        DataGridExportFormat? exportFormats = null,
        bool expandable = false)
    {
        return _ctx.Render<DataGrid<TestItem>>(p =>
        {
            p.Add(x => x.Items, GetData());
            p.Add(x => x.Columns, GetColumns());
            p.Add(x => x.ShowToolbar, true);
            p.Add(x => x.Expandable, expandable);
            if (showSearch.HasValue) p.Add(x => x.ShowSearch, showSearch.Value);
            if (showColumnChooser.HasValue) p.Add(x => x.ShowColumnChooser, showColumnChooser.Value);
            if (showExport.HasValue) p.Add(x => x.ShowExport, showExport.Value);
            if (exportFormats.HasValue) p.Add(x => x.ExportFormats, exportFormats.Value);
        });
    }

    private static bool HasSearchInput(IRenderedComponent<DataGrid<TestItem>> cut) =>
        cut.FindAll("input[placeholder]").Any(i =>
            (i.GetAttribute("placeholder") ?? "").Contains("Search", StringComparison.OrdinalIgnoreCase));

    private static bool HasColumnsButton(IRenderedComponent<DataGrid<TestItem>> cut) =>
        cut.FindAll("button").Any(b => b.TextContent.Contains("Columns"));

    private static IElement? FindExportButton(IRenderedComponent<DataGrid<TestItem>> cut) =>
        cut.FindAll("button").FirstOrDefault(b =>
            b.TextContent.Contains("Export") && !b.HasAttribute("data-export-format"));

    private static bool HasExportButton(IRenderedComponent<DataGrid<TestItem>> cut) =>
        FindExportButton(cut) is not null;

    private static List<IElement> ExportMenuItems(IRenderedComponent<DataGrid<TestItem>> cut) =>
        cut.FindAll("button[data-export-format]").ToList();

    // -----------------------------------------------------------------------
    // 1. Default — all four toolbar affordances render when toolbar is enabled
    // -----------------------------------------------------------------------
    [Fact]
    public void Defaults_All_Toolbar_Buttons_Render()
    {
        var cut = RenderToolbar(expandable: true);

        Assert.True(HasSearchInput(cut));
        Assert.True(HasColumnsButton(cut));
        Assert.True(HasExportButton(cut));
        // Expand toggle is controlled separately by Expandable — verify aria-label is present.
        Assert.Contains("Expand to fullscreen", cut.Markup);
    }

    // -----------------------------------------------------------------------
    // 2. ShowSearch=false hides only the search input
    // -----------------------------------------------------------------------
    [Fact]
    public void ShowSearch_False_Hides_Search_Input_Only()
    {
        var cut = RenderToolbar(showSearch: false);

        Assert.False(HasSearchInput(cut));
        Assert.True(HasColumnsButton(cut));
        Assert.True(HasExportButton(cut));
    }

    // -----------------------------------------------------------------------
    // 3. ShowColumnChooser=false hides only the Columns button
    // -----------------------------------------------------------------------
    [Fact]
    public void ShowColumnChooser_False_Hides_Columns_Button_Only()
    {
        var cut = RenderToolbar(showColumnChooser: false);

        Assert.True(HasSearchInput(cut));
        Assert.False(HasColumnsButton(cut));
        Assert.True(HasExportButton(cut));
    }

    // -----------------------------------------------------------------------
    // 4. ShowExport=false hides the Export button entirely
    // -----------------------------------------------------------------------
    [Fact]
    public void ShowExport_False_Hides_Export_Button()
    {
        var cut = RenderToolbar(showExport: false);

        Assert.True(HasSearchInput(cut));
        Assert.True(HasColumnsButton(cut));
        Assert.False(HasExportButton(cut));
    }

    // -----------------------------------------------------------------------
    // 5. ExportFormats=Csv — button visible, only CSV menu item rendered
    // -----------------------------------------------------------------------
    [Fact]
    public void ExportFormats_Csv_Only_Renders_Csv_Menu_Item()
    {
        var cut = RenderToolbar(exportFormats: DataGridExportFormat.Csv);

        var exportBtn = FindExportButton(cut);
        Assert.NotNull(exportBtn);

        // Open the dropdown to render the menu items
        exportBtn!.Click();

        var items = ExportMenuItems(cut);
        Assert.Single(items);
        Assert.Equal("csv", items[0].GetAttribute("data-export-format"));
    }

    // -----------------------------------------------------------------------
    // 6. ExportFormats=Csv|Excel — two menu items, no PDF
    // -----------------------------------------------------------------------
    [Fact]
    public void ExportFormats_CsvExcel_Renders_Two_Items_Without_Pdf()
    {
        var cut = RenderToolbar(exportFormats: DataGridExportFormat.Csv | DataGridExportFormat.Excel);

        FindExportButton(cut)!.Click();

        var formats = ExportMenuItems(cut)
            .Select(i => i.GetAttribute("data-export-format"))
            .ToList();

        Assert.Equal(2, formats.Count);
        Assert.Contains("csv", formats);
        Assert.Contains("excel", formats);
        Assert.DoesNotContain("pdf", formats);
    }

    // -----------------------------------------------------------------------
    // 7. ExportFormats=None — Export button hidden (same effect as ShowExport=false)
    //    This is the edge case: ShowExport stays at its default (true) but
    //    the flags enum is empty, so rendering suppresses the button.
    // -----------------------------------------------------------------------
    [Fact]
    public void ExportFormats_None_Hides_Export_Button()
    {
        var cut = RenderToolbar(exportFormats: DataGridExportFormat.None);

        Assert.False(HasExportButton(cut));
    }

    // -----------------------------------------------------------------------
    // 8. ExportFormats=All (default) renders all three menu items
    // -----------------------------------------------------------------------
    [Fact]
    public void ExportFormats_All_Default_Renders_Csv_Excel_Pdf()
    {
        var cut = RenderToolbar(); // defaults: ExportFormats = All

        FindExportButton(cut)!.Click();

        var formats = ExportMenuItems(cut)
            .Select(i => i.GetAttribute("data-export-format"))
            .ToList();

        Assert.Equal(3, formats.Count);
        Assert.Contains("csv", formats);
        Assert.Contains("excel", formats);
        Assert.Contains("pdf", formats);
    }
}
