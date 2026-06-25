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
}
