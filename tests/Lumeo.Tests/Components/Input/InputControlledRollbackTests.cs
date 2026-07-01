using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Input;

/// <summary>
/// Controlled-mode (ValueChanged bound) regression tests for Input, mirroring
/// ToggleGroup's controlled-rollback contract.
///
/// Bug (Codex P2): a consumer that binds ValueChanged purely to OBSERVE — without
/// ever binding Value back — leaves Value at its unbound default (null) on every
/// render. HandleInput updates only _current (not Value), so the parent's normal
/// post-callback re-render still supplies that same null, which the controlled
/// branch treated as an authoritative rejection and reset _current — clearing
/// whatever the user just typed into &lt;Input ValueChanged="..." /&gt;.
///
/// Fix: the "Value equals what we last pushed" echo check gets a SECOND, narrow
/// exception — Value is null AND it was ALSO null on the render before this
/// interaction (_lastValueParam) — the strongest available signal Value was never
/// actually bound. A NON-null unchanged Value is still treated as a genuine,
/// authoritative rejection and rolls the text back (matching ToggleGroup's
/// null-only carve-out).
/// </summary>
public class InputControlledRollbackTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public InputControlledRollbackTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Controlled_Unchanged_From_Null_Baseline_Keeps_Typed_Text()
    {
        // Callback-only consumer: ValueChanged is bound, but Value is never bound
        // back (stays at its unbound default, null).
        var cut = _ctx.Render<L.Input>(p => p
            .Add(i => i.Value, (string?)null)
            .Add(i => i.ValueChanged, (string? _) => { })); // observes only, never echoes

        cut.Find("input").Input("hello");

        // The parent's normal post-callback re-render still supplies Value=null —
        // its unbound default, unchanged from what this component saw before typing.
        cut.Render(p => p
            .Add(i => i.Value, (string?)null)
            .Add(i => i.ValueChanged, (string? _) => { }));

        // Not a rejection — the typed text must survive.
        Assert.Equal("hello", cut.Find("input").GetAttribute("value"));
    }

    [Fact]
    public void Controlled_Rejection_From_NonNull_Baseline_Rolls_Back_To_Bound_Value()
    {
        // A GENUINE rejection starting from a non-null (bound) baseline: the parent
        // binds Value="start" to real state and rejects the edit by leaving Value
        // unchanged — indistinguishable, on the wire, from a null-baseline observer,
        // but the null-only carve-out means a NON-null unchanged Value is always
        // treated as an authoritative rejection.
        var cut = _ctx.Render<L.Input>(p => p
            .Add(i => i.Value, "start")
            .Add(i => i.ValueChanged, (string? _) => { })); // rejection: never re-supplies Value

        cut.Find("input").Input("typed over start");

        // Parent re-renders with the same "start" — the rejection.
        cut.Render(p => p
            .Add(i => i.Value, "start")
            .Add(i => i.ValueChanged, (string? _) => { }));

        Assert.Equal("start", cut.Find("input").GetAttribute("value"));
    }

    [Fact]
    public void Controlled_Accepted_Edit_Keeps_New_Text()
    {
        // Guard against over-correction: when the parent ACCEPTS the edit and
        // re-renders with the new Value we pushed, the new text must show.
        string? boundValue = null;
        var cut = _ctx.Render<L.Input>(p => p
            .Add(i => i.Value, boundValue)
            .Add(i => i.ValueChanged, (string? v) => boundValue = v));

        cut.Find("input").Input("accepted");

        cut.Render(p => p
            .Add(i => i.Value, boundValue) // = "accepted"
            .Add(i => i.ValueChanged, (string? v) => boundValue = v));

        Assert.Equal("accepted", cut.Find("input").GetAttribute("value"));
    }

    [Fact]
    public void Controlled_Rejection_With_Distinct_Value_Rolls_Back_To_That_Value()
    {
        // A GENUINE, distinguishable rejection: the parent's callback explicitly
        // asserts a value different from BOTH what the user typed AND the
        // pre-interaction (null) baseline — this must still win.
        IRenderedComponent<L.Input>? cut = null;
        var callback = EventCallback.Factory.Create<string?>(_ctx, (string? _) =>
        {
            cut!.Render(p => p
                .Add(i => i.Value, "normalized")
                .Add(i => i.ValueChanged, EventCallback.Factory.Create<string?>(_ctx, (string? _2) => { })));
        });

        cut = _ctx.Render<L.Input>(p => p
            .Add(i => i.Value, (string?)null)
            .Add(i => i.ValueChanged, callback));

        cut.Find("input").Input("typed"); // pushes "typed"

        Assert.Equal("normalized", cut.Find("input").GetAttribute("value"));
    }
}
