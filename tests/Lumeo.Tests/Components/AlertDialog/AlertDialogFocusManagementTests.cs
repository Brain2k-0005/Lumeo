using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.AlertDialog;

/// <summary>
/// AlertDialog focus-management regressions:
///
/// 1. (P1) Escape inside an AlertDialog nested in a Dialog must close only the
///    AlertDialog — the keydown must not bubble into the ancestor overlay.
///
/// 2. (P2) Initial focus must land on the LEAST destructive action — the
///    Cancel button — not on whatever is first in DOM order (WAI-ARIA APG /
///    Radix behaviour). AlertDialogCancel carries a
///    <c>data-lumeo-initial-focus</c> marker and AlertDialogContent passes the
///    matching selector to SetupFocusTrap.
///
/// 3. (P1) The focus trap is removed on both close paths so focus returns to
///    the trigger.
/// </summary>
public class AlertDialogFocusManagementTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public AlertDialogFocusManagementTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static RenderFragment AlertDialogFragment(bool open, RenderFragment content) => builder =>
    {
        builder.OpenComponent<L.AlertDialog>(0);
        builder.AddAttribute(1, "Open", open);
        builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
        {
            b.OpenComponent<L.AlertDialogContent>(0);
            b.AddAttribute(1, "ChildContent", content);
            b.CloseComponent();
        }));
        builder.CloseComponent();
    };

    private static RenderFragment CancelAndAction() => builder =>
    {
        builder.OpenComponent<L.AlertDialogAction>(0);
        builder.AddAttribute(1, "ChildContent", (RenderFragment)(c => c.AddContent(0, "Delete")));
        builder.CloseComponent();
        builder.OpenComponent<L.AlertDialogCancel>(2);
        builder.AddAttribute(3, "ChildContent", (RenderFragment)(c => c.AddContent(0, "Cancel")));
        builder.CloseComponent();
    };

    // --- P2: initial focus targets the least destructive action ---

    [Fact]
    public void SetupFocusTrap_Targets_The_Cancel_Button_Marker()
    {
        _ctx.Render(AlertDialogFragment(open: true, CancelAndAction()));

        var setup = Assert.Single(_interop.FocusTrapSetups);
        Assert.Equal("[data-lumeo-initial-focus]", setup.InitialFocusSelector);
    }

    [Fact]
    public void AlertDialogCancel_Carries_The_Initial_Focus_Marker()
    {
        var cut = _ctx.Render(AlertDialogFragment(open: true, CancelAndAction()));

        var cancel = cut.FindAll("button").Single(b => b.TextContent.Trim() == "Cancel");
        Assert.True(cancel.HasAttribute("data-lumeo-initial-focus"));

        var action = cut.FindAll("button").Single(b => b.TextContent.Trim() == "Delete");
        Assert.False(action.HasAttribute("data-lumeo-initial-focus"));
    }

    // --- P1: nested overlays — Escape must not cascade to ancestors ---

    [Fact]
    public void Escape_On_AlertDialog_Inside_Dialog_Does_Not_Close_Outer_Dialog()
    {
        var cut = _ctx.Render<NestedOverlayHost<L.AlertDialog, L.AlertDialogContent>>();

        Assert.Single(cut.FindAll("[role='dialog']"));
        var inner = cut.Find("[role='alertdialog']");

        inner.KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.False(cut.Instance.InnerOpen);                // alert dialog handled Escape and closed
        Assert.True(cut.Instance.OuterOpen);                 // outer dialog was never asked to close
        Assert.Empty(cut.FindAll("[role='alertdialog']"));   // alert dialog gone from markup
        Assert.Single(cut.FindAll("[role='dialog']"));       // outer dialog survives
    }

    // --- P1: focus trap lifecycle on both close paths ---

    [Fact]
    public void Closing_AlertDialog_Removes_The_Focus_Trap()
    {
        var cut = _ctx.Render<L.AlertDialog>(p => p
            .Add(d => d.Open, true)
            .AddChildContent<L.AlertDialogContent>(cp => cp.AddChildContent("Body")));

        var setup = Assert.Single(_interop.FocusTrapSetups);
        Assert.Empty(_interop.FocusTrapRemovals);

        cut.Render(p => p.Add(d => d.Open, false));

        Assert.Equal(setup.ElementId, Assert.Single(_interop.FocusTrapRemovals));
    }

    [Fact]
    public void Disposing_Open_AlertDialog_Removes_The_Focus_Trap()
    {
        var cut = _ctx.Render<ConditionalRoot>(p => p
            .Add(x => x.Show, true)
            .AddChildContent(AlertDialogFragment(open: true, CancelAndAction())));

        var setup = Assert.Single(_interop.FocusTrapSetups);
        Assert.Empty(_interop.FocusTrapRemovals);

        cut.Render(p => p.Add(x => x.Show, false));

        Assert.Equal(setup.ElementId, Assert.Single(_interop.FocusTrapRemovals));
    }
}
