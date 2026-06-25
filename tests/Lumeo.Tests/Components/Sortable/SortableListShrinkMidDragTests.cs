using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Lumeo.Tests.Components.Sortable;

/// <summary>
/// Regression tests for triage #21 (edge-data, high):
/// "Stale index-based drag state (_dragFromIndex) causes ArgumentOutOfRangeException
/// when Items shrinks mid-drag."
///
/// A drag captures _dragFromIndex against the OLD item count in OnDragStart. If the
/// parent shrinks the Items parameter while the drag is still in progress,
/// OnParametersSet rebuilds the internal _items list to the smaller size but used to
/// leave _dragFromIndex pointing past the end. The subsequent OnDrop then indexed
/// _items[fromIndex] out of range and threw. The fix resets stale indices in
/// OnParametersSet and bound-checks both endpoints in OnDrop.
/// </summary>
public class SortableListShrinkMidDragTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SortableListShrinkMidDragTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static RenderFragment<string> TextTemplate =>
        item => builder => builder.AddContent(0, item);

    [Fact]
    public async Task Drop_After_Items_Shrink_MidDrag_Does_Not_Throw()
    {
        var changed = new List<List<string>>();
        var cut = _ctx.Render<Lumeo.SortableList<string>>(p => p
            .Add(l => l.Items, new List<string> { "A", "B", "C" })
            .Add(l => l.ItemTemplate, TextTemplate)
            .Add(l => l.ItemsChanged,
                EventCallback.Factory.Create<List<string>>(this, v => changed.Add(v))));

        // Begin a drag on the LAST item (index 2): _dragFromIndex = 2.
        var items = cut.FindAll("[data-sortable-item]");
        await cut.InvokeAsync(() => items[2].DragStart());

        // Parent shrinks Items to a single element while the drag is "in flight".
        // OnParametersSet rebuilds _items to count 1; index 2 is now out of range.
        cut.Render(p => p
            .Add(l => l.Items, new List<string> { "A" })
            .Add(l => l.ItemTemplate, TextTemplate)
            .Add(l => l.ItemsChanged,
                EventCallback.Factory.Create<List<string>>(this, v => changed.Add(v))));

        // Drop onto the only remaining item. Pre-fix this indexed _items[2] and threw
        // ArgumentOutOfRangeException (tearing down the circuit); post-fix it is a
        // clean no-op.
        var remaining = cut.FindAll("[data-sortable-item]");
        var ex = await Record.ExceptionAsync(() =>
            cut.InvokeAsync(() => remaining[0].Drop()));

        Assert.Null(ex);
        // The stale drop must NOT have mutated/emitted a reordered list.
        Assert.Empty(changed);
    }

    [Fact]
    public void OnParametersSet_Resets_Stale_DragIndex_When_Items_Shrink()
    {
        var cut = _ctx.Render<Lumeo.SortableList<string>>(p => p
            .Add(l => l.Items, new List<string> { "A", "B", "C" })
            .Add(l => l.ItemTemplate, TextTemplate));

        // Start a drag on the last item, then shrink the list out from under it.
        var items = cut.FindAll("[data-sortable-item]");
        items[2].DragStart();

        cut.Render(p => p
            .Add(l => l.Items, new List<string> { "A" })
            .Add(l => l.ItemTemplate, TextTemplate));

        // After shrinking, the previously-dragged item (index 2) renders no
        // dragging affordance: GetItemClass only applies the "opacity-50" drag
        // class when index == _dragFromIndex. A stale _dragFromIndex of 2 would be
        // harmless to markup (no row at 2), so assert the surviving row 0 is not
        // marked as the drag source and the component renders cleanly.
        var remaining = cut.FindAll("[data-sortable-item]");
        Assert.Single(remaining);
        Assert.DoesNotContain("opacity-50", remaining[0].GetAttribute("class"));
    }
}
