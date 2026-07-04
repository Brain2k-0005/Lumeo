using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// Regression tests for the column-chooser row toggle and the new two-way
/// <c>DataGridColumnDef.Visible</c> binding.
///
/// Bug 1: each chooser row wrapped Lumeo's <c>Checkbox</c> in a <c>&lt;label&gt;</c>.
/// Checkbox is a <c>button[role=checkbox]</c>, which a <c>&lt;label for&gt;</c> cannot
/// activate — so clicking the row label (the column name) did nothing. The whole row
/// is now itself a <c>role=checkbox</c> button, so clicking anywhere toggles.
///
/// Bug 2: consumers had no programmatic control of column visibility. A column def
/// now exposes <c>@bind-Visible</c> — seeding initial visibility, reacting to
/// consumer-driven changes, and surfacing user toggles back through VisibleChanged.
/// </summary>
public class DataGridColumnVisibilityToggleTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataGridColumnVisibilityToggleTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Row(int Id, string Name);

    private static readonly Row[] Data = { new(1, "Alice"), new(2, "Bob") };

    // --- Bug 1: the whole chooser row is an interactive toggle -----------------

    [Fact]
    public void Chooser_Row_Label_Region_Is_A_Toggle_Button_That_Fires()
    {
        var col = new DataGridColumn<Row> { Field = "Name", Title = "Name", Visible = true };
        DataGridColumn<Row>? toggled = null;

        var cut = _ctx.Render<DataGridColumnVisibility<Row>>(b =>
        {
            b.OpenComponent<DataGridColumnVisibility<Row>>(0);
            b.AddAttribute(1, "Columns", (IReadOnlyList<DataGridColumn<Row>>)new[] { col });
            b.AddAttribute(2, "OnColumnToggle",
                EventCallback.Factory.Create<DataGridColumn<Row>>(this, c => toggled = c));
            b.CloseComponent();
        });

        // The row toggle is a role=checkbox button that CARRIES the column title.
        // Pre-fix the only role=checkbox was the inner Checkbox button, whose text
        // is empty — so this Single(...) would throw, failing the test.
        var rowToggle = cut.FindAll("button[role=checkbox]")
            .Single(x => x.TextContent.Contains("Name"));
        Assert.Equal("true", rowToggle.GetAttribute("aria-checked"));

        rowToggle.Click();

        Assert.Same(col, toggled);
        Assert.False(col.Visible);
    }

    // --- Bug 2a: consumer-driven visibility (Visible parameter) -----------------

    // Two column defs inside ONE ChildContent fragment (bUnit collides on a
    // re-render that supplies AddChildContent twice — a single fragment sidesteps
    // that and keeps the SAME child component instances so OnParametersSet fires).
    private static RenderFragment Columns(bool nameVisible) => builder =>
    {
        builder.OpenComponent<DataGridColumnDef<Row>>(0);
        builder.AddAttribute(1, "Field", "Id");
        builder.AddAttribute(2, "Title", "ID");
        builder.CloseComponent();
        builder.OpenComponent<DataGridColumnDef<Row>>(3);
        builder.AddAttribute(4, "Field", "Name");
        builder.AddAttribute(5, "Title", "Name");
        builder.AddAttribute(6, "Visible", nameVisible);
        builder.CloseComponent();
    };

    [Fact]
    public void ColumnDef_Visible_False_Hides_Column_And_Flipping_It_Shows_It()
    {
        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(g => g.Items, Data)
            .Add(g => g.ChildContent, Columns(nameVisible: false)));

        var headers = cut.FindAll("th[data-slot='datagrid-header-cell']");
        Assert.Contains(headers, h => h.TextContent.Contains("ID"));
        Assert.DoesNotContain(headers, h => h.TextContent.Contains("Name"));

        // Consumer flips Visible → column re-appears (OnParametersSet → grid sync).
        cut.Render(p => p
            .Add(g => g.Items, Data)
            .Add(g => g.ChildContent, Columns(nameVisible: true)));

        var headers2 = cut.FindAll("th[data-slot='datagrid-header-cell']");
        Assert.Contains(headers2, h => h.TextContent.Contains("Name"));
    }

    // --- Bug 2b: user toggle surfaces back through VisibleChanged ----------------

    [Fact]
    public void User_Toggle_In_Chooser_Raises_ColumnDef_VisibleChanged()
    {
        bool? captured = null;

        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(g => g.Items, Data)
            .Add(g => g.ShowToolbar, true)
            .Add(g => g.ShowColumnChooser, true)
            .AddChildContent<DataGridColumnDef<Row>>(c => c.Add(x => x.Field, "Id").Add(x => x.Title, "ID"))
            .AddChildContent<DataGridColumnDef<Row>>(c => c
                .Add(x => x.Field, "Name").Add(x => x.Title, "Name")
                .Add(x => x.VisibleChanged, EventCallback.Factory.Create<bool>(this, v => captured = v))));

        // Open the column chooser popover.
        var columnsBtn = cut.FindAll("button")
            .First(b => (b.GetAttribute("id") ?? "").StartsWith("dg-columns-trigger"));
        columnsBtn.Click();

        // Toggle the Name column off from the chooser row.
        var nameToggle = cut.FindAll("button[role=checkbox]")
            .Single(x => x.TextContent.Contains("Name"));
        nameToggle.Click();

        Assert.Equal(false, captured);
    }
}
