using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.DataTable;

public class DataTableTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataTableTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Person(string Name, int Age, string City);

    private static readonly List<Person> SampleData = new()
    {
        new Person("Alice", 30, "New York"),
        new Person("Bob", 25, "Los Angeles"),
        new Person("Charlie", 35, "Chicago"),
    };

    private IRenderedComponent<IComponent> RenderDataTable(
        IEnumerable<Person>? items = null,
        bool isLoading = false,
        int skeletonRows = 3,
        RenderFragment? emptyTemplate = null)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.DataTable<Person>>(0);
            builder.AddAttribute(1, "Items", items);
            builder.AddAttribute(2, "IsLoading", isLoading);
            builder.AddAttribute(3, "SkeletonRows", skeletonRows);
            if (emptyTemplate != null)
                builder.AddAttribute(4, "EmptyTemplate", emptyTemplate);
            builder.AddAttribute(5, "HeaderTemplate", (RenderFragment)(header =>
            {
                header.OpenElement(0, "th");
                header.AddContent(1, "Name");
                header.CloseElement();
                header.OpenElement(2, "th");
                header.AddContent(3, "Age");
                header.CloseElement();
                header.OpenElement(4, "th");
                header.AddContent(5, "City");
                header.CloseElement();
            }));
            builder.AddAttribute(6, "RowTemplate", (RenderFragment<Person>)(item => rowBuilder =>
            {
                rowBuilder.OpenElement(0, "td");
                rowBuilder.AddContent(1, item.Name);
                rowBuilder.CloseElement();
                rowBuilder.OpenElement(2, "td");
                rowBuilder.AddContent(3, item.Age.ToString());
                rowBuilder.CloseElement();
                rowBuilder.OpenElement(4, "td");
                rowBuilder.AddContent(5, item.City);
                rowBuilder.CloseElement();
            }));
            builder.CloseComponent();
        });
    }

    // --- Rendering ---

    [Fact]
    public void DataTable_Renders_Table_Element()
    {
        var cut = RenderDataTable(items: SampleData);
        var table = cut.Find("table");
        Assert.NotNull(table);
    }

    [Fact]
    public void DataTable_Renders_Header_Columns()
    {
        var cut = RenderDataTable(items: SampleData);
        Assert.Contains("Name", cut.Markup);
        Assert.Contains("Age", cut.Markup);
        Assert.Contains("City", cut.Markup);
    }

    [Fact]
    public void DataTable_Renders_All_Rows()
    {
        var cut = RenderDataTable(items: SampleData);

        Assert.Contains("Alice", cut.Markup);
        Assert.Contains("Bob", cut.Markup);
        Assert.Contains("Charlie", cut.Markup);
    }

    [Fact]
    public void DataTable_Renders_Cell_Data()
    {
        var cut = RenderDataTable(items: SampleData);

        Assert.Contains("New York", cut.Markup);
        Assert.Contains("Los Angeles", cut.Markup);
        Assert.Contains("Chicago", cut.Markup);
    }

    // --- Empty State ---

    [Fact]
    public void DataTable_Shows_Default_Empty_Message_When_No_Items()
    {
        var cut = RenderDataTable(items: null);
        Assert.Contains("No results.", cut.Markup);
    }

    [Fact]
    public void DataTable_Shows_Default_Empty_Message_When_Empty_List()
    {
        var cut = RenderDataTable(items: new List<Person>());
        Assert.Contains("No results.", cut.Markup);
    }

    [Fact]
    public void DataTable_Shows_Custom_Empty_Template()
    {
        RenderFragment customEmpty = b => b.AddContent(0, "No data available right now");
        var cut = RenderDataTable(items: null, emptyTemplate: customEmpty);

        Assert.Contains("No data available right now", cut.Markup);
    }

    // --- Loading State ---

    [Fact]
    public void DataTable_Shows_Skeleton_Rows_When_Loading()
    {
        var cut = RenderDataTable(items: SampleData, isLoading: true, skeletonRows: 3);

        // Items should not be shown when loading
        Assert.DoesNotContain("Alice", cut.Markup);
        Assert.DoesNotContain("Bob", cut.Markup);

        // Should have skeleton rows
        var rows = cut.FindAll("tbody tr");
        Assert.Equal(3, rows.Count);
    }

    [Fact]
    public void DataTable_Skeleton_Rows_Count_Matches_SkeletonRows_Param()
    {
        var cut = RenderDataTable(items: null, isLoading: true, skeletonRows: 5);
        var rows = cut.FindAll("tbody tr");
        Assert.Equal(5, rows.Count);
    }

    [Fact]
    public void DataTable_Not_Loading_Shows_Items_Not_Skeleton()
    {
        var cut = RenderDataTable(items: SampleData, isLoading: false);

        Assert.Contains("Alice", cut.Markup);
        // Correct number of data rows (3)
        var rows = cut.FindAll("tbody tr");
        Assert.Equal(3, rows.Count);
    }

    // --- Table structure ---

    [Fact]
    public void DataTable_Has_Thead_And_Tbody()
    {
        var cut = RenderDataTable(items: SampleData);

        Assert.NotNull(cut.Find("thead"));
        Assert.NotNull(cut.Find("tbody"));
    }

    [Fact]
    public void DataTable_Header_Row_Rendered()
    {
        var cut = RenderDataTable(items: SampleData);
        var headerRow = cut.Find("thead tr");
        Assert.NotNull(headerRow);
    }

    // --- Custom CSS ---

    [Fact]
    public void Custom_Class_Forwarded_On_DataTable_Table()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.DataTable<Person>>(0);
            builder.AddAttribute(1, "Items", SampleData);
            builder.AddAttribute(2, "Class", "my-table-class");
            builder.AddAttribute(3, "HeaderTemplate", (RenderFragment)(h => h.AddContent(0, "")));
            builder.AddAttribute(4, "RowTemplate", (RenderFragment<Person>)(_ => _ => { }));
            builder.CloseComponent();
        });

        var table = cut.Find("table");
        Assert.Contains("my-table-class", table.GetAttribute("class"));
    }

    [Fact]
    public void DataTable_Has_Default_Classes()
    {
        var cut = RenderDataTable(items: SampleData);
        var table = cut.Find("table");
        Assert.Contains("w-full", table.GetAttribute("class"));
        Assert.Contains("text-sm", table.GetAttribute("class"));
    }

    // --- AdditionalAttributes ---

    [Fact]
    public void AdditionalAttributes_Forwarded_On_DataTable_Wrapper()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.DataTable<Person>>(0);
            builder.AddAttribute(1, "Items", SampleData);
            builder.AddAttribute(2, "data-testid", "my-table");
            builder.AddAttribute(3, "HeaderTemplate", (RenderFragment)(h => h.AddContent(0, "")));
            builder.AddAttribute(4, "RowTemplate", (RenderFragment<Person>)(_ => _ => { }));
            builder.CloseComponent();
        });

        // DataTable wraps table in a div - check the outer div
        var div = cut.Find("div");
        Assert.Equal("my-table", div.GetAttribute("data-testid"));
    }

    // --- Edge Cases ---

    [Fact]
    public void DataTable_With_Single_Item_Renders_One_Row()
    {
        var singleItem = new List<Person> { new Person("Diana", 28, "Seattle") };
        var cut = RenderDataTable(items: singleItem);

        Assert.Contains("Diana", cut.Markup);
        var rows = cut.FindAll("tbody tr");
        Assert.Equal(1, rows.Count);
    }

    [Fact]
    public void DataTable_Empty_Template_Spans_All_Columns()
    {
        var cut = RenderDataTable(items: null);

        var emptyTd = cut.Find("tbody tr td");
        Assert.Equal("100", emptyTd.GetAttribute("colspan"));
    }
}
