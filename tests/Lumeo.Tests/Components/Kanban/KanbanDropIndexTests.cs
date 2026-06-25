using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Kanban;

/// <summary>
/// Regression for battle-test finding #72 (edge-data): the drop insert index used
/// to come from KanbanCard.Index, a consumer-supplied parameter that defaults to 0
/// and that the documented examples never set — so a drop onto ANY card always
/// targeted index 0 (effectively prepend) instead of that card's real position.
/// The column now derives the hovered card's index from its sibling order, so the
/// enriched ToIndex is correct even when Index is omitted (the documented usage).
/// </summary>
public class KanbanDropIndexTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public KanbanDropIndexTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // Board: "todo" column with three cards c1/c2/c3 rendered WITHOUT Index (the
    // documented pattern); a "src" column with a draggable card "dragged".
    private static RenderFragment Board(EventCallback<L.Kanban.DropEventArgs> onDrop) => builder =>
    {
        builder.OpenComponent<L.Kanban>(0);
        builder.AddAttribute(1, nameof(L.Kanban.OnCardDrop), onDrop);
        builder.AddAttribute(2, "ChildContent", (RenderFragment)(cb =>
        {
            // todo column — three cards, NO Index supplied on any of them.
            cb.OpenComponent<L.KanbanColumn>(0);
            cb.AddAttribute(1, nameof(L.KanbanColumn.ColumnId), "todo");
            cb.AddAttribute(2, "ChildContent", (RenderFragment)(ccb =>
            {
                ccb.OpenComponent<L.KanbanCard>(0);
                ccb.AddAttribute(1, nameof(L.KanbanCard.CardId), "c1");
                ccb.AddAttribute(2, nameof(L.KanbanCard.Title), "First");
                ccb.CloseComponent();

                ccb.OpenComponent<L.KanbanCard>(3);
                ccb.AddAttribute(4, nameof(L.KanbanCard.CardId), "c2");
                ccb.AddAttribute(5, nameof(L.KanbanCard.Title), "Second");
                ccb.CloseComponent();

                ccb.OpenComponent<L.KanbanCard>(6);
                ccb.AddAttribute(7, nameof(L.KanbanCard.CardId), "c3");
                ccb.AddAttribute(8, nameof(L.KanbanCard.Title), "Third");
                ccb.CloseComponent();
            }));
            cb.CloseComponent();

            // src column — the card we drag from.
            cb.OpenComponent<L.KanbanColumn>(9);
            cb.AddAttribute(10, nameof(L.KanbanColumn.ColumnId), "src");
            cb.AddAttribute(11, "ChildContent", (RenderFragment)(ccb =>
            {
                ccb.OpenComponent<L.KanbanCard>(0);
                ccb.AddAttribute(1, nameof(L.KanbanCard.CardId), "dragged");
                ccb.AddAttribute(2, nameof(L.KanbanCard.Title), "Dragged");
                ccb.CloseComponent();
            }));
            cb.CloseComponent();
        }));
        builder.CloseComponent();
    };

    private static IElement Card(IRenderedComponent<IComponent> cut, string title)
        => cut.FindAll("[draggable]").First(e => e.TextContent.Contains(title));

    // The drop zone is the column body div carrying min-h-16.
    private static IElement ColumnBody(IRenderedComponent<IComponent> cut, int index)
        => cut.FindAll(".min-h-16")[index];

    [Fact]
    public async Task Drop_onto_second_card_without_explicit_index_targets_index_one()
    {
        L.Kanban.DropEventArgs? captured = null;
        var cut = _ctx.Render(Board(
            EventCallback.Factory.Create<L.Kanban.DropEventArgs>(this, a => captured = a)));

        // Begin dragging "dragged" from the src column.
        await cut.InvokeAsync(() => Card(cut, "Dragged").DragStart());
        // Hover the SECOND card (c2) in the todo column, then drop on the todo body.
        await cut.InvokeAsync(() => Card(cut, "Second").DragOver());
        await cut.InvokeAsync(() => ColumnBody(cut, 0).Drop());

        Assert.NotNull(captured);
        Assert.Equal("dragged", captured!.CardId);
        Assert.Equal("todo", captured.ToColumnId);
        // Sibling order, not the unset Index default (0): the second card is at 1.
        Assert.Equal(1, captured.ToIndex);
    }

    [Fact]
    public async Task Drop_onto_third_card_without_explicit_index_targets_index_two()
    {
        L.Kanban.DropEventArgs? captured = null;
        var cut = _ctx.Render(Board(
            EventCallback.Factory.Create<L.Kanban.DropEventArgs>(this, a => captured = a)));

        await cut.InvokeAsync(() => Card(cut, "Dragged").DragStart());
        await cut.InvokeAsync(() => Card(cut, "Third").DragOver());
        await cut.InvokeAsync(() => ColumnBody(cut, 0).Drop());

        Assert.NotNull(captured);
        Assert.Equal(2, captured!.ToIndex);
    }
}
