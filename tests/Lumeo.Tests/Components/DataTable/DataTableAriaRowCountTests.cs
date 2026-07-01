using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.DataTable;

/// <summary>
/// Regression tests for battle-wave1 #64: aria-rowcount must match the role=row
/// elements actually rendered, not the provisional Items count. Previously the
/// grid reported <c>Items.Count + 1</c> unconditionally, so while loading it
/// announced the (not-yet-shown) data count instead of the skeleton rows, and in
/// the empty state the "No results" placeholder was a phantom role=row/gridcell
/// that the rowcount never accounted for.
/// </summary>
public class DataTableAriaRowCountTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataTableAriaRowCountTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Person(string Name, int Age);

    private static readonly List<Person> Data = new()
    {
        new("Alice", 30),
        new("Bob", 25),
        new("Charlie", 35),
    };

    private IRenderedComponent<L.DataTable<Person>> RenderTable(
        IEnumerable<Person>? items,
        bool isLoading = false,
        int skeletonRows = 5)
    {
        return _ctx.Render<L.DataTable<Person>>(builder =>
        {
            builder.OpenComponent<L.DataTable<Person>>(0);
            builder.AddAttribute(1, "Items", items);
            builder.AddAttribute(2, "IsLoading", isLoading);
            builder.AddAttribute(3, "SkeletonRows", skeletonRows);
            builder.AddAttribute(4, "HeaderTemplate", (RenderFragment)(h =>
            {
                h.OpenElement(0, "th"); h.AddContent(1, "Name"); h.CloseElement();
            }));
            builder.AddAttribute(5, "RowTemplate", (RenderFragment<Person>)(p => rb =>
            {
                rb.OpenElement(0, "td"); rb.AddContent(1, p.Name); rb.CloseElement();
            }));
            builder.AddAttribute(6, "LoadingTemplate", (RenderFragment)(l =>
            {
                l.OpenElement(0, "td"); l.AddContent(1, "…"); l.CloseElement();
            }));
            builder.CloseComponent();
        });
    }

    // --- Loading state: count the skeleton rows, not the Items count ---

    [Fact]
    public void AriaRowCount_While_Loading_Counts_Skeleton_Rows_Not_Items()
    {
        // Items has 3 entries but IsLoading renders 4 skeleton rows. The grid must
        // announce the rendered shape (4 skeleton + 1 header = 5), NOT Items+1 (4).
        var cut = RenderTable(Data, isLoading: true, skeletonRows: 4);

        var table = cut.Find("table");
        Assert.Equal("5", table.GetAttribute("aria-rowcount"));

        // Sanity: the rendered body rows really are the 4 skeleton rows.
        var bodyRows = cut.FindAll("tbody tr");
        Assert.Equal(4, bodyRows.Count);
    }

    // --- Empty state: header only, and the placeholder is not a grid row/cell ---

    [Fact]
    public void AriaRowCount_When_Empty_Is_Header_Only()
    {
        var cut = RenderTable(new List<Person>());
        // Only the header row exists as a real grid row.
        Assert.Equal("1", cut.Find("table").GetAttribute("aria-rowcount"));
    }

    [Fact]
    public void Empty_Placeholder_Carries_No_Grid_Roles()
    {
        var cut = RenderTable(new List<Person>());

        // The "No results" placeholder must not masquerade as a grid row/cell —
        // otherwise it is a phantom row that aria-rowcount does not count,
        // misannouncing the grid shape to assistive tech.
        var placeholderCell = cut.Find("tbody td");
        Assert.Null(placeholderCell.GetAttribute("role"));
        Assert.Null(placeholderCell.ParentElement!.GetAttribute("role"));
    }

    [Fact]
    public void AriaRowCount_When_Items_Null_Is_Header_Only()
    {
        var cut = RenderTable(items: null);
        Assert.Equal("1", cut.Find("table").GetAttribute("aria-rowcount"));
    }

    // --- Normal state regression: header + data rows still correct ---

    [Fact]
    public void AriaRowCount_With_Data_Is_DataRows_Plus_Header()
    {
        var cut = RenderTable(Data);
        // 3 data rows + 1 header row.
        Assert.Equal("4", cut.Find("table").GetAttribute("aria-rowcount"));
    }
}
