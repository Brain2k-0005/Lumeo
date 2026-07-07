using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Dialog;

/// <summary>
/// Overlay focus-management regressions (a11y, P1):
///
/// 1. Escape pressed inside a nested overlay must close only that overlay.
///    Inner overlay content renders inside the outer's DOM, so the keydown
///    bubbles; pre-fix the outer Dialog's Escape handler also fired and every
///    ancestor overlay closed at once. The content div now stops keydown
///    propagation at its own boundary.
///
/// 2. The focus trap must be torn down (RemoveFocusTrap) on BOTH close paths —
///    Open flipped to false externally AND dispose-while-open — because the JS
///    side of RemoveFocusTrap is what returns focus to the trigger element.
/// </summary>
public class DialogFocusManagementTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public DialogFocusManagementTests()
    {
        _ctx.AddLumeoServices();
        // Replace the JS-backed interop with the tracking fake so focus-trap
        // lifecycle calls are observable (last registration wins).
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static RenderFragment DialogFragment(bool open, EventCallback<bool>? openChanged, RenderFragment content) => builder =>
    {
        builder.OpenComponent<L.Dialog>(0);
        builder.AddAttribute(1, "Open", open);
        if (openChanged.HasValue)
            builder.AddAttribute(2, "OpenChanged", openChanged.Value);
        builder.AddAttribute(3, "ChildContent", (RenderFragment)(b =>
        {
            b.OpenComponent<L.DialogContent>(0);
            b.AddAttribute(1, "ChildContent", content);
            b.CloseComponent();
        }));
        builder.CloseComponent();
    };

    // --- Escape (single dialog still closes; stopPropagation must not break it) ---

    [Fact]
    public void Escape_On_Dialog_Content_Closes_The_Dialog()
    {
        bool? closed = null;
        var callback = EventCallback.Factory.Create<bool>(_ctx, (bool v) => closed = v);
        var cut = _ctx.Render(DialogFragment(open: true, callback, inner => inner.AddContent(0, "Body")));

        cut.Find("[role='dialog']").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.False(closed);
    }

    // --- Nested overlays: Escape must not cascade to ancestors ---

    [Fact]
    public void Escape_On_Inner_Dialog_Does_Not_Close_Outer_Dialog()
    {
        var cut = _ctx.Render<NestedOverlayHost<L.Dialog, L.DialogContent>>();

        var dialogs = cut.FindAll("[role='dialog']");
        Assert.Equal(2, dialogs.Count);

        // Press Escape on the INNER dialog's content element (last in document
        // order). The event bubbles upward through the outer dialog's content.
        dialogs[^1].KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.False(cut.Instance.InnerOpen);          // inner handled Escape and closed
        Assert.True(cut.Instance.OuterOpen);           // outer was never asked to close
        // The inner dialog now plays an exit animation before unmounting (the
        // declarative close animates by default), so it lingers in the DOM briefly.
        // Wait for it to leave; only the outer dialog should remain.
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[role='dialog']")));
    }

    // --- Focus trap lifecycle: close path 1 (Open flipped false externally) ---

    [Fact]
    public void Closing_Dialog_Removes_The_Focus_Trap()
    {
        var cut = _ctx.Render<L.Dialog>(p => p
            .Add(d => d.Open, true)
            .AddChildContent<L.DialogContent>(cp => cp.AddChildContent("Body")));

        var setup = Assert.Single(_interop.FocusTrapSetups);
        Assert.Null(setup.InitialFocusSelector); // Dialog has no preferred initial-focus target
        Assert.Empty(_interop.FocusTrapRemovals);

        cut.Render(p => p.Add(d => d.Open, false));

        Assert.Equal(setup.ElementId, Assert.Single(_interop.FocusTrapRemovals));
    }

    // --- Focus trap lifecycle: close path 2 (dispose while open) ---

    [Fact]
    public void Disposing_Open_Dialog_Removes_The_Focus_Trap()
    {
        var cut = _ctx.Render<ConditionalRoot>(p => p
            .Add(x => x.Show, true)
            .AddChildContent(DialogFragment(open: true, null, inner => inner.AddContent(0, "Body"))));

        var setup = Assert.Single(_interop.FocusTrapSetups);
        Assert.Empty(_interop.FocusTrapRemovals);

        cut.Render(p => p.Add(x => x.Show, false)); // unmounts the open dialog → DisposeAsync path

        Assert.Equal(setup.ElementId, Assert.Single(_interop.FocusTrapRemovals));
    }
}
