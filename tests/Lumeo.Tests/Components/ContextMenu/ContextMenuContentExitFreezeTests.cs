using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.ContextMenu;

/// <summary>
/// Round-6/7 (Codex) — "exit render overwrites the clamped position". The claim was
/// that closing re-emits style="@PositionStyle" (the RAW click point) and snaps the
/// box back from the JS-clamped coordinates mid-exit.
///
/// Verdict: NOT a bug. On close, ContextMenu._x/_y (hence Context.X/Y and
/// PositionStyle) are UNCHANGED — the raw click point is frozen for the item's whole
/// open→exit life. Blazor's attribute diff does not re-emit an attribute whose value
/// is byte-identical to the previous render, so the style attribute produces NO DOM
/// edit on the exit render and the inline left/top that positionAtPoint wrote (the
/// clamp) survive. positionAtPoint writes el.style.left/top/position directly, i.e.
/// the same inline style — which is precisely why an unchanged Razor style must not,
/// and does not, clobber it. (The finding's literal suggestion — omit the style on
/// exit — would be HARMFUL: dropping the attribute makes Blazor REMOVE it, wiping the
/// JS-applied inline position.)
///
/// This test locks the freeze invariant that guarantees the above: the style
/// attribute value is identical across the open→exit transition and still carries the
/// raw coordinates verbatim. It fails if a future change makes PositionStyle depend on
/// IsOpen, resets the coords on close, or omits/rewrites the style during exit.
/// </summary>
public class ContextMenuContentExitFreezeTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public ContextMenuContentExitFreezeTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static RenderFragment MenuContent => c =>
    {
        c.OpenComponent<L.ContextMenuItem>(0);
        c.AddAttribute(1, "ChildContent", (RenderFragment)(i => i.AddContent(0, "Apple")));
        c.CloseComponent();
    };

    private static L.ContextMenu.ContextMenuContext Ctx(bool open, double x, double y)
        => new(open, x, y, default, default);

    [Fact]
    public void Closing_Does_Not_Change_The_Position_Style_During_Exit()
    {
        // Open at a raw click point (500,400) — mirrors a menu that JS then clamps.
        var cut = _ctx.Render<CascadingValue<L.ContextMenu.ContextMenuContext>>(p => p
            .Add(v => v.IsFixed, false)
            .Add(v => v.Value, Ctx(open: true, x: 500, y: 400))
            .Add(v => v.ChildContent, b =>
            {
                b.OpenComponent<L.ContextMenuContent>(0);
                b.AddAttribute(1, "ChildContent", MenuContent);
                b.CloseComponent();
            }));

        var openStyle = cut.Find("[role='menu']").GetAttribute("style");
        Assert.Contains("left: 500px", openStyle);
        Assert.Contains("top: 400px", openStyle);

        // Close — the raw coords are unchanged (the item is exiting, not repositioned).
        cut.Render(p => p
            .Add(v => v.IsFixed, false)
            .Add(v => v.Value, Ctx(open: false, x: 500, y: 400))
            .Add(v => v.ChildContent, b =>
            {
                b.OpenComponent<L.ContextMenuContent>(0);
                b.AddAttribute(1, "ChildContent", MenuContent);
                b.CloseComponent();
            }));

        // The panel is still mounted for the zoom-out exit...
        var exiting = cut.Find("[role='menu']");
        Assert.Equal("closed", exiting.GetAttribute("data-state"));

        // ...and its style attribute is byte-identical to the open render. An identical
        // value means Blazor emits no style edit, so the JS-written inline clamp is not
        // overwritten. (Pre-"fix" — had someone omitted the style on exit — this would
        // be null/different and Blazor would wipe the inline position.)
        Assert.Equal(openStyle, exiting.GetAttribute("style"));
    }
}
