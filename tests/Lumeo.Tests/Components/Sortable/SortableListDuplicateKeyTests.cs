using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;

namespace Lumeo.Tests.Components.Sortable;

/// <summary>
/// Regression tests for triage #214 (edge-data, low):
/// "Default @key uses the item reference, which collides for duplicate values
/// (e.g. duplicate strings), breaking Blazor diffing and corrupting reorder/render."
///
/// With no Key selector supplied, the old fallback keyed every row by the raw item
/// reference. For a list containing duplicate values (two equal strings interned to
/// the same reference, or any repeated value type) two sibling rows received the SAME
/// @key, which makes Blazor's diff throw ("more than one sibling with the same key")
/// on render. The fix disambiguates duplicate keys by appending the per-value
/// occurrence index, so every sibling key is distinct while the unique-list path is
/// unchanged.
/// </summary>
public class SortableListDuplicateKeyTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SortableListDuplicateKeyTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static RenderFragment<string> TextTemplate =>
        item => builder => builder.AddContent(0, item);

    [Fact]
    public void Duplicate_Values_Without_Key_Selector_Render_Without_Throwing()
    {
        // No Key selector + duplicate values. Pre-fix the two "A" rows received the
        // same @key and Blazor's diff threw on render; post-fix each gets a distinct
        // composite key.
        var ex = Record.Exception(() =>
            _ctx.Render<Lumeo.SortableList<string>>(p => p
                .Add(l => l.Items, new List<string> { "A", "A", "B" })
                .Add(l => l.ItemTemplate, TextTemplate)));

        Assert.Null(ex);
    }

    [Fact]
    public void Duplicate_Values_Without_Key_Selector_Render_All_Rows()
    {
        var cut = _ctx.Render<Lumeo.SortableList<string>>(p => p
            .Add(l => l.Items, new List<string> { "A", "A", "B" })
            .Add(l => l.ItemTemplate, TextTemplate));

        // All three rows must be present (no row dropped/merged by a key collision).
        var rows = cut.FindAll("[data-sortable-item]");
        Assert.Equal(3, rows.Count);
        Assert.Equal(new[] { "A", "A", "B" }, rows.Select(r => r.TextContent.Trim()).ToArray());
    }

    [Fact]
    public void ReRender_With_New_Instance_Of_Duplicate_Values_Does_Not_Throw()
    {
        // A subsequent render that still carries duplicate values (a fresh instance,
        // forcing OnParametersSet to rebuild _items) must also diff cleanly.
        var cut = _ctx.Render<Lumeo.SortableList<string>>(p => p
            .Add(l => l.Items, new List<string> { "A", "A", "B" })
            .Add(l => l.ItemTemplate, TextTemplate));

        var ex = Record.Exception(() =>
            cut.Render(p => p
                .Add(l => l.Items, new List<string> { "A", "A", "A" })
                .Add(l => l.ItemTemplate, TextTemplate)));

        Assert.Null(ex);
        Assert.Equal(3, cut.FindAll("[data-sortable-item]").Count);
    }
}
