using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// Tests for <see cref="DataGridColumnGroup{TItem}"/> — the labelled parent header that
/// spans multiple data columns via colspan, plus the new <c>FooterFormat</c> column
/// parameter that lets the totals row format independently from the cell.
/// </summary>
public class DataGridColumnGroupTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataGridColumnGroupTests() => _ctx.AddLumeoServices();

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Sale(string Product, decimal Revenue, decimal Cost);

    private static IEnumerable<Sale> Sales => new[]
    {
        new Sale("A", 100m, 40m),
        new Sale("B", 200m, 80m),
        new Sale("C", 300m, 90m),
    };

    // Renders a DataGrid with two column groups (Sales: Revenue+Cost, Meta: Product).
    private IRenderedComponent<IComponent> RenderGrouped()
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<DataGrid<Sale>>(0);
            builder.AddAttribute(1, "Items", Sales);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                // Meta group with just Product
                b.OpenComponent<DataGridColumnGroup<Sale>>(0);
                b.AddAttribute(1, "Label", "Meta");
                b.AddAttribute(2, "ChildContent", (RenderFragment)(g =>
                {
                    g.OpenComponent<DataGridColumnDef<Sale>>(0);
                    g.AddAttribute(1, "Title", "Product");
                    g.AddAttribute(2, "Field", "Product");
                    g.CloseComponent();
                }));
                b.CloseComponent();

                // Pricing group spans Revenue + Cost
                b.OpenComponent<DataGridColumnGroup<Sale>>(3);
                b.AddAttribute(1, "Label", "Pricing");
                b.AddAttribute(2, "ChildContent", (RenderFragment)(g =>
                {
                    g.OpenComponent<DataGridColumnDef<Sale>>(0);
                    g.AddAttribute(1, "Title", "Revenue");
                    g.AddAttribute(2, "Field", "Revenue");
                    g.CloseComponent();
                    g.OpenComponent<DataGridColumnDef<Sale>>(3);
                    g.AddAttribute(1, "Title", "Cost");
                    g.AddAttribute(2, "Field", "Cost");
                    g.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    [Fact]
    public void Renders_Parent_Header_Row_When_Column_Groups_Declared()
    {
        var cut = RenderGrouped();

        // <thead> contains two <tr> when groups exist: the parent row + the data row.
        var thead = cut.Find("thead");
        var headerRows = thead.QuerySelectorAll("tr");
        Assert.Equal(2, headerRows.Length);
    }

    [Fact]
    public void Group_Label_Appears_In_Parent_Header()
    {
        var cut = RenderGrouped();

        Assert.Contains("Pricing", cut.Markup);
        Assert.Contains("Meta", cut.Markup);
    }

    [Fact]
    public void Group_Header_Spans_Member_Columns_Via_Colspan()
    {
        var cut = RenderGrouped();

        // Find the parent row (first <tr> inside <thead>).
        var parentRow = cut.Find("thead tr");
        // Pricing group spans 2 columns (Revenue + Cost) → colspan="2".
        var pricingTh = parentRow.QuerySelectorAll("th").First(t => t.TextContent.Contains("Pricing"));
        Assert.Equal("2", pricingTh.GetAttribute("colspan"));
    }

    [Fact]
    public void Ungrouped_Columns_Get_Blank_Parent_Cells()
    {
        // Column outside any group → blank <th> in the parent row keeps alignment.
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<DataGrid<Sale>>(0);
            builder.AddAttribute(1, "Items", Sales);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                // Ungrouped column first
                b.OpenComponent<DataGridColumnDef<Sale>>(0);
                b.AddAttribute(1, "Title", "Product");
                b.AddAttribute(2, "Field", "Product");
                b.CloseComponent();

                b.OpenComponent<DataGridColumnGroup<Sale>>(3);
                b.AddAttribute(1, "Label", "Pricing");
                b.AddAttribute(2, "ChildContent", (RenderFragment)(g =>
                {
                    g.OpenComponent<DataGridColumnDef<Sale>>(0);
                    g.AddAttribute(1, "Title", "Revenue");
                    g.AddAttribute(2, "Field", "Revenue");
                    g.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        // Two rows in the header now. The parent row has 2 ths: one blank + Pricing.
        var thead = cut.Find("thead");
        Assert.Equal(2, thead.QuerySelectorAll("tr").Length);
        var parentTHs = thead.QuerySelectorAll("tr").First().QuerySelectorAll("th");
        Assert.Equal(2, parentTHs.Length);
        Assert.Contains(parentTHs, th => th.TextContent.Contains("Pricing"));
        // The other one is empty
        Assert.Contains(parentTHs, th => string.IsNullOrWhiteSpace(th.TextContent));
    }

    [Fact]
    public void No_Parent_Row_When_No_Column_Groups()
    {
        // Plain DataGrid without any DataGridColumnGroup keeps the single header row
        // (no behavioural change for existing consumers).
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<DataGrid<Sale>>(0);
            builder.AddAttribute(1, "Items", Sales);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<DataGridColumnDef<Sale>>(0);
                b.AddAttribute(1, "Title", "Product");
                b.AddAttribute(2, "Field", "Product");
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.Single(cut.Find("thead").QuerySelectorAll("tr"));
    }

    // --- FooterFormat ---

    [Fact]
    public void Footer_Uses_FooterFormat_When_Set()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<DataGrid<Sale>>(0);
            builder.AddAttribute(1, "Items", Sales);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<DataGridColumnDef<Sale>>(0);
                b.AddAttribute(1, "Title", "Revenue");
                b.AddAttribute(2, "Field", "Revenue");
                b.AddAttribute(3, "Aggregate", AggregateType.Sum);
                b.AddAttribute(4, "Format", "C2");          // cells: currency w/ 2 decimals
                b.AddAttribute(5, "FooterFormat", "N0");    // totals: plain integer
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        // 100 + 200 + 300 = 600, formatted as N0 → "600" (no decimals).
        var footer = cut.Find("[data-slot='datagrid-aggregate-strip']");
        Assert.Contains("600", footer.TextContent);
    }

    [Fact]
    public void Footer_Falls_Back_To_Column_Format_When_FooterFormat_Unset()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<DataGrid<Sale>>(0);
            builder.AddAttribute(1, "Items", Sales);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<DataGridColumnDef<Sale>>(0);
                b.AddAttribute(1, "Title", "Revenue");
                b.AddAttribute(2, "Field", "Revenue");
                b.AddAttribute(3, "Aggregate", AggregateType.Sum);
                b.AddAttribute(4, "Format", "N0"); // cell + totals both N0
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var footer = cut.Find("[data-slot='datagrid-aggregate-strip']");
        Assert.Contains("600", footer.TextContent);
        Assert.DoesNotContain("600.00", footer.TextContent);
    }
}
