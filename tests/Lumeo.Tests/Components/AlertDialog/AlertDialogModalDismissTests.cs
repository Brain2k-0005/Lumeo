using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.AlertDialog;

/// <summary>
/// n=172 (lifecycle) — AlertDialog must stay strictly modal, matching Radix.
/// The backdrop carries no click-to-dismiss handler, so clicking it never
/// closes the dialog and never produces an "outside" dismiss reason. The only
/// reachable <c>TryDismiss</c> reasons are "escape", "cancel" and "action";
/// "outside" is intentionally unreachable (the docs no longer advertise it).
///
/// These tests lock the modal contract: if a future change wires
/// <c>@onclick</c> on the backdrop (re-introducing an "outside" path), the
/// click would close the dialog / fire OnBeforeClose and these assertions fail.
/// </summary>
public class AlertDialogModalDismissTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public AlertDialogModalDismissTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderOpen(
        EventCallback<bool>? openChanged = null,
        EventCallback<L.DismissEventArgs>? onBeforeClose = null)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.AlertDialog>(0);
            builder.AddAttribute(1, "IsOpen", true);
            if (openChanged.HasValue)
                builder.AddAttribute(2, "IsOpenChanged", openChanged.Value);
            if (onBeforeClose.HasValue)
                builder.AddAttribute(3, "OnBeforeClose", onBeforeClose.Value);
            builder.AddAttribute(4, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.AlertDialogContent>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner =>
                    inner.AddContent(0, "Are you sure?")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    // The backdrop is the inner fixed/inset-0 div carrying the
    // --color-overlay-backdrop token (NOT the centering wrapper, which is the
    // first inset-0 div but has no backdrop token).
    private static AngleSharp.Dom.IElement FindBackdrop(IRenderedComponent<IComponent> cut)
        => cut.FindAll("div").Single(d =>
            (d.GetAttribute("style") ?? "").Contains("--color-overlay-backdrop"));

    [Fact]
    public void Backdrop_Has_No_Click_Handler_Stays_Modal()
    {
        var cut = RenderOpen();
        var backdrop = FindBackdrop(cut);

        // No @onclick is wired on the backdrop, so triggering a click raises
        // bUnit's MissingEventHandlerException. That exception type is the
        // proof of the modal contract; any other outcome (a real handler) would
        // mean an "outside" dismiss path was introduced.
        var ex = Record.Exception(() => backdrop.Click());

        Assert.IsType<Bunit.MissingEventHandlerException>(ex);
        // The dialog is unchanged — still open.
        Assert.NotEmpty(cut.FindAll("[role='alertdialog']"));
    }

    [Fact]
    public void Backdrop_Click_Does_Not_Fire_OnBeforeClose_Or_Close()
    {
        var dismissReasons = new List<string>();
        bool? openChangedValue = null;

        var onBeforeClose = EventCallback.Factory.Create<L.DismissEventArgs>(
            _ctx, (L.DismissEventArgs e) => dismissReasons.Add(e.Reason));
        var openChanged = EventCallback.Factory.Create<bool>(
            _ctx, (bool v) => openChangedValue = v);

        var cut = RenderOpen(openChanged, onBeforeClose);
        var backdrop = FindBackdrop(cut);

        // Swallow the MissingEventHandlerException — the point is that nothing
        // observable happens: no dismiss gate fires, no close is dispatched.
        try { backdrop.Click(); } catch (Bunit.MissingEventHandlerException) { }

        Assert.Empty(dismissReasons);              // OnBeforeClose never fired
        Assert.DoesNotContain("outside", dismissReasons);
        Assert.Null(openChangedValue);             // dialog never closed
        Assert.NotEmpty(cut.FindAll("[role='alertdialog']"));
    }

    [Fact]
    public void Escape_Still_Closes_So_Modal_Is_Not_A_Dead_End()
    {
        // Guard against over-correcting: making the dialog modal must not break
        // the legitimate dismiss paths. Escape still produces a close.
        bool? openChangedValue = null;
        var openChanged = EventCallback.Factory.Create<bool>(
            _ctx, (bool v) => openChangedValue = v);

        var cut = RenderOpen(openChanged);
        cut.Find("[role='alertdialog']")
            .KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" });

        Assert.False(openChangedValue);
    }
}
