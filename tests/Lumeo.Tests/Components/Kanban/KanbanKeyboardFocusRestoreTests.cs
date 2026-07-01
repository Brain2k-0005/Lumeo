using AngleSharp.Dom;
using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Lumeo.Tests.Components.Kanban;

/// <summary>
/// Regression for triage #41 (Kanban, state-on-data-change, medium) —
/// "Keyboard-move focus restoration lands on the WRONG card when consumers
/// render cards without @key (the documented pattern)".
///
/// Repro: a column whose cards are rendered from a backing list WITHOUT @key
/// (see <see cref="KanbanFocusRestoreHost"/>). The user focuses a card and
/// presses an arrow key; the board raises OnCardMove and the consumer reorders
/// its list. Because the cards carry no @key, Blazor reuses the KanbanCard
/// component instances POSITIONALLY — so the generated per-instance element id
/// no longer travels with the moved card's data. The old code keyed the
/// post-move focus restoration by that element id, so focus landed on whichever
/// card slid into the moved card's old slot instead of the card the user moved.
///
/// The fix keys the focus request by the STABLE consumer CardId (which DOES
/// travel with the data): the instance whose CardId matches re-focuses its OWN
/// live element id, so the moved card regains focus regardless of positional
/// diffing. FocusElement() is recorded by <see cref="TrackingInteropService"/>,
/// so the assertion is against the recorded interop focus call — no real DOM
/// focus is asserted.
/// </summary>
public class KanbanKeyboardFocusRestoreTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public KanbanKeyboardFocusRestoreTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IElement Card(IRenderedComponent<KanbanFocusRestoreHost> cut, string title)
        => cut.FindAll("[draggable]").First(e => e.TextContent.Contains(title));

    // The element id passed to the LAST recorded Interop.FocusElement call —
    // i.e. the card the board re-focused after the move.
    private string? LastFocusedId => _interop.FocusElementCalls.Count > 0
        ? _interop.FocusElementCalls[^1]
        : null;

    [Fact]
    public async Task ArrowDown_restores_focus_to_the_moved_card_when_cards_have_no_key()
    {
        var cut = _ctx.Render<KanbanFocusRestoreHost>();

        // Focus is on "Card A" (position 0). ArrowDown asks the board to move it
        // down; the consumer reorders to [Card B, Card A] and the cards re-render
        // with positionally-reused instances (no @key).
        await cut.InvokeAsync(() => Card(cut, "Card A").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" }));

        // The board restored focus exactly once, to a real card element.
        cut.WaitForAssertion(() => Assert.NotNull(LastFocusedId));

        // THE DISCRIMINATOR: the focused element must be the card the user moved
        // ("Card A"), not the card that slid into its old slot ("Card B"). Under
        // the bug the recorded id keyed to the moved card's OLD slot, which after
        // the reorder renders "Card B".
        var focused = cut.Find($"#{LastFocusedId}");
        Assert.Contains("Card A", focused.TextContent);
        Assert.DoesNotContain("Card B", focused.TextContent);
    }

    [Fact]
    public async Task ArrowUp_restores_focus_to_the_moved_card_when_cards_have_no_key()
    {
        var cut = _ctx.Render<KanbanFocusRestoreHost>();

        // Focus "Card B" (position 1) and move it UP; the consumer reorders to
        // [Card B, Card A]. The moved card (B) must regain focus.
        await cut.InvokeAsync(() => Card(cut, "Card B").KeyDown(new KeyboardEventArgs { Key = "ArrowUp" }));

        cut.WaitForAssertion(() => Assert.NotNull(LastFocusedId));

        var focused = cut.Find($"#{LastFocusedId}");
        Assert.Contains("Card B", focused.TextContent);
        Assert.DoesNotContain("Card A", focused.TextContent);
    }
}
