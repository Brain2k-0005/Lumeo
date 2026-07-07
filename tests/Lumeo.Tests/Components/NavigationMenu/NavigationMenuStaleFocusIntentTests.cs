using System.Linq;
using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.NavigationMenu;

/// <summary>
/// Round-6/7 (Codex) — stale keyboard focus intent survives a vetoed open. A
/// keyboard ArrowDown on a trigger latches _focusContentOnOpenItemId BEFORE
/// SetActiveItemId runs. In a controlled menu whose parent VETOES the open (or a
/// one-way Value with no handler), _open never becomes that item, the content never
/// mounts, and the focus request is never consumed — it stays latched. A LATER
/// parent-INITIATED open of that same item (Value pushed in programmatically, not
/// via the keyboard) then mounts the content, whose OnAfterRenderAsync consumes the
/// stale request and steals focus into the content.
///
/// Radix: only a KEYBOARD open moves focus into the content; a programmatic open
/// must not. The fix clears the latched intent when the open is not adopted (and on
/// item unregistration for safety).
///
/// Discriminating repro:
///   1. ArrowDown on the trigger of a controlled menu whose ValueChanged handler
///      does NOT adopt the value (veto) → open is refused, content stays unmounted.
///   2. The parent later opens the item programmatically (Value := "products").
///   • Pre-fix: the stale intent is consumed → FocusElement(contentId) is recorded.
///   • Fixed:   the intent was cleared at step 1 → NO focus call for the content.
/// </summary>
public class NavigationMenuStaleFocusIntentTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public NavigationMenuStaleFocusIntentTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static RenderFragment Menu => b =>
    {
        b.OpenComponent<L.NavigationMenuList>(0);
        b.AddAttribute(1, "ChildContent", (RenderFragment)(list =>
        {
            list.OpenComponent<L.NavigationMenuItem>(0);
            list.AddAttribute(1, "Value", "products");
            list.AddAttribute(2, "ChildContent", (RenderFragment)(item =>
            {
                item.OpenComponent<L.NavigationMenuTrigger>(0);
                item.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "Products")));
                item.CloseComponent();

                item.OpenComponent<L.NavigationMenuContent>(1);
                item.AddAttribute(2, "ChildContent", (RenderFragment)(c => c.AddContent(0, "Products content")));
                item.CloseComponent();
            }));
            list.CloseComponent();
        }));
        b.CloseComponent();
    };

    [Fact]
    public void Vetoed_Keyboard_Open_Does_Not_Leak_Focus_Into_A_Later_Programmatic_Open()
    {
        string? emitted = null;

        // Controlled menu: ValueChanged is bound (so IsControlled) but the handler
        // VETOES — it records the emitted value and never pushes it back into Value.
        var cut = _ctx.Render<L.NavigationMenu>(p => p
            .Add(m => m.Value, (string?)null)
            .Add(m => m.ValueChanged, (string? v) => emitted = v)
            .Add(m => m.ChildContent, Menu));

        // Step 1: keyboard-open the item. The trigger latches the focus intent, then
        // the vetoing parent refuses the open — content stays unmounted.
        cut.Find("button").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        Assert.Equal("products", emitted);                 // intent was reported...
        Assert.Empty(cut.FindAll("[role='menu']"));        // ...but nothing opened.

        // Step 2: the parent opens the item PROGRAMMATICALLY (not via keyboard).
        cut.Render(p => p
            .Add(m => m.Value, "products")
            .Add(m => m.ValueChanged, (string? v) => emitted = v)
            .Add(m => m.ChildContent, Menu));

        var content = cut.Find("[role='menu']");
        Assert.Contains("Products content", cut.Markup);

        // A programmatic open must NOT move focus into the content. Pre-fix the stale
        // keyboard intent was consumed here and FocusElement(contentId) was recorded.
        Assert.DoesNotContain(content.Id, _interop.FocusElementCalls);
    }
}
