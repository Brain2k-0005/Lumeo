using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.SortableList;

public class SortableListTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SortableListTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_default_with_items()
    {
        var items = new List<string> { "Alpha", "Beta", "Gamma" };
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.SortableList<string>>(0);
            builder.AddAttribute(1, "Items", items);
            builder.AddAttribute(2, "ItemTemplate", (RenderFragment<string>)(item => b =>
            {
                b.AddContent(0, item);
            }));
            builder.CloseComponent();
        });
        Assert.Contains("Alpha", cut.Markup);
        Assert.Contains("Beta", cut.Markup);
        Assert.Contains("Gamma", cut.Markup);
    }

    [Fact]
    public void Merges_class_parameter()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.SortableList<string>>(0);
            builder.AddAttribute(1, "Items", new List<string>());
            builder.AddAttribute(2, "Class", "sortable-cls");
            builder.CloseComponent();
        });
        Assert.Contains("sortable-cls", cut.Markup);
    }

    [Fact]
    public void Forwards_additional_attributes()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.SortableList<string>>(0);
            builder.AddAttribute(1, "Items", new List<string>());
            builder.AddAttribute(2, "AdditionalAttributes", new Dictionary<string, object>
            {
                ["data-testid"] = "sortable"
            });
            builder.CloseComponent();
        });
        Assert.Contains("data-testid=\"sortable\"", cut.Markup);
    }

    [Fact]
    public void Items_are_draggable_when_not_disabled()
    {
        var items = new List<string> { "Item1", "Item2" };
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.SortableList<string>>(0);
            builder.AddAttribute(1, "Items", items);
            builder.AddAttribute(2, "ItemTemplate", (RenderFragment<string>)(item => b =>
            {
                b.AddContent(0, item);
            }));
            builder.CloseComponent();
        });
        var draggableDivs = cut.FindAll("[draggable='true']");
        Assert.Equal(2, draggableDivs.Count);
    }

    private static RenderFragment<string> TextTemplate =>
        item => b => b.AddContent(0, item);

    // Bug #4 (high, state-on-data-change): an in-flight drag captures _dragFromIndex /
    // _dragOverIndex against the OLD list. When the parent refreshes Items with a NEW
    // instance of the SAME length but DIFFERENT content, _items is rebuilt while the
    // drag indices stay in range — so they now address a different item. The dragged
    // highlight (opacity-50) and drag-over highlight (border-primary) would survive
    // onto the WRONG rows and a subsequent OnDrop would move the wrong item. (The
    // existing #21 shrink guard only fires when the indices fall OUT of range, so the
    // same-length case slips through.) A refresh mid-drag must abort the drag: both
    // indices reset to -1.
    [Fact]
    public async Task Same_length_items_refresh_mid_drag_aborts_drag_and_clears_highlight()
    {
        var cut = _ctx.Render<L.SortableList<string>>(p => p
            .Add(l => l.Items, new List<string> { "Alpha", "Beta", "Gamma" })
            .Add(l => l.ItemTemplate, TextTemplate));

        // Begin a drag on the first row (sets _dragFromIndex=0) and hover the third
        // row (sets _dragOverIndex=2). Re-query inside each InvokeAsync: DragStart
        // re-renders, which invalidates a stale element's event-handler id.
        await cut.InvokeAsync(() => cut.FindAll("[data-sortable-item]")[0].DragStart());
        await cut.InvokeAsync(() => cut.FindAll("[data-sortable-item]")[2].DragOver());

        // Drag highlights are now applied to the correct rows.
        var dragging = cut.FindAll("[data-sortable-item]");
        Assert.Contains("opacity-50", dragging[0].GetAttribute("class"));
        Assert.Contains("border-primary", dragging[2].GetAttribute("class"));

        // Parent refreshes Items mid-drag with a NEW, same-length list of DIFFERENT
        // content (a genuine replacement: new instance, content differs).
        cut.Render(p => p
            .Add(l => l.Items, new List<string> { "X1", "X2", "X3" })
            .Add(l => l.ItemTemplate, TextTemplate));

        // The drag must be aborted: no row retains the dragged/drag-over highlight.
        var afterRows = cut.FindAll("[data-sortable-item]");
        Assert.Equal(3, afterRows.Count);
        foreach (var row in afterRows)
        {
            var cls = row.GetAttribute("class")!;
            Assert.DoesNotContain("opacity-50", cls);
            Assert.DoesNotContain("border-primary", cls);
        }
        // New content rendered.
        Assert.Contains("X1", cut.Markup);
        Assert.Contains("X2", cut.Markup);
        Assert.Contains("X3", cut.Markup);
    }

    // Bug #40 (medium, keyboard-a11y): rows exposed no list/listitem ARIA semantics,
    // handles had a generic "Drag handle" label with no position/count and no
    // aria-keyshortcuts hint, and there was no aria-live region for reorders. Assert
    // the static ARIA markup that the fix introduces (no real DOM focus assertions).
    [Fact]
    public void Exposes_list_listitem_roles_and_contextual_handle_labels()
    {
        var cut = _ctx.Render<L.SortableList<string>>(p => p
            .Add(l => l.Items, new List<string> { "Alpha", "Beta", "Gamma" })
            .Add(l => l.ItemTemplate, TextTemplate));

        // Container is a list; each row is a listitem.
        var list = cut.Find("[role='list']");
        Assert.NotNull(list);
        var listItems = cut.FindAll("[role='listitem']");
        Assert.Equal(3, listItems.Count);

        // Each handle carries a contextual aria-label with its 1-based position and
        // the list count, plus an aria-keyshortcuts hint for the reorder keys.
        var handles = cut.FindAll("[role='button'][aria-keyshortcuts]");
        Assert.Equal(3, handles.Count);
        foreach (var handle in handles)
        {
            Assert.Equal("ArrowUp ArrowDown Home End", handle.GetAttribute("aria-keyshortcuts"));
        }
        // Default English labels: "Reorder item {n} of {count}, use arrow keys".
        Assert.Contains("1 of 3", handles[0].GetAttribute("aria-label"));
        Assert.Contains("3 of 3", handles[2].GetAttribute("aria-label"));

        // A polite live region exists for reorder announcements (initially empty).
        var live = cut.Find("[aria-live='polite']");
        Assert.NotNull(live);
        Assert.Equal(string.Empty, live.TextContent.Trim());
    }

    // Bug #40: a keyboard reorder must update the visually-hidden aria-live region so
    // screen readers hear the move. Without the fix there is no live region at all and
    // the move is silent.
    [Fact]
    public async Task Keyboard_reorder_announces_move_in_live_region()
    {
        var cut = _ctx.Render<L.SortableList<string>>(p => p
            .Add(l => l.Items, new List<string> { "Alpha", "Beta", "Gamma" })
            .Add(l => l.ItemTemplate, TextTemplate));

        // Live region starts empty.
        Assert.Equal(string.Empty, cut.Find("[aria-live='polite']").TextContent.Trim());

        // Press ArrowDown on the first row's handle to move "Alpha" from position 1 to 2.
        await cut.InvokeAsync(() =>
            cut.FindAll("[role='button'][aria-keyshortcuts]")[0]
                .KeyDown(new KeyboardEventArgs { Key = "ArrowDown" }));

        // The polite live region now announces the move with the moved item and its
        // new 1-based position within the list ("Moved Alpha to position 2 of 3").
        var announcement = cut.Find("[aria-live='polite']").TextContent;
        Assert.Contains("Alpha", announcement);
        Assert.Contains("2 of 3", announcement);
    }
}
