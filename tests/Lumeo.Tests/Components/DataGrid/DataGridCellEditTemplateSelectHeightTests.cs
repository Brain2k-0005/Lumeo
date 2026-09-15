using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// Review round 1 on PR #481, finding B1: a Select-based custom <c>EditTemplate</c> at its
/// smallest density rung (SelectTrigger's Compact <c>h-7</c> = 28px) is taller than the built-in
/// text editor's line-height (20px). In <see cref="DataGridEditMode.Cell"/> the row has no fixed
/// height of its own — the <c>&lt;td&gt;</c>'s own padding supplies it, and the built-in editor
/// (no border/padding/fixed height) just sizes to its text-sm line-height, landing on the same
/// total as a non-editing row. A taller trigger grows the row while editing and it snaps back on
/// commit/cancel. Mirrors docs/Lumeo.Docs/Pages/Components/DataGridPage.razor's "Custom Cell
/// Editor" demo fix: size the trigger to <c>h-5 px-2 py-0</c> (20px, matching the line-height)
/// instead of a density rung, so the cell's own padding stays the only source of height.
/// </summary>
public class DataGridCellEditTemplateSelectHeightTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataGridCellEditTemplateSelectHeightTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Row(int Id, string Status);

    private static List<Row> Sample() => new() { new(1, "Active"), new(2, "Inactive") };

    // Mirrors DataGridPage.razor's "Custom Cell Editor" demo EditTemplate exactly: a Select
    // whose SelectTrigger is sized with Class="h-5 px-2 py-0" instead of a Density rung.
    private RenderFragment<CellEditContext<Row>> SelectEditTemplate() => ctx => builder =>
    {
        builder.OpenComponent<Lumeo.Select>(0);
        builder.AddComponentParameter(1, nameof(Lumeo.Select.Value), ctx.Value?.ToString());
        builder.AddComponentParameter(2, nameof(Lumeo.Select.ValueChanged),
            EventCallback.Factory.Create<string?>(this, async v =>
            {
                ctx.Value = v;
                ctx.ValueChanged();
                await ctx.Commit();
            }));
        builder.AddComponentParameter(3, nameof(Lumeo.Select.ChildContent), (RenderFragment)(child =>
        {
            child.OpenComponent<Lumeo.SelectTrigger>(0);
            child.AddComponentParameter(1, nameof(Lumeo.SelectTrigger.Class), "h-5 px-2 py-0");
            child.CloseComponent();

            child.OpenComponent<Lumeo.SelectContent>(10);
            child.AddComponentParameter(11, nameof(Lumeo.SelectContent.ChildContent), (RenderFragment)(content =>
            {
                content.OpenComponent<Lumeo.SelectItem>(0);
                content.AddComponentParameter(1, nameof(Lumeo.SelectItem.Value), "Active");
                content.AddComponentParameter(2, nameof(Lumeo.SelectItem.ChildContent), (RenderFragment)(i => i.AddContent(0, "Active")));
                content.CloseComponent();
                content.OpenComponent<Lumeo.SelectItem>(3);
                content.AddComponentParameter(4, nameof(Lumeo.SelectItem.Value), "Inactive");
                content.AddComponentParameter(5, nameof(Lumeo.SelectItem.ChildContent), (RenderFragment)(i => i.AddContent(0, "Inactive")));
                content.CloseComponent();
            }));
            child.CloseComponent();
        }));
        builder.CloseComponent();
    };

    [Fact]
    public async Task Opening_The_Select_Editor_Does_Not_Change_The_Cells_Own_Padding_Classes()
    {
        var columns = new List<DataGridColumn<Row>>
        {
            new() { Field = "Status", Title = "Status", EditTemplate = SelectEditTemplate() },
        };

        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(x => x.Items, Sample())
            .Add(x => x.Columns, columns)
            .Add(x => x.EditMode, DataGridEditMode.Cell));

        var closedClass = cut.Find("td[data-slot='datagrid-cell']").GetAttribute("class");

        await cut.InvokeAsync(() => cut.Find("td[data-slot='datagrid-cell']").Click());

        // Same <td>, same padding classes while editing — the cell's own padding is the ONLY
        // thing that should be contributing height in either state, exactly like the built-in
        // text editor (which has no border/padding/fixed height of its own).
        var openClass = cut.Find("td[data-slot='datagrid-cell']").GetAttribute("class");
        Assert.Equal(closedClass, openClass);
    }

    [Fact]
    public async Task Select_Editor_Trigger_Is_Sized_To_The_Built_In_Editors_Line_Height_Not_A_Density_Rung()
    {
        var columns = new List<DataGridColumn<Row>>
        {
            new() { Field = "Status", Title = "Status", EditTemplate = SelectEditTemplate() },
        };

        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(x => x.Items, Sample())
            .Add(x => x.Columns, columns)
            .Add(x => x.EditMode, DataGridEditMode.Cell));

        await cut.InvokeAsync(() => cut.Find("td[data-slot='datagrid-cell']").Click());

        var triggerClasses = (cut.Find("[data-slot='select-trigger']").GetAttribute("class") ?? "")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // h-5 (20px) matches text-sm's line-height — the same total the built-in text editor gets
        // "for free". None of SelectTrigger's normal Density rungs (h-7 Compact / the default
        // control-height token / h-9 Spacious) would land on that.
        Assert.Contains("h-5", triggerClasses);
        Assert.DoesNotContain("h-7", triggerClasses);
        Assert.DoesNotContain("h-8", triggerClasses);
        Assert.DoesNotContain("h-9", triggerClasses);
    }
}
