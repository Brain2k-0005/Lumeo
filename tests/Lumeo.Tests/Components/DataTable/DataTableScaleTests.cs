using System.Diagnostics;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.DataTable;

/// <summary>
/// Enterprise-scale "battle test" for <see cref="L.DataTable{TItem}"/>: with
/// <c>Virtualize</c> enabled a large item set must not put every row in the DOM.
/// (The live viewport-driven windowing is a Playwright E2E concern — bUnit's
/// headless DOM does not run the IntersectionObserver — so here we assert the
/// large set is accepted and rendered within a CI-safe budget without putting all
/// rows in the markup.)
/// </summary>
public class DataTableScaleTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public DataTableScaleTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Row(int Id, string Name);

    private IRenderedComponent<IComponent> RenderTable(IEnumerable<Row> items, bool virtualize)
        => _ctx.Render(builder =>
        {
            builder.OpenComponent<L.DataTable<Row>>(0);
            builder.AddAttribute(1, "Items", items);
            builder.AddAttribute(2, "Virtualize", virtualize);
            builder.AddAttribute(3, "HeaderTemplate", (RenderFragment)(h =>
            {
                h.OpenElement(0, "th"); h.AddContent(1, "Id"); h.CloseElement();
                h.OpenElement(2, "th"); h.AddContent(3, "Name"); h.CloseElement();
            }));
            builder.AddAttribute(4, "RowTemplate", (RenderFragment<Row>)(item => rb =>
            {
                rb.OpenElement(0, "td"); rb.AddContent(1, item.Id.ToString()); rb.CloseElement();
                rb.OpenElement(2, "td"); rb.AddContent(3, item.Name); rb.CloseElement();
            }));
            builder.CloseComponent();
        });

    [Fact]
    public void Virtualized_datatable_accepts_a_large_set_within_budget_without_rendering_all_rows()
    {
        const int count = 100_000;
        var data = Enumerable.Range(0, count).Select(i => new Row(i, $"Row {i}")).ToList();

        var sw = Stopwatch.StartNew();
        var cut = RenderTable(data, virtualize: true);
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 10_000,
            $"Virtualized render of {count} rows took {sw.ElapsedMilliseconds}ms.");
        var rows = cut.FindAll("tbody tr");
        Assert.True(rows.Count < count,
            $"Virtualize put {rows.Count} of {count} rows in the DOM — not windowing.");
    }
}
