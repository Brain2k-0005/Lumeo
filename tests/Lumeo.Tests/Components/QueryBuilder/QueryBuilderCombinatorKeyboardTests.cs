using AngleSharp.Dom;
using Bunit;
using Lumeo;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Lumeo.Tests.Components.QueryBuilder;

/// <summary>
/// Keyboard / a11y regression coverage for battle-test finding #52 against the
/// combinator radio group inside <see cref="Lumeo.QueryBuilder"/>'s group renderer.
///
/// #52 — the And/Or pills carry <c>role="radiogroup"</c> / <c>role="radio"</c> but
/// were wired with only <c>@onclick</c>: both buttons were in the tab order (no roving
/// tabindex) and there was no Arrow/Home/End handling, so the group failed the WAI-ARIA
/// radio-group keyboard contract. The fix applies a roving tabindex (selected radio
/// <c>tabindex=0</c>, the other <c>tabindex=-1</c>), Arrow/Home/End selection, and a
/// programmatic focus move to the newly-selected radio via <see cref="IComponentInteropService.FocusElement"/>.
///
/// Per the keyboard-a11y test rules, bUnit cannot move real DOM focus, so these assert
/// the OBSERVABLE mechanism: the roving <c>tabindex</c> + <c>aria-checked</c> in the
/// rendered markup after a KeyDown, and the recorded <c>FocusElement</c> interop call —
/// never document.activeElement. Mirrors DropdownButtonKeyboardAriaTests (same
/// loose-but-tracked interop, KeyDown dispatch, markup assertions).
/// </summary>
public class QueryBuilderCombinatorKeyboardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public QueryBuilderCombinatorKeyboardTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static List<QueryField> Fields() =>
    [
        new() { Name = "name", Label = "Name", Type = QueryFieldType.Text }
    ];

    private IRenderedComponent<Lumeo.QueryBuilder> Render()
        => _ctx.Render<Lumeo.QueryBuilder>(p => p.Add(q => q.Fields, Fields()));

    private static IElement And(IRenderedComponent<Lumeo.QueryBuilder> cut)
        => cut.FindAll("[role='radiogroup'] [role='radio']").First(r => r.TextContent.Trim() == "AND");

    private static IElement Or(IRenderedComponent<Lumeo.QueryBuilder> cut)
        => cut.FindAll("[role='radiogroup'] [role='radio']").First(r => r.TextContent.Trim() == "OR");

    // ---- roving tabindex: exactly one tab stop, on the selected radio ----

    [Fact]
    public void Combinator_Radios_Have_Roving_Tabindex_Selected_Is_The_Only_Tab_Stop()
    {
        var cut = Render();

        // Default combinator is And, so AND is the single tab stop and OR leaves the tab order.
        // Pre-fix neither button declared tabindex, so both were tabbable (two tab stops).
        Assert.Equal("0", And(cut).GetAttribute("tabindex"));
        Assert.Equal("-1", Or(cut).GetAttribute("tabindex"));
    }

    // ---- Arrow key moves the selection (aria-checked + roving tabindex) ----

    [Fact]
    public void ArrowRight_On_And_Selects_Or_And_Moves_The_Roving_Tabindex()
    {
        var cut = Render();
        Assert.Equal("true", And(cut).GetAttribute("aria-checked"));

        And(cut).KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        // Selection moved to OR: aria-checked and the single tab stop both follow.
        Assert.Equal("true", Or(cut).GetAttribute("aria-checked"));
        Assert.Equal("false", And(cut).GetAttribute("aria-checked"));
        Assert.Equal("0", Or(cut).GetAttribute("tabindex"));
        Assert.Equal("-1", And(cut).GetAttribute("tabindex"));
    }

    [Fact]
    public void ArrowLeft_On_Or_Selects_And()
    {
        var cut = Render();
        // Move to OR first.
        And(cut).KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });
        Assert.Equal("true", Or(cut).GetAttribute("aria-checked"));

        Or(cut).KeyDown(new KeyboardEventArgs { Key = "ArrowLeft" });

        Assert.Equal("true", And(cut).GetAttribute("aria-checked"));
        Assert.Equal("0", And(cut).GetAttribute("tabindex"));
    }

    [Fact]
    public void Home_Selects_First_End_Selects_Last()
    {
        var cut = Render();

        // End -> last radio (OR).
        And(cut).KeyDown(new KeyboardEventArgs { Key = "End" });
        Assert.Equal("true", Or(cut).GetAttribute("aria-checked"));

        // Home -> first radio (AND).
        Or(cut).KeyDown(new KeyboardEventArgs { Key = "Home" });
        Assert.Equal("true", And(cut).GetAttribute("aria-checked"));
    }

    // ---- programmatic focus follows the selection (interop move) ----

    [Fact]
    public void Arrow_Selection_Moves_Focus_To_The_Newly_Selected_Radio()
    {
        var cut = Render();
        var orId = Or(cut).GetAttribute("id");
        Assert.False(string.IsNullOrEmpty(orId));

        And(cut).KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });

        // The component moves DOM focus to the newly-selected radio via interop; bUnit
        // can't observe real focus, so assert the recorded FocusElement call instead.
        Assert.Contains(orId, _interop.FocusElementCalls);
    }
}
