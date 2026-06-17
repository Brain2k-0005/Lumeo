using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Table;

public class TableTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TableTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // --- Table ---

    [Fact]
    public void Table_Renders_Table_Element()
    {
        var cut = _ctx.Render<L.Table>(p => p.AddChildContent("content"));
        Assert.NotNull(cut.Find("table"));
    }

    [Fact]
    public void Table_Renders_ChildContent()
    {
        var cut = _ctx.Render<L.Table>(p => p.AddChildContent("hello"));
        Assert.Contains("hello", cut.Markup);
    }

    [Fact]
    public void Table_Has_Default_Classes()
    {
        var cut = _ctx.Render<L.Table>(p => p.AddChildContent(""));
        var cls = cut.Find("table").GetAttribute("class") ?? "";
        Assert.Contains("w-full", cls);
        Assert.Contains("caption-bottom", cls);
        Assert.Contains("text-sm", cls);
    }

    [Fact]
    public void Table_Custom_Class_Appended()
    {
        var cut = _ctx.Render<L.Table>(p => p
            .Add(c => c.Class, "my-table")
            .AddChildContent(""));
        var cls = cut.Find("table").GetAttribute("class") ?? "";
        Assert.Contains("my-table", cls);
        Assert.Contains("w-full", cls);
    }

    [Fact]
    public void Table_Additional_Attributes_Forwarded()
    {
        var cut = _ctx.Render<L.Table>(p => p
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object> { ["data-testid"] = "my-table" })
            .AddChildContent(""));
        Assert.Equal("my-table", cut.Find("table").GetAttribute("data-testid"));
    }

    [Fact]
    public void Table_Wrapped_In_Overflow_Div()
    {
        var cut = _ctx.Render<L.Table>(p => p.AddChildContent(""));
        var wrapper = cut.Find("div");
        Assert.Contains("overflow-auto", wrapper.GetAttribute("class") ?? "");
    }

    // --- TableHeader ---

    [Fact]
    public void TableHeader_Renders_Thead_Element()
    {
        var cut = _ctx.Render<L.TableHeader>(p => p.AddChildContent(""));
        Assert.NotNull(cut.Find("thead"));
    }

    [Fact]
    public void TableHeader_Renders_ChildContent()
    {
        var cut = _ctx.Render<L.TableHeader>(p => p.AddChildContent("header content"));
        Assert.Contains("header content", cut.Markup);
    }

    [Fact]
    public void TableHeader_Custom_Class_Appended()
    {
        var cut = _ctx.Render<L.TableHeader>(p => p
            .Add(c => c.Class, "my-header")
            .AddChildContent(""));
        var cls = cut.Find("thead").GetAttribute("class") ?? "";
        Assert.Contains("my-header", cls);
    }

    [Fact]
    public void TableHeader_Additional_Attributes_Forwarded()
    {
        var cut = _ctx.Render<L.TableHeader>(p => p
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object> { ["data-testid"] = "my-thead" })
            .AddChildContent(""));
        Assert.Equal("my-thead", cut.Find("thead").GetAttribute("data-testid"));
    }

    // --- TableBody ---

    [Fact]
    public void TableBody_Renders_Tbody_Element()
    {
        var cut = _ctx.Render<L.TableBody>(p => p.AddChildContent(""));
        Assert.NotNull(cut.Find("tbody"));
    }

    [Fact]
    public void TableBody_Renders_ChildContent()
    {
        var cut = _ctx.Render<L.TableBody>(p => p.AddChildContent("body content"));
        Assert.Contains("body content", cut.Markup);
    }

    [Fact]
    public void TableBody_Custom_Class_Appended()
    {
        var cut = _ctx.Render<L.TableBody>(p => p
            .Add(c => c.Class, "my-body")
            .AddChildContent(""));
        var cls = cut.Find("tbody").GetAttribute("class") ?? "";
        Assert.Contains("my-body", cls);
    }

    [Fact]
    public void TableBody_Additional_Attributes_Forwarded()
    {
        var cut = _ctx.Render<L.TableBody>(p => p
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object> { ["data-testid"] = "my-tbody" })
            .AddChildContent(""));
        Assert.Equal("my-tbody", cut.Find("tbody").GetAttribute("data-testid"));
    }

    // --- TableRow ---

    [Fact]
    public void TableRow_Renders_Tr_Element()
    {
        var cut = _ctx.Render<L.TableRow>(p => p.AddChildContent(""));
        Assert.NotNull(cut.Find("tr"));
    }

    [Fact]
    public void TableRow_Renders_ChildContent()
    {
        var cut = _ctx.Render<L.TableRow>(p => p.AddChildContent("row content"));
        Assert.Contains("row content", cut.Markup);
    }

    [Fact]
    public void TableRow_Has_Default_Classes()
    {
        var cut = _ctx.Render<L.TableRow>(p => p.AddChildContent(""));
        var cls = cut.Find("tr").GetAttribute("class") ?? "";
        Assert.Contains("border-b", cls);
        Assert.Contains("transition-colors", cls);
    }

    [Fact]
    public void TableRow_Custom_Class_Appended()
    {
        var cut = _ctx.Render<L.TableRow>(p => p
            .Add(c => c.Class, "my-row")
            .AddChildContent(""));
        var cls = cut.Find("tr").GetAttribute("class") ?? "";
        Assert.Contains("my-row", cls);
    }

    [Fact]
    public void TableRow_Additional_Attributes_Forwarded()
    {
        var cut = _ctx.Render<L.TableRow>(p => p
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object> { ["data-testid"] = "my-tr" })
            .AddChildContent(""));
        Assert.Equal("my-tr", cut.Find("tr").GetAttribute("data-testid"));
    }

    // --- TableHead ---

    [Fact]
    public void TableHead_Renders_Th_Element()
    {
        var cut = _ctx.Render<L.TableHead>(p => p.AddChildContent(""));
        Assert.NotNull(cut.Find("th"));
    }

    [Fact]
    public void TableHead_Renders_ChildContent()
    {
        var cut = _ctx.Render<L.TableHead>(p => p.AddChildContent("Name"));
        Assert.Contains("Name", cut.Markup);
    }

    [Fact]
    public void TableHead_Has_Default_Classes()
    {
        var cut = _ctx.Render<L.TableHead>(p => p.AddChildContent(""));
        var cls = cut.Find("th").GetAttribute("class") ?? "";
        Assert.Contains("h-10", cls);
        Assert.Contains("px-2", cls);
        Assert.Contains("font-medium", cls);
    }

    [Fact]
    public void TableHead_Custom_Class_Appended()
    {
        var cut = _ctx.Render<L.TableHead>(p => p
            .Add(c => c.Class, "my-head")
            .AddChildContent(""));
        var cls = cut.Find("th").GetAttribute("class") ?? "";
        Assert.Contains("my-head", cls);
    }

    [Fact]
    public void TableHead_Additional_Attributes_Forwarded()
    {
        var cut = _ctx.Render<L.TableHead>(p => p
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object> { ["data-testid"] = "my-th" })
            .AddChildContent(""));
        Assert.Equal("my-th", cut.Find("th").GetAttribute("data-testid"));
    }

    // --- TableCell ---

    [Fact]
    public void TableCell_Renders_Td_Element()
    {
        var cut = _ctx.Render<L.TableCell>(p => p.AddChildContent(""));
        Assert.NotNull(cut.Find("td"));
    }

    [Fact]
    public void TableCell_Renders_ChildContent()
    {
        var cut = _ctx.Render<L.TableCell>(p => p.AddChildContent("Cell data"));
        Assert.Contains("Cell data", cut.Markup);
    }

    [Fact]
    public void TableCell_Has_Default_Classes()
    {
        var cut = _ctx.Render<L.TableCell>(p => p.AddChildContent(""));
        var cls = cut.Find("td").GetAttribute("class") ?? "";
        Assert.Contains("p-2", cls);
        Assert.Contains("align-middle", cls);
    }

    [Fact]
    public void TableCell_Custom_Class_Appended()
    {
        var cut = _ctx.Render<L.TableCell>(p => p
            .Add(c => c.Class, "my-cell")
            .AddChildContent(""));
        var cls = cut.Find("td").GetAttribute("class") ?? "";
        Assert.Contains("my-cell", cls);
    }

    [Fact]
    public void TableCell_Additional_Attributes_Forwarded()
    {
        var cut = _ctx.Render<L.TableCell>(p => p
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object> { ["data-testid"] = "my-td" })
            .AddChildContent(""));
        Assert.Equal("my-td", cut.Find("td").GetAttribute("data-testid"));
    }

    // --- TableCaption ---

    [Fact]
    public void TableCaption_Renders_Caption_Element()
    {
        var cut = _ctx.Render<L.TableCaption>(p => p.AddChildContent(""));
        Assert.NotNull(cut.Find("caption"));
    }

    [Fact]
    public void TableCaption_Renders_ChildContent()
    {
        var cut = _ctx.Render<L.TableCaption>(p => p.AddChildContent("Caption text"));
        Assert.Contains("Caption text", cut.Markup);
    }

    [Fact]
    public void TableCaption_Has_Default_Classes()
    {
        var cut = _ctx.Render<L.TableCaption>(p => p.AddChildContent(""));
        var cls = cut.Find("caption").GetAttribute("class") ?? "";
        Assert.Contains("mt-4", cls);
        Assert.Contains("text-sm", cls);
        Assert.Contains("text-muted-foreground", cls);
    }

    [Fact]
    public void TableCaption_Custom_Class_Appended()
    {
        var cut = _ctx.Render<L.TableCaption>(p => p
            .Add(c => c.Class, "my-caption")
            .AddChildContent(""));
        var cls = cut.Find("caption").GetAttribute("class") ?? "";
        Assert.Contains("my-caption", cls);
    }

    // --- Full Table Structure ---

    [Fact]
    public void Full_Table_Structure_Renders_Correctly()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Table>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.TableCaption>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(c => c.AddContent(0, "My Caption")));
                b.CloseComponent();

                b.OpenComponent<L.TableHeader>(1);
                b.AddAttribute(2, "ChildContent", (RenderFragment)(thead =>
                {
                    thead.OpenComponent<L.TableRow>(0);
                    thead.AddAttribute(1, "ChildContent", (RenderFragment)(tr =>
                    {
                        tr.OpenComponent<L.TableHead>(0);
                        tr.AddAttribute(1, "ChildContent", (RenderFragment)(th => th.AddContent(0, "Name")));
                        tr.CloseComponent();
                    }));
                    thead.CloseComponent();
                }));
                b.CloseComponent();

                b.OpenComponent<L.TableBody>(2);
                b.AddAttribute(3, "ChildContent", (RenderFragment)(tbody =>
                {
                    tbody.OpenComponent<L.TableRow>(0);
                    tbody.AddAttribute(1, "ChildContent", (RenderFragment)(tr =>
                    {
                        tr.OpenComponent<L.TableCell>(0);
                        tr.AddAttribute(1, "ChildContent", (RenderFragment)(td => td.AddContent(0, "Alice")));
                        tr.CloseComponent();
                    }));
                    tbody.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.NotNull(cut.Find("table"));
        Assert.NotNull(cut.Find("thead"));
        Assert.NotNull(cut.Find("tbody"));
        Assert.NotNull(cut.Find("caption"));
        Assert.Contains("My Caption", cut.Markup);
        Assert.Contains("Name", cut.Markup);
        Assert.Contains("Alice", cut.Markup);
    }

    // --- Striped (#262) ---

    [Fact]
    public void Table_Striped_Adds_Zebra_Rule()
    {
        var cut = _ctx.Render<L.Table>(p => p
            .Add(c => c.Striped, true)
            .AddChildContent(""));
        var cls = cut.Find("table").GetAttribute("class") ?? "";
        Assert.Contains("nth-child(even)", cls);
    }

    [Fact]
    public void Table_Not_Striped_By_Default()
    {
        var cut = _ctx.Render<L.Table>(p => p.AddChildContent(""));
        var cls = cut.Find("table").GetAttribute("class") ?? "";
        Assert.DoesNotContain("nth-child(even)", cls);
    }

    // --- TableFooter (#262) ---

    [Fact]
    public void TableFooter_Renders_Tfoot_Element()
    {
        var cut = _ctx.Render<L.TableFooter>(p => p.AddChildContent("totals"));
        Assert.NotNull(cut.Find("tfoot"));
    }

    [Fact]
    public void TableFooter_Has_Footer_Classes()
    {
        var cut = _ctx.Render<L.TableFooter>(p => p.AddChildContent(""));
        var cls = cut.Find("tfoot").GetAttribute("class") ?? "";
        Assert.Contains("border-t", cls);
        Assert.Contains("font-medium", cls);
    }

    [Fact]
    public void TableFooter_Additional_Attributes_Forwarded()
    {
        var cut = _ctx.Render<L.TableFooter>(p => p
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object> { ["data-testid"] = "tfoot" })
            .AddChildContent(""));
        Assert.Equal("tfoot", cut.Find("tfoot").GetAttribute("data-testid"));
    }

    // --- TableEmpty (#262) ---

    [Fact]
    public void TableEmpty_Renders_Row_With_Colspan()
    {
        var cut = _ctx.Render<L.TableEmpty>(p => p
            .Add(c => c.ColumnCount, 4));
        var td = cut.Find("td");
        Assert.Equal("4", td.GetAttribute("colspan"));
    }

    [Fact]
    public void TableEmpty_Falls_Back_To_Localized_NoResults()
    {
        var cut = _ctx.Render<L.TableEmpty>();
        Assert.Contains("No results", cut.Markup);
    }

    [Fact]
    public void TableEmpty_Custom_Text_Wins()
    {
        var cut = _ctx.Render<L.TableEmpty>(p => p
            .Add(c => c.Text, "Nothing here"));
        Assert.Contains("Nothing here", cut.Markup);
        Assert.DoesNotContain("No results", cut.Markup);
    }

    [Fact]
    public void TableEmpty_ChildContent_Wins_Over_Text()
    {
        var cut = _ctx.Render<L.TableEmpty>(p => p
            .Add(c => c.Text, "ignored")
            .AddChildContent("<span>custom empty</span>"));
        Assert.Contains("custom empty", cut.Markup);
        Assert.DoesNotContain("ignored", cut.Markup);
    }

    // --- TableSkeleton (#262) ---

    [Fact]
    public void TableSkeleton_Renders_Requested_Rows_And_Columns()
    {
        var cut = _ctx.Render<L.TableSkeleton>(p => p
            .Add(c => c.Rows, 3)
            .Add(c => c.Columns, 5));
        Assert.Equal(3, cut.FindAll("tr").Count);
        Assert.Equal(15, cut.FindAll("td").Count);
    }

    [Fact]
    public void TableSkeleton_Rows_Are_Aria_Hidden()
    {
        var cut = _ctx.Render<L.TableSkeleton>(p => p
            .Add(c => c.Rows, 1)
            .Add(c => c.Columns, 1));
        Assert.Equal("true", cut.Find("tr").GetAttribute("aria-hidden"));
    }
}
