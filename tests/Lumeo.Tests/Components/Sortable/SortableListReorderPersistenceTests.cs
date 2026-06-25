using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Lumeo.Tests.Components.Sortable;

/// <summary>
/// Regression tests for triage #144 (state-on-data-change, medium):
/// "OnParametersSet unconditionally rebuilds _items from Items on EVERY render,
/// silently reverting an applied reorder when the parent re-renders without (yet)
/// propagating the new order."
///
/// A keyboard/drag reorder mutates the internal _items list in place. The old code
/// then ran <c>_items = Items.ToList()</c> on every subsequent OnParametersSet, so any
/// unrelated parent re-render that still carried the original (un-propagated) Items
/// instance snapped the order back to its pre-reorder state.
///
/// The fix tracks the last *parameter* Items instance (_lastItems) and only rebuilds
/// _items when a genuinely different instance arrives whose content also differs from
/// the current order; the reorder deliberately does not touch _lastItems. This mirrors
/// Gantt's _lastSeenViewMode and Collapsible's last-param tracking.
/// </summary>
public class SortableListReorderPersistenceTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SortableListReorderPersistenceTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static RenderFragment<string> TextTemplate =>
        item => builder => builder.AddContent(0, item);

    // The rendered order of the items, read back from the data-sortable-item rows.
    private static List<string> RenderedOrder(IRenderedComponent<Lumeo.SortableList<string>> cut)
        => cut.FindAll("[data-sortable-item]").Select(el => el.TextContent.Trim()).ToList();

    [Fact]
    public async Task AppliedReorder_Survives_ParentReRender_With_Same_Items_Instance()
    {
        // Uncontrolled-style usage: the parent holds ONE Items instance and does not
        // (yet) reassign it when ItemsChanged fires.
        var list = new List<string> { "A", "B", "C" };
        var changed = new List<List<string>>();

        var cut = _ctx.Render<Lumeo.SortableList<string>>(p => p
            .Add(l => l.Items, list)
            .Add(l => l.ItemTemplate, TextTemplate)
            .Add(l => l.ItemsChanged,
                EventCallback.Factory.Create<List<string>>(this, v => changed.Add(v))));

        // Keyboard-reorder the first item ("A") down one slot: A,B,C -> B,A,C.
        var firstHandle = cut.FindAll("[role='button']")[0];
        await cut.InvokeAsync(() => firstHandle.KeyDown(new KeyboardEventArgs { Key = "ArrowDown" }));

        Assert.Equal(new List<string> { "B", "A", "C" }, RenderedOrder(cut));
        // The reorder did emit the new order to the parent...
        Assert.Single(changed);
        Assert.Equal(new List<string> { "B", "A", "C" }, changed[0]);

        // ...but the parent re-renders carrying the SAME original Items instance
        // (it has not propagated the new order back). Pre-fix this rebuilt _items
        // from the stale parameter and reverted the order to A,B,C; post-fix the
        // unchanged-instance is recognised and the applied order is preserved.
        cut.Render(p => p
            .Add(l => l.Items, list)
            .Add(l => l.ItemTemplate, TextTemplate)
            .Add(l => l.ItemsChanged,
                EventCallback.Factory.Create<List<string>>(this, v => changed.Add(v))));

        Assert.Equal(new List<string> { "B", "A", "C" }, RenderedOrder(cut));
    }

    [Fact]
    public async Task AppliedReorder_Survives_Unrelated_ParentReRender_That_Only_Changes_Another_Param()
    {
        // Same scenario, but the re-render flips an unrelated parameter (Class) while
        // keeping the SAME Items instance — the canonical "unrelated parent re-render".
        var list = new List<string> { "A", "B", "C" };

        var cut = _ctx.Render<Lumeo.SortableList<string>>(p => p
            .Add(l => l.Items, list)
            .Add(l => l.ItemTemplate, TextTemplate));

        var firstHandle = cut.FindAll("[role='button']")[0];
        await cut.InvokeAsync(() => firstHandle.KeyDown(new KeyboardEventArgs { Key = "End" }));

        // A,B,C -> B,C,A (move first item to the end).
        Assert.Equal(new List<string> { "B", "C", "A" }, RenderedOrder(cut));

        // Unrelated re-render: only Class changes, Items is the same instance.
        cut.Render(p => p
            .Add(l => l.Items, list)
            .Add(l => l.ItemTemplate, TextTemplate)
            .Add(l => l.Class, "mt-4"));

        Assert.Equal(new List<string> { "B", "C", "A" }, RenderedOrder(cut));
    }

    [Fact]
    public async Task External_Items_Replacement_Still_Drives_The_List()
    {
        // Guard: the fix must NOT freeze the component to the order it last held.
        // A genuinely new Items instance (external replacement, or a @bind-Items echo)
        // must still re-sync the rendered order.
        var list = new List<string> { "A", "B", "C" };

        var cut = _ctx.Render<Lumeo.SortableList<string>>(p => p
            .Add(l => l.Items, list)
            .Add(l => l.ItemTemplate, TextTemplate));

        var firstHandle = cut.FindAll("[role='button']")[0];
        await cut.InvokeAsync(() => firstHandle.KeyDown(new KeyboardEventArgs { Key = "ArrowDown" }));
        Assert.Equal(new List<string> { "B", "A", "C" }, RenderedOrder(cut));

        // Parent replaces Items with a brand-new, different-content instance.
        cut.Render(p => p
            .Add(l => l.Items, new List<string> { "X", "Y", "Z" })
            .Add(l => l.ItemTemplate, TextTemplate));

        Assert.Equal(new List<string> { "X", "Y", "Z" }, RenderedOrder(cut));
    }
}
