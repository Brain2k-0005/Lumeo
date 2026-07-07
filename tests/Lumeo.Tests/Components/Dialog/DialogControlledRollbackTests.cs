using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Dialog;

/// <summary>
/// Regression tests for the controlled-component rollback fix on Dialog. When
/// the dialog is used in fully controlled mode (OpenChanged bound) and the
/// parent vetoes a dismiss by re-rendering with the original Open value, the
/// UI must roll back to the bound (rejected) value rather than permanently
/// showing the optimistic close.
/// </summary>
public class DialogControlledRollbackTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DialogControlledRollbackTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static RenderFragment DialogBody => b =>
    {
        b.OpenComponent<L.DialogContent>(0);
        b.AddAttribute(1, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Body text")));
        b.CloseComponent();
    };

    // --- Controlled: veto rolls back ---

    [Fact]
    public void Controlled_Veto_Rolls_Back_To_Bound_Value()
    {
        // Parent starts with Open=true and vetoes every dismiss by keeping its
        // own state unchanged (always re-renders with Open=true).
        bool parentState = true;
        IRenderedComponent<L.Dialog>? cut = null;

        var callback = EventCallback.Factory.Create<bool>(_ctx, (bool incoming) =>
        {
            // Veto: do NOT update parentState; re-render with the original value.
            cut!.Render(p =>
            {
                p.Add(d => d.Open, parentState);   // still true
                p.Add(d => d.OpenChanged, EventCallback.Factory.Create<bool>(_ctx, (_) => { }));
            });
        });

        cut = _ctx.Render<L.Dialog>(p => p
            .Add(d => d.Open, true)
            .Add(d => d.OpenChanged, callback)
            .Add(d => d.ChildContent, DialogBody));

        Assert.NotEmpty(cut.FindAll("[role='dialog']"));

        // Escape -> TryDismiss -> SetOpen(false); the parent vetoes and
        // re-renders with Open=true (unchanged, rejected).
        cut.Find("[role='dialog']").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        // After the veto the dialog must have rolled back to open, not stayed
        // closed showing the rejected optimistic state.
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[role='dialog']")));
    }

    // --- Controlled: accepted dismiss keeps closed ---

    [Fact]
    public void Controlled_Accepted_Dismiss_Keeps_Closed()
    {
        // Parent accepts every dismiss by updating its own state and re-rendering.
        bool parentState = true;
        IRenderedComponent<L.Dialog>? cut = null;

        EventCallback<bool> callback = default;
        callback = EventCallback.Factory.Create<bool>(_ctx, (bool incoming) =>
        {
            parentState = incoming;
            cut!.Render(p =>
            {
                p.Add(d => d.Open, parentState);
                p.Add(d => d.OpenChanged, callback);
            });
        });

        cut = _ctx.Render<L.Dialog>(p => p
            .Add(d => d.Open, true)
            .Add(d => d.OpenChanged, callback)
            .Add(d => d.ChildContent, DialogBody));

        cut.Find("[role='dialog']").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        // Parent accepted the close — the dialog must stay closed.
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[role='dialog']")));
    }

    // --- Controlled: programmatic parent close ---

    [Fact]
    public void Controlled_Programmatic_Close_Is_Adopted()
    {
        // Start open; the parent programmatically closes WITHOUT the user
        // dismissing first (simulates an external close, e.g. a timeout or
        // another action driving the same bound Open value).
        var cut = _ctx.Render<L.Dialog>(p => p
            .Add(d => d.Open, true)
            .Add(d => d.OpenChanged, EventCallback.Factory.Create<bool>(_ctx, (_) => { }))
            .Add(d => d.ChildContent, DialogBody));

        Assert.NotEmpty(cut.FindAll("[role='dialog']"));

        cut.Render(p => p.Add(d => d.Open, false));

        // Declarative close now animates by default (PlayExitAnimation defaults to
        // true): the panel stays mounted for the zoom-out window, then unmounts on
        // the fallback timer. Poll for the eventual unmount instead of asserting it
        // synchronously. (Generous ceiling so a starved thread pool under parallel
        // load can't trip the ~280 ms fallback.)
        cut.WaitForAssertion(
            () => Assert.Empty(cut.FindAll("[role='dialog']")),
            timeout: TimeSpan.FromSeconds(5));
    }
}
