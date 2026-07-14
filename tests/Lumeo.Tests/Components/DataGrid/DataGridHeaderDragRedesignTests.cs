using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// Column-header drag redesign (zone model — A: resize edges, B: grip+title, C:
/// action buttons). Covers the C#/markup half: the whole-surface sort button's
/// data-slot hook (what the pointer engine now allows to arm a drag from), aria-sort
/// staying intact, keyboard (Alt+Arrow) reorder plus its SR live-region announcement,
/// and the structural DOM markers the JS engine's non-reorderable nudge and
/// hover/touch grip-visibility CSS key off of. The actual pointer/touch gestures
/// (drag-to-reorder, click-suppression after an armed drag, long-press) are real-
/// browser behavior covered by scripts/pointer-harness, not reachable from bUnit.
/// </summary>
public class DataGridHeaderDragRedesignTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public DataGridHeaderDragRedesignTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddScoped<IComponentInteropService>(_ => _interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Row(int Id, string Name, string Dept);
    private static List<Row> Data() => new() { new(1, "Alice", "Eng"), new(2, "Bob", "Sales") };

    [Fact]
    public void Sort_Button_Has_DataSlot_And_Fills_Zone_B()
    {
        var cols = new List<DataGridColumn<Row>>
        {
            new() { Field = "Id", Title = "A", Sortable = true },
            new() { Field = "Name", Title = "B" },
        };
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p =>
        {
            p.Add(g => g.Items, Data());
            p.Add(g => g.Columns, cols);
        });

        var header = cut.FindAll("th[data-slot='datagrid-header-cell']")[0];
        var button = header.QuerySelector("button[data-slot='datagrid-sort-button']");
        Assert.NotNull(button);
        // flex-1/self-stretch is what makes zone B clickable across its whole width,
        // not just the title glyph — see the razor comment on the button.
        Assert.Contains("flex-1", button!.GetAttribute("class"));
        Assert.Contains("self-stretch", button.GetAttribute("class"));
    }

    [Fact]
    public void WholeSurface_Click_Sorts_And_AriaSort_Unchanged()
    {
        var cols = new List<DataGridColumn<Row>>
        {
            new() { Field = "Id", Title = "A", Sortable = true },
        };
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p =>
        {
            p.Add(g => g.Items, Data());
            p.Add(g => g.Columns, cols);
        });

        var header = cut.FindAll("th[data-slot='datagrid-header-cell']")[0];
        Assert.Equal("none", header.GetAttribute("aria-sort"));

        header.QuerySelector("button[data-slot='datagrid-sort-button']")!.Click();

        header = cut.FindAll("th[data-slot='datagrid-header-cell']")[0];
        Assert.Equal("ascending", header.GetAttribute("aria-sort"));

        header.QuerySelector("button[data-slot='datagrid-sort-button']")!.Click();

        header = cut.FindAll("th[data-slot='datagrid-header-cell']")[0];
        Assert.Equal("descending", header.GetAttribute("aria-sort"));
    }

    [Fact]
    public void NonSortable_Reorderable_SortButton_Is_Not_Disabled()
    {
        // Zone B's drag-arm-from-title path needs the button to actually receive
        // pointerdown — a disabled <button> never does — so a reorderable-but-not-
        // sortable column must NOT disable it, even though clicking it stays a no-op.
        var cols = new List<DataGridColumn<Row>>
        {
            new() { Field = "Id", Title = "A", Sortable = false, Reorderable = true },
            new() { Field = "Name", Title = "B", Sortable = false, Reorderable = true },
        };
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p =>
        {
            p.Add(g => g.Items, Data());
            p.Add(g => g.Columns, cols);
            p.Add(g => g.Reorderable, true);
        });

        var button = cut.FindAll("th[data-slot='datagrid-header-cell']")[0]
            .QuerySelector("button[data-slot='datagrid-sort-button']");
        Assert.NotNull(button);
        Assert.False(button!.HasAttribute("disabled"));
    }

    [Fact]
    public void Truly_Inert_SortButton_Stays_Disabled()
    {
        // Neither sortable nor reorderable: nothing zone B could do, so the button
        // stays disabled exactly like before the redesign.
        var cols = new List<DataGridColumn<Row>>
        {
            new() { Field = "Id", Title = "A", Sortable = false, Reorderable = false },
        };
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p =>
        {
            p.Add(g => g.Items, Data());
            p.Add(g => g.Columns, cols);
            p.Add(g => g.Reorderable, true);
        });

        var button = cut.Find("th[data-slot='datagrid-header-cell'] button[data-slot='datagrid-sort-button']");
        Assert.True(button.HasAttribute("disabled"));
    }

    [Fact]
    public void AltArrowRight_On_Header_Reorders_Column_And_Announces()
    {
        var a = new DataGridColumn<Row> { Field = "Id", Title = "Id", Reorderable = true };
        var b = new DataGridColumn<Row> { Field = "Name", Title = "Name", Reorderable = true };
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p =>
        {
            p.Add(g => g.Items, Data());
            p.Add(g => g.Columns, new List<DataGridColumn<Row>> { a, b });
            p.Add(g => g.Reorderable, true);
        });

        var header = cut.FindAll("th[data-slot='datagrid-header-cell']")[0];
        Assert.Equal(a.Id, header.GetAttribute("data-col-id"));

        header.TriggerEvent("onkeydown", new KeyboardEventArgs { Key = "ArrowRight", AltKey = true });

        var order = cut.FindAll("th[data-slot='datagrid-header-cell']")
            .Select(h => h.GetAttribute("data-col-id")).ToList();
        Assert.Equal(new[] { b.Id, a.Id }, order);

        var announcement = cut.Find("span[aria-live='polite']");
        Assert.Contains("Id", announcement.TextContent);
        Assert.Contains("2", announcement.TextContent); // moved to position 2 of 2
    }

    [Fact]
    public void AltArrowLeft_At_Partition_Start_Is_A_NoOp()
    {
        // Nothing reorderable in that direction — matches the pointer engine's own
        // "stay put" skip-over behavior; no reorder, no announcement change.
        var a = new DataGridColumn<Row> { Field = "Id", Title = "Id", Reorderable = true };
        var b = new DataGridColumn<Row> { Field = "Name", Title = "Name", Reorderable = true };
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p =>
        {
            p.Add(g => g.Items, Data());
            p.Add(g => g.Columns, new List<DataGridColumn<Row>> { a, b });
            p.Add(g => g.Reorderable, true);
        });

        var header = cut.FindAll("th[data-slot='datagrid-header-cell']")[0];
        header.TriggerEvent("onkeydown", new KeyboardEventArgs { Key = "ArrowLeft", AltKey = true });

        var order = cut.FindAll("th[data-slot='datagrid-header-cell']")
            .Select(h => h.GetAttribute("data-col-id")).ToList();
        Assert.Equal(new[] { a.Id, b.Id }, order);
    }

    [Fact]
    public void NonReorderable_Column_Has_No_DataReorderable_Attribute()
    {
        // The JS engine's non-reorderable nudge feedback (registerColumnReorder's
        // onPointerDown) keys off this attribute being absent/false — it never
        // arms a drag for such a column, only shows the nudge-and-spring instead.
        var cols = new List<DataGridColumn<Row>>
        {
            new() { Field = "Id", Title = "A", Reorderable = false },
            new() { Field = "Name", Title = "B", Reorderable = true },
        };
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p =>
        {
            p.Add(g => g.Items, Data());
            p.Add(g => g.Columns, cols);
            p.Add(g => g.Reorderable, true);
        });

        var headers = cut.FindAll("th[data-slot='datagrid-header-cell']");
        Assert.Null(headers[0].GetAttribute("data-reorderable"));
        Assert.Equal("true", headers[1].GetAttribute("data-reorderable"));
    }

    [Fact]
    public void Grip_Carries_Visibility_Css_Hook_Class_And_No_Longer_Inlines_Opacity()
    {
        var cols = new List<DataGridColumn<Row>>
        {
            new() { Field = "Id", Title = "A", Reorderable = true },
            new() { Field = "Name", Title = "B", Reorderable = true },
        };
        var cut = _ctx.Render<Lumeo.DataGrid<Row>>(p =>
        {
            p.Add(g => g.Items, Data());
            p.Add(g => g.Columns, cols);
            p.Add(g => g.Reorderable, true);
        });

        var grip = cut.Find("[data-reorder-grip]");
        var gripClass = grip.GetAttribute("class") ?? "";
        Assert.Contains("lumeo-dg-reorder-grip", gripClass);
        // The old always-on opacity-60/hover:opacity-100 utility pair is gone — the
        // dedicated class now owns visibility (hover/focus-within/pointer-fine vs.
        // always-on for touch), see lumeo.css.
        Assert.DoesNotContain("opacity-60", gripClass);
    }
}
