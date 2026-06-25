using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Dialog;

/// <summary>
/// #75 (battle-wave2, state-on-data-change) — a one-way controlled dialog
/// (consumer supplies <c>Open</c> but does NOT bind <c>OpenChanged</c>) must be
/// able to stay closed after an internal dismiss. Previously the internal close
/// wrote straight into the <c>Open</c> [Parameter]; the next parent re-render
/// re-pushed the same <c>Open=true</c> literal and the dialog silently reopened.
/// The fix keeps live open-state in a private backing field that is only
/// reseeded from <c>Open</c> when the parent genuinely changes it, so a
/// same-value re-render no longer clobbers the user's dismiss.
/// </summary>
public class DialogControlledStateTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DialogControlledStateTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.Dialog> RenderOneWayControlled(bool open)
    {
        // Open is supplied, OpenChanged is intentionally NOT bound -> one-way
        // controlled (the bug scenario).
        return _ctx.Render<L.Dialog>(p => p
            .Add(d => d.Open, open)
            .Add(d => d.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<L.DialogContent>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Body text")));
                b.CloseComponent();
            })));
    }

    [Fact]
    public void OneWayControlled_StaysClosed_After_Internal_Dismiss_And_SameValue_Rerender()
    {
        var cut = RenderOneWayControlled(open: true);
        Assert.NotEmpty(cut.FindAll("[role='dialog']"));

        // Internal dismiss (Escape -> TryDismiss -> SetOpen(false)).
        cut.Find("[role='dialog']").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[role='dialog']")));

        // An unrelated parent re-render re-pushes the SAME Open=true literal.
        // Without the fix this reopens the dialog; with the fix the internal
        // close survives because the param value did not actually change.
        cut.Render(p => p.Add(d => d.Open, true));

        Assert.Empty(cut.FindAll("[role='dialog']"));
    }

    [Fact]
    public void OneWayControlled_Reopens_When_Parent_Actually_Toggles_Open()
    {
        // After an internal close, a parent that genuinely changes Open
        // (false -> true) must regain authority and reopen the dialog. This
        // guards against the fix over-correcting and ignoring real changes.
        var cut = RenderOneWayControlled(open: true);

        cut.Find("[role='dialog']").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[role='dialog']")));

        // Parent drives Open to a genuinely different value (false), then back to
        // true — a real change that must reassert the open state.
        cut.Render(p => p.Add(d => d.Open, false));
        Assert.Empty(cut.FindAll("[role='dialog']"));

        cut.Render(p => p.Add(d => d.Open, true));
        Assert.NotEmpty(cut.FindAll("[role='dialog']"));
    }
}
