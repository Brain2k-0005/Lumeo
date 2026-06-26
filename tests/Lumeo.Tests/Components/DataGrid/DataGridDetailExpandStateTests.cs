using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// Battle-test regression (wave1 #7, state-on-data-change):
/// "Detail-row expand state (and in-progress row edits) silently reset on any
/// Items refresh that produces fresh instances."
///
/// <para>
/// _expandedRows/_editingRows/_rowEditValues are keyed by TItem reference. A
/// same-content Items refresh that hands the grid value-equal but
/// reference-distinct instances (server-mode page fetch, parent re-fetch) used
/// to orphan those sets — expanded detail rows collapsed and open editors
/// vanished even though the logical row was still present. The fix re-anchors
/// those sets onto the fresh instances by stable key inside ProcessClientData
/// whenever SelectionKeySelector is supplied, mirroring the _selectedItems
/// cleanup.
/// </para>
/// </summary>
public class DataGridDetailExpandStateTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataGridDetailExpandStateTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // Plain reference type (NOT a record): reference-distinct instances do NOT
    // compare equal, which is exactly the failure mode the bug describes. A
    // record would compare value-equal and mask the orphaning.
    private sealed class TestItem
    {
        public TestItem(int id, string name) { Id = id; Name = name; }
        public int Id { get; }
        public string Name { get; }
    }

    // Distinct instances each call — same Ids/Names. Simulates a parent
    // re-fetch (or server-mode page) producing reference-distinct rows.
    private static List<TestItem> FreshData() => new()
    {
        new(1, "Alice"),
        new(2, "Bob"),
        new(3, "Charlie"),
    };

    private static List<DataGridColumn<TestItem>> GetColumns() => new()
    {
        new() { Field = "Id", Title = "ID" },
        new() { Field = "Name", Title = "Name" },
    };

    private const string DetailMarker = "row-detail-marker";

    private static RenderFragment<TestItem> DetailTemplate() =>
        item => builder =>
        {
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "class", DetailMarker);
            builder.AddContent(2, $"Detail of {item.Name}");
            builder.CloseElement();
        };

    [Fact]
    public async Task ExpandedDetailRow_SurvivesItemsRefresh_WithFreshInstances_WhenKeyed()
    {
        var cut = _ctx.Render<DataGrid<TestItem>>(p => p
            .Add(x => x.Items, FreshData())
            .Add(x => x.Columns, GetColumns())
            .Add(x => x.SelectionKeySelector, item => item.Id)
            .Add(x => x.DetailTemplate, DetailTemplate()));

        // Collapsed initially: no detail content rendered.
        Assert.DoesNotContain(DetailMarker, cut.Markup);

        // Expand the first row via its detail-toggle button. With no selection
        // column and no row-reorder, the only leading <td class="... w-8"> is
        // the detail toggle.
        var toggle = cut.Find("td.w-8 button");
        await cut.InvokeAsync(() => toggle.Click());

        // The detail row for Alice is now open.
        Assert.Contains(DetailMarker, cut.Markup);
        Assert.Contains("Detail of Alice", cut.Markup);

        // Parent refresh: brand-new instances, same Ids. Without the fix the
        // expand set still references the OLD Alice instance, so the new Alice
        // row renders collapsed and the detail marker disappears.
        cut.Render(p => p.Add(x => x.Items, FreshData()));

        Assert.Contains(DetailMarker, cut.Markup);
        Assert.Contains("Detail of Alice", cut.Markup);
    }
}
