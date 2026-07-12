using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Card;

/// <summary>
/// SPECIAL scout finding re-verified against the source: Card.razor already
/// implements the correct conditionally-interactive contract — when OnClick
/// is bound it renders role="button" tabindex="0" with a HandleKeyDown that
/// activates on Enter/Space (and registers a Space-scroll prevent-default
/// rule via RegisterPreventDefaultKeys, since a div has no native key
/// synthesis); when OnClick is NOT bound it renders a plain div with none of
/// that — no tabindex, no role, no keydown listener at all. There was no
/// product fix needed here; CardInteractiveTests already covers the
/// role/tabindex/click wiring. This file closes the two gaps that suite
/// doesn't: actual Enter/Space keyboard activation, and an explicit
/// "passive card has zero interactive attributes" pin.
/// </summary>
public class CardKeyboardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public CardKeyboardTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Enter_Activates_The_Clickable_Card()
    {
        var clicked = false;
        var cut = _ctx.Render<L.Card>(p => p
            .Add(c => c.OnClick, _ => clicked = true)
            .AddChildContent("Clickable"));

        cut.Find("div[role='button']").KeyDown("Enter");

        Assert.True(clicked);
    }

    [Fact]
    public void Space_Activates_The_Clickable_Card()
    {
        var clicked = false;
        var cut = _ctx.Render<L.Card>(p => p
            .Add(c => c.OnClick, _ => clicked = true)
            .AddChildContent("Clickable"));

        cut.Find("div[role='button']").KeyDown(" ");

        Assert.True(clicked);
    }

    [Fact]
    public void Unhandled_Key_Does_Not_Activate_The_Clickable_Card()
    {
        var clicked = false;
        var cut = _ctx.Render<L.Card>(p => p
            .Add(c => c.OnClick, _ => clicked = true)
            .AddChildContent("Clickable"));

        cut.Find("div[role='button']").KeyDown("a");

        Assert.False(clicked);
    }

    [Fact]
    public void Interactive_Card_Registers_The_Space_Scroll_Guard_On_Mount()
    {
        // Space activates the div AND, without this, scrolls the page — a div
        // has no native key synthesis the way a <button> does. Assert the
        // guard is actually wired via RegisterPreventDefaultKeys.
        var interop = new TrackingInteropService();
        _ctx.Services.AddSingleton<Lumeo.Services.IComponentInteropService>(interop);

        var cut = _ctx.Render<L.Card>(p => p
            .Add(c => c.OnClick, _ => { })
            .AddChildContent("Clickable"));

        var cardId = cut.Find("div[role='button']").GetAttribute("id");
        cut.WaitForAssertion(() =>
            Assert.Contains(cardId!, interop.RegisterPreventDefaultKeysElementIds));
    }

    // --- Passive card: zero interactive surface ---

    [Fact]
    public void Passive_Card_Has_No_Tabindex_No_Role_And_No_Keydown_Listener()
    {
        var cut = _ctx.Render<L.Card>(p => p.AddChildContent("Static"));

        var div = cut.Find("div");
        Assert.Null(div.GetAttribute("tabindex"));
        Assert.Null(div.GetAttribute("role"));
        // Blazor emits a `blazor:onkeydown="<id>"` marker attribute only when
        // an @onkeydown handler is actually wired for that element.
        Assert.False(div.HasAttribute("blazor:onkeydown"));
    }

    [Fact]
    public void Passive_Card_Never_Registers_The_Space_Scroll_Guard()
    {
        var interop = new TrackingInteropService();
        _ctx.Services.AddSingleton<Lumeo.Services.IComponentInteropService>(interop);

        _ctx.Render<L.Card>(p => p.AddChildContent("Static"));

        Assert.Empty(interop.RegisterPreventDefaultKeysElementIds);
    }
}
