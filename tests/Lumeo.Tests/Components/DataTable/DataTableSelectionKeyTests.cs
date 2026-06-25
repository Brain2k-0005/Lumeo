using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.DataTable;

/// <summary>
/// Regression for battle-test finding #1 (high, state-on-data-change):
/// selection silently breaks when <c>TItem</c> is a plain reference type and
/// <c>Items</c> is refreshed with new (value-equal but reference-distinct)
/// instances. The <c>ItemKey</c> selector decides membership by stable key so
/// the highlight survives a same-content refresh.
/// </summary>
public class DataTableSelectionKeyTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataTableSelectionKeyTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // PLAIN reference type (class, NOT a record) — HashSet<Person> therefore
    // compares by object reference, which is exactly the broken case.
    private sealed class Person
    {
        public required int Id { get; init; }
        public required string Name { get; init; }
    }

    private static List<Person> MakeData() =>
    [
        new() { Id = 1, Name = "Alice" },
        new() { Id = 2, Name = "Bob" },
        new() { Id = 3, Name = "Charlie" },
    ];

    private IRenderedComponent<L.DataTable<Person>> RenderTable(
        List<Person> items,
        HashSet<Person> selected,
        Func<Person, object>? itemKey)
    {
        return _ctx.Render<L.DataTable<Person>>(builder =>
        {
            builder.OpenComponent<L.DataTable<Person>>(0);
            builder.AddAttribute(1, "Items", items);
            builder.AddAttribute(2, "Selectable", true);
            builder.AddAttribute(3, "SelectedItems", selected);
            if (itemKey is not null) builder.AddAttribute(4, "ItemKey", itemKey);
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
    public void Selection_Survives_SameContent_Items_Refresh_When_ItemKey_Supplied()
    {
        var data = MakeData();
        // Select Alice (Id=1) — by the ORIGINAL instance reference.
        var selected = new HashSet<Person> { data[0] };

        var cut = RenderTable(data, selected, itemKey: p => p.Id);

        // Sanity: first row is selected before the refresh.
        Assert.Equal("true", cut.FindAll("tbody tr")[0].GetAttribute("aria-selected"));

        // Parent re-fetches: brand-new Person instances, same Id/Name content.
        // These are reference-distinct, so a reference-keyed HashSet would now
        // report the row as NOT selected.
        var refreshed = MakeData();
        cut.Render(p =>
        {
            p.Add(x => x.Items, refreshed);
            // SelectedItems still holds the ORIGINAL instance(s).
            p.Add(x => x.SelectedItems, selected);
            p.Add(x => x.ItemKey, (Func<Person, object>)(person => person.Id));
        });

        // With the key selector, Alice's row stays selected across the refresh.
        var rows = cut.FindAll("tbody tr");
        Assert.Equal("true", rows[0].GetAttribute("aria-selected"));
        Assert.Equal("false", rows[1].GetAttribute("aria-selected"));
        Assert.Equal("false", rows[2].GetAttribute("aria-selected"));
    }

    [Fact]
    public void Without_ItemKey_SameContent_Refresh_Drops_The_Highlight()
    {
        // Documents the pre-fix behaviour: with a plain reference type and no
        // ItemKey, a same-content refresh breaks the highlight (reference compare).
        var data = MakeData();
        var selected = new HashSet<Person> { data[0] };

        var cut = RenderTable(data, selected, itemKey: null);
        Assert.Equal("true", cut.FindAll("tbody tr")[0].GetAttribute("aria-selected"));

        var refreshed = MakeData();
        cut.Render(p => p.Add(x => x.Items, refreshed));

        // New instances are reference-distinct → no longer reported as selected.
        Assert.Equal("false", cut.FindAll("tbody tr")[0].GetAttribute("aria-selected"));
    }

    [Fact]
    public void ItemKey_Toggle_Targets_The_Clicked_Row_By_Key()
    {
        var data = MakeData();
        var selected = new HashSet<Person>();
        HashSet<Person>? emitted = null;
        var cb = EventCallback.Factory.Create<HashSet<Person>>(this, s => emitted = s);

        var cut = _ctx.Render<L.DataTable<Person>>(builder =>
        {
            builder.OpenComponent<L.DataTable<Person>>(0);
            builder.AddAttribute(1, "Items", data);
            builder.AddAttribute(2, "Selectable", true);
            builder.AddAttribute(3, "SelectedItems", selected);
            builder.AddAttribute(4, "SelectedItemsChanged", cb);
            builder.AddAttribute(5, "ItemKey", (Func<Person, object>)(p => p.Id));
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

        // Click the SECOND row's checkbox (Bob, Id=2).
        var checkboxes = cut.FindAll("tbody tr td [role=checkbox]");
        checkboxes[1].Click();

        Assert.NotNull(emitted);
        Assert.Single(emitted!);
        Assert.Equal(2, emitted!.Single().Id);
    }
}
