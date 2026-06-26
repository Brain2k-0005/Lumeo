using System.Runtime.CompilerServices;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Table;

/// <summary>
/// Regression for battle-test finding #9 (medium, edge-data):
/// "Selection set keyed by record value-equality silently merges/aliases
/// distinct rows with identical field values."
///
/// <para>
/// When <c>TItem</c> is a <see langword="record"/> (value-equality) and two
/// DISTINCT rows carry IDENTICAL field values — e.g. two freshly-added "blank"
/// / "Guest" rows (the exact collision <c>DataGridRowKeys</c> calls out) — a
/// plain <c>HashSet&lt;TItem&gt;</c> collapses them into one bucket. Selecting
/// one row then aliases its value-equal twin (both render <c>aria-selected</c>)
/// and the select-all count is wrong. The DOM, however, keys each row by
/// <em>instance identity</em> (<c>DataGridRowKeys.KeyFor</c> =&gt;
/// <c>RuntimeHelpers.GetHashCode</c>), so selection (value identity) and the
/// grid (instance identity) disagree.
/// </para>
///
/// <para>
/// The fix already lives in <c>DataTable.razor</c>: the <c>ItemKey</c> selector
/// routes membership through a stable per-row key in
/// <c>IsSelected</c>/<c>ToggleItem</c>/<c>ToggleSelectAll</c>/<c>IsAllSelected</c>.
/// Supplying the SAME instance-identity key the grid uses for <c>@key</c>
/// (<c>RuntimeHelpers.GetHashCode</c>) makes value-equal rows behave as the
/// distinct rows they are. These tests lock that edge-data behaviour in; the
/// companion test documents the un-keyed value-equality aliasing the fix avoids.
/// </para>
/// </summary>
public class DataTableSelectionAliasTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataTableSelectionAliasTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // RECORD => value-equality on ALL members. The two "Guest" rows below are
    // FULLY value-equal (same Id, same Name) yet are separate list entries /
    // instances — the literal "distinct rows with identical field values"
    // collision finding #9 is about.
    private sealed record Person(int Id, string Name);

    // data[0] and data[1] are value-equal twins (Guest/0); data[2] differs.
    private static List<Person> MakeData() =>
    [
        new Person(0, "Guest"),   // <- selected instance
        new Person(0, "Guest"),   // <- value-equal TWIN, must NOT alias-select
        new Person(7, "Blair"),
    ];

    // The stable per-row key the grid itself uses for @key: instance identity.
    // For fully value-equal records, NO field distinguishes the rows — only
    // instance identity does, exactly like DataGridRowKeys.KeyFor.
    private static readonly Func<Person, object> IdentityKey =
        p => RuntimeHelpers.GetHashCode(p);

    private IRenderedComponent<L.DataTable<Person>> RenderTable(
        List<Person> items,
        HashSet<Person> selected,
        Func<Person, object>? itemKey,
        EventCallback<HashSet<Person>>? selectedChanged = null)
    {
        return _ctx.Render<L.DataTable<Person>>(builder =>
        {
            builder.OpenComponent<L.DataTable<Person>>(0);
            builder.AddAttribute(1, "Items", items);
            builder.AddAttribute(2, "Selectable", true);
            builder.AddAttribute(3, "SelectedItems", selected);
            if (itemKey is not null) builder.AddAttribute(4, "ItemKey", itemKey);
            if (selectedChanged is not null)
                builder.AddAttribute(5, "SelectedItemsChanged", selectedChanged.Value);
            builder.AddAttribute(6, "HeaderTemplate", (RenderFragment)(h =>
            {
                h.OpenElement(0, "th"); h.AddContent(1, "Name"); h.CloseElement();
            }));
            builder.AddAttribute(7, "RowTemplate", (RenderFragment<Person>)(p => rb =>
            {
                rb.OpenElement(0, "td"); rb.AddContent(1, p.Name); rb.CloseElement();
            }));
            builder.CloseComponent();
        });
    }

    [Fact]
    public void ItemKey_Keeps_ValueEqual_Records_Distinct_No_Alias_Select()
    {
        var data = MakeData();
        // Select ONLY the first Guest instance.
        var selected = new HashSet<Person>(ReferenceEqualityComparer.Instance) { data[0] };

        var cut = RenderTable(data, selected, itemKey: IdentityKey);

        var rows = cut.FindAll("tbody tr");
        // The selected instance is highlighted...
        Assert.Equal("true", rows[0].GetAttribute("aria-selected"));
        // ...but its FULLY value-equal twin (data[1]) is NOT aliased into the
        // selection, because membership is decided by the instance-identity key,
        // matching the @key the grid uses for DOM diffing.
        Assert.Equal("false", rows[1].GetAttribute("aria-selected"));
        Assert.Equal("false", rows[2].GetAttribute("aria-selected"));
    }

    [Fact]
    public void Without_ItemKey_ValueEqual_Records_Alias_Into_The_Selection()
    {
        // Documents the pre-fix edge-data behaviour: with a record TItem and no
        // ItemKey, the default HashSet<Person> buckets data[0] and data[1]
        // together (value-equal), so selecting one visually selects BOTH while
        // the grid still renders them as two distinct rows.
        var data = MakeData();
        var selected = new HashSet<Person> { data[0] };

        var cut = RenderTable(data, selected, itemKey: null);

        var rows = cut.FindAll("tbody tr");
        Assert.Equal("true", rows[0].GetAttribute("aria-selected"));
        // The alias bug: row 2 is value-equal, so Contains() returns true for it.
        Assert.Equal("true", rows[1].GetAttribute("aria-selected"));
        // The genuinely-different row stays unselected.
        Assert.Equal("false", rows[2].GetAttribute("aria-selected"));
    }

    [Fact]
    public void ItemKey_Toggle_Only_Selects_The_Clicked_Instance_Not_Its_Twin()
    {
        var data = MakeData();
        var selected = new HashSet<Person>(ReferenceEqualityComparer.Instance);
        HashSet<Person>? emitted = null;
        var cb = EventCallback.Factory.Create<HashSet<Person>>(this, s => emitted = s);

        var cut = RenderTable(data, selected, itemKey: IdentityKey, selectedChanged: cb);

        // Click the SECOND row's checkbox — the value-equal twin (data[1]).
        var checkboxes = cut.FindAll("tbody tr td [role=checkbox]");
        checkboxes[1].Click();

        Assert.NotNull(emitted);
        // Exactly ONE instance is selected — the one clicked — not its twin.
        Assert.Single(emitted!);
        Assert.True(ReferenceEquals(data[1], emitted!.Single()));
    }

    [Fact]
    public void ItemKey_IsAllSelected_Counts_ValueEqual_Records_Separately()
    {
        var data = MakeData();
        // Select BOTH Guest twins (by instance) but leave Blair — NOT all rows.
        var selected = new HashSet<Person>(ReferenceEqualityComparer.Instance) { data[0], data[1] };

        var cut = RenderTable(data, selected, itemKey: IdentityKey);

        // The select-all header checkbox must be INDETERMINATE (mixed): two of
        // three distinct rows are selected. A value-equality HashSet would have
        // collapsed the two Guest rows to one entry and mis-driven the count.
        var headerCheckbox = cut.Find("thead [role=checkbox]");
        Assert.Equal("mixed", headerCheckbox.GetAttribute("aria-checked"));
    }
}
