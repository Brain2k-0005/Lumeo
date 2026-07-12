using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// axe button-name regression coverage: every icon-only control the grid owns
/// directly (filter toggle, row/select-all checkboxes, single-select radio,
/// detail expand/collapse) must expose a real accessible name — a docs sweep
/// found 113 nameless buttons on a single DataGrid-heavy route before these
/// fixes (dg-filter-trigger: 29, checkbox: 18, single-select radio + detail
/// toggle: 6, plus consumer-owned demo buttons outside this component's
/// control).
/// </summary>
public class DataGridAccessibleNamesTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataGridAccessibleNamesTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Row(int Id, string Name);

    private static List<Row> Data() => new() { new(1, "Alice"), new(2, "Bob") };

    [Fact]
    public void FilterTrigger_Has_Column_Context_AriaLabel()
    {
        var col = new DataGridColumn<Row> { Field = "Name", Title = "Name", Filterable = true };

        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.Columns, new List<DataGridColumn<Row>> { col }));

        var trigger = cut.Find("[id^='dg-filter-trigger-']");
        var label = trigger.GetAttribute("aria-label");
        Assert.False(string.IsNullOrWhiteSpace(label));
        Assert.Contains("Name", label);
    }

    [Fact]
    public void Row_Checkbox_Has_Row_Context_AriaLabel()
    {
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.Columns, new List<DataGridColumn<Row>> { new() { Field = "Id", Title = "ID" } })
            .Add(g => g.SelectionMode, DataGridSelectionMode.Multiple));

        var rowCheckboxes = cut.FindAll("td[role='gridcell'] button[role='checkbox']");
        Assert.NotEmpty(rowCheckboxes);
        Assert.All(rowCheckboxes, b => Assert.False(string.IsNullOrWhiteSpace(b.GetAttribute("aria-label"))));
    }

    [Fact]
    public void SelectAll_Checkbox_Has_AriaLabel()
    {
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.Columns, new List<DataGridColumn<Row>> { new() { Field = "Id", Title = "ID" } })
            .Add(g => g.SelectionMode, DataGridSelectionMode.Multiple));

        var headerCheckbox = cut.Find("thead button[role='checkbox']");
        Assert.False(string.IsNullOrWhiteSpace(headerCheckbox.GetAttribute("aria-label")));
    }

    [Fact]
    public void Single_Select_Radio_Button_Has_AriaLabel()
    {
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.Columns, new List<DataGridColumn<Row>> { new() { Field = "Id", Title = "ID" } })
            .Add(g => g.SelectionMode, DataGridSelectionMode.Single));

        var radioButtons = cut.FindAll("td[role='gridcell'] > button.rounded-full");
        Assert.NotEmpty(radioButtons);
        Assert.All(radioButtons, b => Assert.False(string.IsNullOrWhiteSpace(b.GetAttribute("aria-label"))));
    }

    [Fact]
    public void Group_Row_Toggle_Has_Group_Context_AriaLabel()
    {
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.Columns, new List<DataGridColumn<Row>> { new() { Field = "Name", Title = "Name" } })
            .Add(g => g.GroupBy, "Name"));

        var groupToggle = cut.Find("tr[data-slot='datagrid-group-row'] button");
        var label = groupToggle.GetAttribute("aria-label");
        Assert.False(string.IsNullOrWhiteSpace(label));
    }

    [Fact]
    public void Detail_Expand_Toggle_Has_AriaLabel_That_Reflects_State()
    {
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.Columns, new List<DataGridColumn<Row>> { new() { Field = "Id", Title = "ID" } })
            .Add(g => g.DetailTemplate, (RenderFragment<Row>)(row => b => b.AddContent(0, $"Detail for {row.Name}"))));

        var toggle = cut.FindAll("td[role='gridcell'] button").First(b => b.QuerySelector("svg") != null);
        var collapsedLabel = toggle.GetAttribute("aria-label");
        Assert.False(string.IsNullOrWhiteSpace(collapsedLabel));

        toggle.Click();

        var expandedLabel = cut.FindAll("td[role='gridcell'] button").First(b => b.QuerySelector("svg") != null)
            .GetAttribute("aria-label");
        Assert.False(string.IsNullOrWhiteSpace(expandedLabel));
        Assert.NotEqual(collapsedLabel, expandedLabel);
    }
}
