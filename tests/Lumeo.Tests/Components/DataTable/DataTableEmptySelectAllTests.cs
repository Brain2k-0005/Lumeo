using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.DataTable;

/// <summary>
/// Regression for battle-test finding #63 (low, edge-data): the select-all
/// header checkbox on an empty / fully-filtered-out table.
///
/// Two parts:
///   1. The header checkbox must render UNCHECKED when there are no rows
///      (nothing is "all selected" when there is nothing to select).
///   2. Clicking the header select-all toggle must be a true no-op on an empty
///      table — it must NOT emit a spurious <c>SelectedItemsChanged(empty)</c>
///      event. The fix guards <c>ToggleSelectAll</c> with
///      <c>if (Items is null || !Items.Any()) return;</c>.
/// </summary>
public class DataTableEmptySelectAllTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataTableEmptySelectAllTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Person(string Name, int Age);

    private IRenderedComponent<L.DataTable<Person>> RenderTable(
        List<Person> items,
        HashSet<Person> selected,
        EventCallback<HashSet<Person>> selectedChanged)
    {
        return _ctx.Render<L.DataTable<Person>>(builder =>
        {
            builder.OpenComponent<L.DataTable<Person>>(0);
            builder.AddAttribute(1, "Items", items);
            builder.AddAttribute(2, "Selectable", true);
            builder.AddAttribute(3, "SelectedItems", selected);
            builder.AddAttribute(4, "SelectedItemsChanged", selectedChanged);
            builder.AddAttribute(5, "HeaderTemplate", (RenderFragment)(h =>
            {
                h.OpenElement(0, "th"); h.AddContent(1, "Name"); h.CloseElement();
            }));
            builder.AddAttribute(6, "RowTemplate", (RenderFragment<Person>)(p => rb =>
            {
                rb.OpenElement(0, "td"); rb.AddContent(1, p.Name); rb.CloseElement();
            }));
            builder.CloseComponent();
        });
    }

    [Fact]
    public void EmptyTable_HeaderCheckbox_Is_Unchecked()
    {
        // An empty Items list with selectable rows: the select-all checkbox
        // reflects "nothing is all-selected" — it must NOT show checked/true.
        var cut = RenderTable([], new HashSet<Person>(), default);

        var headerCheckbox = cut.Find("thead [role=checkbox]");
        Assert.Equal("false", headerCheckbox.GetAttribute("aria-checked"));
    }

    [Fact]
    public void ToggleSelectAll_On_EmptyTable_Does_Not_Emit_Change()
    {
        // The core repro: clicking select-all on an empty table must be a no-op.
        // Without the empty guard, ToggleSelectAll falls through to
        // SelectedItemsChanged.InvokeAsync(new HashSet<>()) — a spurious change
        // event for a table that has nothing to select.
        var selected = new HashSet<Person>();
        var emittedCount = 0;
        HashSet<Person>? emitted = null;
        var cb = EventCallback.Factory.Create<HashSet<Person>>(this, set =>
        {
            emittedCount++;
            emitted = set;
        });

        var cut = RenderTable([], selected, cb);

        var headerCheckbox = cut.Find("thead [role=checkbox]");
        headerCheckbox.Click();

        // No-op: the callback never fired because there was nothing to select.
        Assert.Equal(0, emittedCount);
        Assert.Null(emitted);
    }

    [Fact]
    public void ToggleSelectAll_On_NonEmptyTable_Still_Emits()
    {
        // Guard rail: the empty-table fix must NOT change the normal path — a
        // table WITH rows still emits select-all as before.
        var data = new List<Person> { new("Alice", 30), new("Bob", 25) };
        var emittedCount = 0;
        HashSet<Person>? emitted = null;
        var cb = EventCallback.Factory.Create<HashSet<Person>>(this, set =>
        {
            emittedCount++;
            emitted = set;
        });

        var cut = RenderTable(data, new HashSet<Person>(), cb);

        var headerCheckbox = cut.Find("thead [role=checkbox]");
        headerCheckbox.Click();

        Assert.Equal(1, emittedCount);
        Assert.NotNull(emitted);
        Assert.Equal(data.Count, emitted!.Count);
    }
}
