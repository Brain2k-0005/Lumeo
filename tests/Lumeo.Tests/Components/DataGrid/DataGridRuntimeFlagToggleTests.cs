using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// Regression tests for the previously-dead runtime flips of the per-column
/// affordance flags (Sortable / Filterable / Resizable / Pinnable, declared via
/// <see cref="DataGridColumnDef{TItem}"/>) and the grid-level
/// <see cref="DataGrid{TItem}.Hoverable"/> parameter.
///
/// The column flags were captured once at <c>DataGridColumnDef.OnInitialized</c>
/// registration and never re-read, so a host binding like
/// <c>Sortable="_someState"</c> was inert after first render (the deliberately
/// reverted "re-Register on OnParametersSet" attempt is documented in
/// DataGridColumnDef — the new targeted <c>DataGrid.UpdateColumnFlags</c> push
/// avoids that revert's autosave-thrash trap). Hoverable was baked into
/// <c>DataGridRow</c>'s base class string unconditionally, so the parameter never
/// reached the rendered markup at all.
/// </summary>
public class DataGridRuntimeFlagToggleTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataGridRuntimeFlagToggleTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Row(int Id, string Name);

    private static readonly Row[] Data = { new(1, "Alice"), new(2, "Bob") };

    /// <summary>
    /// Renders a grid whose single declarative column def binds all four
    /// affordance flags to the given value — mirroring the docs playground's
    /// <c>Sortable="_pgSortable" ...</c> host-state binding.
    /// </summary>
    private IRenderedComponent<DataGrid<Row>> RenderGrid(bool flags) =>
        _ctx.Render<DataGrid<Row>>(p => p
            .Add(g => g.Items, Data)
            .AddChildContent<DataGridColumnDef<Row>>(c => c
                .Add(x => x.Field, "Name")
                .Add(x => x.Title, "Name")
                .Add(x => x.Sortable, flags)
                .Add(x => x.Filterable, flags)
                .Add(x => x.Resizable, flags)
                .Add(x => x.Pinnable, flags)));

    private static void Rerender(IRenderedComponent<DataGrid<Row>> cut, bool flags) =>
        cut.Render(p => p
            .Add(g => g.Items, Data)
            .AddChildContent<DataGridColumnDef<Row>>(c => c
                .Add(x => x.Field, "Name")
                .Add(x => x.Title, "Name")
                .Add(x => x.Sortable, flags)
                .Add(x => x.Filterable, flags)
                .Add(x => x.Resizable, flags)
                .Add(x => x.Pinnable, flags)));

    // --- Per-flag DOM markers -------------------------------------------------
    // Sortable    -> th carries aria-sort (="none" when unsorted); absent otherwise.
    // Filterable  -> the filter trigger button (aria-label "Filter …") exists.
    // Resizable   -> the resize handle [data-slot='datagrid-resize-handle'] exists.
    // Pinnable    -> the pin trigger button (aria-label "Pin column") exists.

    private static bool HasAriaSort(IRenderedComponent<DataGrid<Row>> cut) =>
        cut.FindAll("th[data-slot='datagrid-header-cell'][aria-sort]").Count > 0;

    private static bool HasFilterButton(IRenderedComponent<DataGrid<Row>> cut) =>
        cut.FindAll("th [aria-label^='Filter']").Count > 0;

    private static bool HasResizeHandle(IRenderedComponent<DataGrid<Row>> cut) =>
        cut.FindAll("[data-slot='datagrid-resize-handle']").Count > 0;

    private static bool HasPinButton(IRenderedComponent<DataGrid<Row>> cut) =>
        cut.FindAll("th [aria-label='Pin column']").Count > 0;

    [Fact]
    public void Flags_On_Render_All_Affordances()
    {
        var cut = RenderGrid(flags: true);
        Assert.True(HasAriaSort(cut));
        Assert.True(HasFilterButton(cut));
        Assert.True(HasResizeHandle(cut));
        Assert.True(HasPinButton(cut));
    }

    [Fact]
    public void Flags_Off_Render_No_Affordances()
    {
        var cut = RenderGrid(flags: false);
        Assert.False(HasAriaSort(cut));
        Assert.False(HasFilterButton(cut));
        Assert.False(HasResizeHandle(cut));
        Assert.False(HasPinButton(cut));
    }

    [Fact]
    public void Turning_Flags_Off_At_Runtime_Removes_Affordances()
    {
        var cut = RenderGrid(flags: true);
        Assert.True(HasAriaSort(cut));

        Rerender(cut, flags: false);

        Assert.False(HasAriaSort(cut));
        Assert.False(HasFilterButton(cut));
        Assert.False(HasResizeHandle(cut));
        Assert.False(HasPinButton(cut));
    }

    [Fact]
    public void Turning_Flags_On_At_Runtime_Adds_Affordances()
    {
        var cut = RenderGrid(flags: false);
        Assert.False(HasAriaSort(cut));

        Rerender(cut, flags: true);

        Assert.True(HasAriaSort(cut));
        Assert.True(HasFilterButton(cut));
        Assert.True(HasResizeHandle(cut));
        Assert.True(HasPinButton(cut));
    }

    // --- Hoverable ------------------------------------------------------------

    private static string FirstBodyRowClass(IRenderedComponent<DataGrid<Row>> cut) =>
        cut.FindAll("tr[data-slot='datagrid-row']")[0].GetAttribute("class") ?? "";

    private IRenderedComponent<DataGrid<Row>> RenderHoverGrid(bool hoverable) =>
        _ctx.Render<DataGrid<Row>>(p => p
            .Add(g => g.Items, Data)
            .Add(g => g.Hoverable, hoverable)
            .Add(g => g.Columns, new List<DataGridColumn<Row>>
            {
                new() { Field = "Id", Title = "ID" },
                new() { Field = "Name", Title = "Name" },
            }));

    [Fact]
    public void Hoverable_On_Applies_Row_Hover_Class()
    {
        var cut = RenderHoverGrid(hoverable: true);
        Assert.Contains("hover:bg-muted/50", FirstBodyRowClass(cut));
    }

    [Fact]
    public void Hoverable_Off_Omits_Row_Hover_Class()
    {
        var cut = RenderHoverGrid(hoverable: false);
        Assert.DoesNotContain("hover:bg-muted/50", FirstBodyRowClass(cut));
    }

    [Fact]
    public void Toggling_Hoverable_After_First_Render_Updates_Rows()
    {
        var cols = new List<DataGridColumn<Row>>
        {
            new() { Field = "Id", Title = "ID" },
            new() { Field = "Name", Title = "Name" },
        };
        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(g => g.Items, Data)
            .Add(g => g.Hoverable, true)
            .Add(g => g.Columns, cols));
        Assert.Contains("hover:bg-muted/50", FirstBodyRowClass(cut));

        cut.Render(p => p
            .Add(g => g.Items, Data)
            .Add(g => g.Hoverable, false)
            .Add(g => g.Columns, cols));

        Assert.DoesNotContain("hover:bg-muted/50", FirstBodyRowClass(cut));
    }
}
