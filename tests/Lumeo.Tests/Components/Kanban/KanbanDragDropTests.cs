using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Kanban;

/// <summary>
/// Kanban drag-drop now emits an actionable payload (which card, from→to
/// column, target index) via Kanban.OnCardDrop, and offers a keyboard move
/// alternative (arrow keys) via Kanban.OnCardMove. Previously OnDrop forwarded
/// only the raw browser event and cards wrote no drag data — presentational only.
/// </summary>
public class KanbanDragDropTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public KanbanDragDropTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // Board: column "todo" with card "c1" (index 0); column "done" (empty).
    private static RenderFragment Board(
        EventCallback<L.Kanban.DropEventArgs>? onDrop = null,
        EventCallback<L.Kanban.MoveEventArgs>? onMove = null,
        EventCallback<DragEventArgs>? onRawDrop = null) => builder =>
    {
        builder.OpenComponent<L.Kanban>(0);
        if (onDrop is { } d) builder.AddAttribute(1, nameof(L.Kanban.OnCardDrop), d);
        if (onMove is { } m) builder.AddAttribute(2, nameof(L.Kanban.OnCardMove), m);
        builder.AddAttribute(3, "ChildContent", (RenderFragment)(cb =>
        {
            // todo column
            cb.OpenComponent<L.KanbanColumn>(0);
            cb.AddAttribute(1, nameof(L.KanbanColumn.ColumnId), "todo");
            if (onRawDrop is { } r) cb.AddAttribute(2, nameof(L.KanbanColumn.OnDrop), r);
            cb.AddAttribute(3, "ChildContent", (RenderFragment)(ccb =>
            {
                ccb.OpenComponent<L.KanbanCard>(0);
                ccb.AddAttribute(1, nameof(L.KanbanCard.CardId), "c1");
                ccb.AddAttribute(2, nameof(L.KanbanCard.Index), 0);
                ccb.AddAttribute(3, nameof(L.KanbanCard.Title), "Card One");
                ccb.CloseComponent();
            }));
            cb.CloseComponent();

            // done column (empty)
            cb.OpenComponent<L.KanbanColumn>(4);
            cb.AddAttribute(5, nameof(L.KanbanColumn.ColumnId), "done");
            cb.AddAttribute(6, "ChildContent", (RenderFragment)(_ => { }));
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
    public void Card_with_id_in_board_with_move_handler_is_focusable()
    {
        var cut = _ctx.Render(Board(onMove: EventCallback.Factory.Create<L.Kanban.MoveEventArgs>(this, _ => { })));
        var card = Card(cut, "Card One");
        Assert.Equal("button", card.GetAttribute("role"));
        Assert.Equal("0", card.GetAttribute("tabindex"));
    }

    [Fact]
    public async Task Drop_on_empty_column_emits_enriched_payload()
    {
        L.Kanban.DropEventArgs? captured = null;
        var cut = _ctx.Render(Board(
            onDrop: EventCallback.Factory.Create<L.Kanban.DropEventArgs>(this, a => captured = a)));

        // Start dragging c1 from todo, then drop on the done column body.
        await cut.InvokeAsync(() => Card(cut, "Card One").DragStart());
        await cut.InvokeAsync(() => ColumnBody(cut, 1).Drop());

        Assert.NotNull(captured);
        Assert.Equal("c1", captured!.CardId);
        Assert.Equal("todo", captured.FromColumnId);
        Assert.Equal("done", captured.ToColumnId);
        // Dropped on the body (no card hovered) → append (int.MaxValue sentinel).
        Assert.Equal(int.MaxValue, captured.ToIndex);
    }

    [Fact]
    public async Task Drop_onto_a_card_targets_that_cards_index()
    {
        L.Kanban.DropEventArgs? captured = null;
        var cut = _ctx.Render(Board(
            onDrop: EventCallback.Factory.Create<L.Kanban.DropEventArgs>(this, a => captured = a)));

        await cut.InvokeAsync(() => Card(cut, "Card One").DragStart());
        // Hover c1 (index 0) then drop on todo body — the hovered card advertised index 0.
        await cut.InvokeAsync(() => Card(cut, "Card One").DragOver());
        await cut.InvokeAsync(() => ColumnBody(cut, 0).Drop());

        Assert.NotNull(captured);
        Assert.Equal("todo", captured!.ToColumnId);
        Assert.Equal(0, captured.ToIndex);
    }

    [Fact]
    public async Task Raw_OnDrop_still_fires_for_manual_boards()
    {
        var rawFired = false;
        var cut = _ctx.Render(Board(
            onRawDrop: EventCallback.Factory.Create<DragEventArgs>(this, _ => rawFired = true)));

        await cut.InvokeAsync(() => ColumnBody(cut, 0).Drop());

        Assert.True(rawFired);
    }

    [Theory]
    [InlineData("ArrowRight", L.Kanban.MoveDirection.Right)]
    [InlineData("ArrowLeft", L.Kanban.MoveDirection.Left)]
    [InlineData("ArrowUp", L.Kanban.MoveDirection.Up)]
    [InlineData("ArrowDown", L.Kanban.MoveDirection.Down)]
    public async Task Arrow_keys_on_card_emit_move_payload(string key, L.Kanban.MoveDirection expected)
    {
        L.Kanban.MoveEventArgs? captured = null;
        var cut = _ctx.Render(Board(
            onMove: EventCallback.Factory.Create<L.Kanban.MoveEventArgs>(this, a => captured = a)));

        await cut.InvokeAsync(() => Card(cut, "Card One").KeyDown(new KeyboardEventArgs { Key = key }));

        Assert.NotNull(captured);
        Assert.Equal("c1", captured!.CardId);
        Assert.Equal("todo", captured.FromColumnId);
        Assert.Equal(expected, captured.Direction);
    }

    [Fact]
    public async Task Enter_still_invokes_OnClick_when_card_also_movable()
    {
        var clicked = false;
        var moveCb = EventCallback.Factory.Create<L.Kanban.MoveEventArgs>(this, _ => { });
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Kanban>(0);
            builder.AddAttribute(1, nameof(L.Kanban.OnCardMove), moveCb);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(cb =>
            {
                cb.OpenComponent<L.KanbanColumn>(0);
                cb.AddAttribute(1, nameof(L.KanbanColumn.ColumnId), "todo");
                cb.AddAttribute(2, "ChildContent", (RenderFragment)(ccb =>
                {
                    ccb.OpenComponent<L.KanbanCard>(0);
                    ccb.AddAttribute(1, nameof(L.KanbanCard.CardId), "c1");
                    ccb.AddAttribute(2, nameof(L.KanbanCard.Title), "Card One");
                    ccb.AddAttribute(3, nameof(L.KanbanCard.OnClick),
                        EventCallback.Factory.Create(this, () => clicked = true));
                    ccb.CloseComponent();
                }));
                cb.CloseComponent();
            }));
            builder.CloseComponent();
        });

        await cut.InvokeAsync(() => Card(cut, "Card One").KeyDown(new KeyboardEventArgs { Key = "Enter" }));

        Assert.True(clicked);
    }
}
