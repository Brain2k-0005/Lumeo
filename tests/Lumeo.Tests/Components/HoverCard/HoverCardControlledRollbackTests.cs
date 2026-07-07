using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.HoverCard;

/// <summary>
/// Regression tests for the controlled-component rollback fix on HoverCard's
/// Open/OpenChanged pair. The live hover/pin state lives in the private
/// <c>_open</c> backing field, mutated optimistically (before <c>OpenChanged</c>
/// is awaited) by RequestOpen/RequestClose/TogglePin. In controlled mode
/// (OpenChanged bound), if the parent VETOES a close by re-rendering with Open
/// unchanged from before the user's interaction, SetParametersAsync must still
/// roll the backing field back to the parent's authoritative (rejected) value —
/// not silently keep the optimistic local mutation forever, which is what
/// happened when re-adoption only compared against the last INCOMING parameter
/// value instead of the last value the component itself PUSHED via OpenChanged.
/// </summary>
public class HoverCardControlledRollbackTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public HoverCardControlledRollbackTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static RenderFragment Body => b =>
    {
        b.OpenComponent<L.HoverCardTrigger>(0);
        b.AddAttribute(1, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Profile")));
        b.CloseComponent();

        b.OpenComponent<L.HoverCardContent>(2);
        b.AddAttribute(3, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Card body")));
        b.CloseComponent();
    };

    // The trigger is the inline-flex wrapper div.
    private static IElement Trigger(IRenderedComponent<L.HoverCard> cut) =>
        cut.FindAll("div").First(d => (d.GetAttribute("class") ?? "").Contains("inline-flex"));

    // --- Controlled: veto rolls back ---

    [Fact]
    public void Controlled_HoverLeave_Veto_Rolls_Back_To_Open()
    {
        // Parent starts (and stays) Open=true and vetoes every close request by
        // re-rendering with Open unchanged (still true) — a controlled parent
        // that rejects the close.
        IRenderedComponent<L.HoverCard>? cut = null;

        var callback = EventCallback.Factory.Create<bool>(_ctx, (bool incoming) =>
        {
            // Veto: ignore `incoming` (false) and re-assert the original Open=true.
            cut!.Render(p =>
            {
                p.Add(c => c.Open, true);
                p.Add(c => c.OpenChanged, EventCallback.Factory.Create<bool>(_ctx, (_) => { }));
                p.Add(c => c.OpenDelay, 0);
                p.Add(c => c.CloseDelay, 0);
                p.Add(c => c.ChildContent, Body);
            });
        });

        cut = _ctx.Render<L.HoverCard>(p => p
            .Add(c => c.Open, true)
            .Add(c => c.OpenChanged, callback)
            .Add(c => c.OpenDelay, 0)
            .Add(c => c.CloseDelay, 0)
            .Add(c => c.ChildContent, Body));

        Assert.Contains("Card body", cut.Markup);

        // Mouse-leave the trigger: RequestClose optimistically sets _open=false and
        // fires OpenChanged(false); the parent vetoes by keeping Open=true.
        Trigger(cut).MouseLeave();

        // After the veto round-trip settles, the card must have rolled back to
        // open — NOT stayed closed from the un-rolled-back optimistic mutation.
        cut.WaitForAssertion(() => Assert.Contains("Card body", cut.Markup));
    }

    // --- Controlled: accepted close stays closed (contrast case) ---

    [Fact]
    public void Controlled_HoverLeave_Accepted_Close_Stays_Closed()
    {
        // The parent ACCEPTS the close by adopting the incoming value, so the
        // card should genuinely close — proves the fix distinguishes an accepted
        // change from a veto rather than always rolling back.
        bool parentOpen = true;
        IRenderedComponent<L.HoverCard>? cut = null;

        EventCallback<bool> callback = default;
        callback = EventCallback.Factory.Create<bool>(_ctx, (bool incoming) =>
        {
            parentOpen = incoming;
            cut!.Render(p =>
            {
                p.Add(c => c.Open, parentOpen);
                p.Add(c => c.OpenChanged, callback);
                p.Add(c => c.OpenDelay, 0);
                p.Add(c => c.CloseDelay, 0);
                p.Add(c => c.ChildContent, Body);
            });
        });

        cut = _ctx.Render<L.HoverCard>(p => p
            .Add(c => c.Open, true)
            .Add(c => c.OpenChanged, callback)
            .Add(c => c.OpenDelay, 0)
            .Add(c => c.CloseDelay, 0)
            .Add(c => c.ChildContent, Body));

        Assert.Contains("Card body", cut.Markup);

        Trigger(cut).MouseLeave();

        cut.WaitForAssertion(() => Assert.DoesNotContain("Card body", cut.Markup), timeout: TimeSpan.FromSeconds(5));
    }

    // --- Controlled: programmatic parent reset is still adopted ---

    [Fact]
    public void Controlled_Programmatic_Close_Without_User_Interaction_Is_Adopted()
    {
        // Guards against the fix over-correcting: a real parent-driven change of
        // Open (no prior user interaction, so _lastPushed still equals the
        // initial value) must still win.
        var cut = _ctx.Render<L.HoverCard>(p => p
            .Add(c => c.Open, true)
            .Add(c => c.OpenChanged, EventCallback.Factory.Create<bool>(_ctx, (_) => { }))
            .Add(c => c.OpenDelay, 0)
            .Add(c => c.CloseDelay, 0)
            .Add(c => c.ChildContent, Body));

        Assert.Contains("Card body", cut.Markup);

        cut.Render(p => p
            .Add(c => c.Open, false)
            .Add(c => c.OpenChanged, EventCallback.Factory.Create<bool>(_ctx, (_) => { }))
            .Add(c => c.OpenDelay, 0)
            .Add(c => c.CloseDelay, 0)
            .Add(c => c.ChildContent, Body));

        // Content stays mounted through its zoom-out exit window (B11 parity) — poll
        // for the unmount rather than asserting instant removal.
        cut.WaitForAssertion(() => Assert.DoesNotContain("Card body", cut.Markup), timeout: TimeSpan.FromSeconds(5));
    }
}
