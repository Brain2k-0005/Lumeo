using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Chip;

/// <summary>
/// Triage wave-3 #7 (medium, keyboard-a11y) — a clickable Chip is a
/// <c>div[role=button]</c>, which has no native key synthesis: Space both
/// activated the chip AND scroll-jumped the page (the <c>@onkeydown</c> handler
/// fired <c>OnClick</c> but never suppressed Space's default). The fix suppresses
/// only Space's default action via the library's <c>RegisterPreventDefaultKeys</c>
/// interop (the same idiom as Card / CollapsibleTrigger / DialogTrigger), which
/// requires the chip to carry a stable <c>id</c> so the JS handler can target it.
/// A blanket <c>@onkeydown:preventDefault</c> is intentionally avoided because it
/// would also block Tab navigation.
///
/// bUnit cannot observe a JS-level <c>preventDefault</c> nor real focus, so this
/// test asserts the OBSERVABLE precondition the fix introduces: the clickable
/// chip's <c>role=button</c> surface now renders a non-empty <c>id</c> attribute
/// (it had none before), which is what the key-suppression registration binds to.
/// It also pins the existing behaviour (Enter/Space still activate).
/// </summary>
public class ChipSpacePreventDefaultTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public ChipSpacePreventDefaultTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // ---- the fix: the role=button surface carries a stable id (key-suppression target) ----

    [Fact]
    public void Clickable_RoleButton_Chip_Has_Stable_Id()
    {
        var cut = _ctx.Render<L.Chip>(p => p
            .Add(c => c.Clickable, true)
            .AddChildContent("Tag"));

        var root = cut.Find("div[role='button']");
        var id = root.GetAttribute("id");

        Assert.False(string.IsNullOrEmpty(id),
            "A clickable Chip's role=button surface must expose an id so Space preventDefault can be registered against it.");
    }

    // ---- existing behaviour preserved: activation still works on Space and Enter ----

    [Fact]
    public void Space_Still_Fires_OnClick()
    {
        var clicked = false;
        var cut = _ctx.Render<L.Chip>(p => p
            .Add(c => c.Clickable, true)
            .Add(c => c.OnClick, EventCallback.Factory.Create(this, () => clicked = true))
            .AddChildContent("Tag"));

        cut.Find("div[role='button']").KeyDown(new KeyboardEventArgs { Key = " " });

        Assert.True(clicked);
    }

    [Fact]
    public void Enter_Still_Fires_OnClick()
    {
        var clicked = false;
        var cut = _ctx.Render<L.Chip>(p => p
            .Add(c => c.Clickable, true)
            .Add(c => c.OnClick, EventCallback.Factory.Create(this, () => clicked = true))
            .AddChildContent("Tag"));

        cut.Find("div[role='button']").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.True(clicked);
    }
}
