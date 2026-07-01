using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.ToggleGroup;

/// <summary>
/// Regression: when a controlled parent vetoes a toggle (re-renders with a value
/// DISTINCT from both what the user pushed and what it held before the
/// interaction), the component must roll the UI back to that authoritative value
/// instead of keeping the in-flight rejected selection.
///
/// Bug (original): the old OnParametersSet compared incoming Value against
/// _lastValue (the last parameter the parent SUPPLIED). When the parent re-renders
/// after a veto it supplies the SAME value → the comparison saw "no change" and
/// skipped the resync, leaving the rejected selection visible.
///
/// Fix (first pass): track _lastPushedValue/_lastPushedSelectedValues (what WE
/// emitted via the callback) and treat ANY incoming value that differs from that
/// push as authoritative.
///
/// Bug (Codex P2, second pass): that first-pass fix used ValueChanged.HasDelegate
/// alone as the "is controlled" signal. A consumer that binds ValueChanged/
/// SelectedValuesChanged purely to OBSERVE — without ever binding Value/
/// SelectedValues back — leaves those parameters at their unbound default (e.g.
/// null) on every render. The parent's normal post-callback re-render then supplies
/// that same unbound default, which the first-pass fix treated as an authoritative
/// override (since it differs from the just-pushed value) and clobbered the local
/// toggle — making callback-only single/multiple toggle groups appear unable to
/// stay selected.
///
/// Fix (this pass): a SECOND baseline — _lastValue/_lastSelectedValues, i.e. what
/// the PARAMETER held on the render immediately before this interaction — is also
/// treated as a no-op alongside the echo check. An incoming value matching that
/// pre-interaction baseline is indistinguishable from "the parent isn't actually
/// driving this parameter" and must NOT clobber local state. Only a value that
/// differs from BOTH the push AND the pre-interaction baseline is a genuine,
/// distinguishable authoritative assertion (veto-with-a-different-value,
/// normalization, or programmatic reset) and rolls the UI back.
/// </summary>
public class ToggleGroupControlledRollbackTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ToggleGroupControlledRollbackTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static RenderFragment TwoItems() => b =>
    {
        b.OpenComponent<L.ToggleGroupItem>(0);
        b.AddAttribute(1, "Value", "a");
        b.AddAttribute(2, "ChildContent", (RenderFragment)(c => c.AddContent(0, "A")));
        b.CloseComponent();

        b.OpenComponent<L.ToggleGroupItem>(3);
        b.AddAttribute(4, "Value", "b");
        b.AddAttribute(5, "ChildContent", (RenderFragment)(c => c.AddContent(0, "B")));
        b.CloseComponent();
    };

    // ------------------------------------------------------------------
    // Single mode
    // ------------------------------------------------------------------

    [Fact]
    public void Single_Controlled_Unchanged_From_Null_Baseline_Keeps_Local_Toggle()
    {
        // A callback-only consumer: ValueChanged is bound, but Value is never bound
        // back (stays at its unbound default, null) — the #38-class observer-only
        // contract, extended to ToggleGroup (Codex P2).
        var cut = _ctx.Render<L.ToggleGroup>(p => p
            .Add(g => g.Type, L.ToggleGroup.ToggleGroupType.Single)
            .Add(g => g.Value, (string?)null)
            .Add(g => g.ValueChanged, (string? _) => { })  // observes only, never echoes
            .Add(g => g.ChildContent, TwoItems()));

        Assert.All(cut.FindAll("button"), b => Assert.Equal("false", b.GetAttribute("aria-pressed")));

        // Click "a" — ToggleItem selects it and pushes "a" via ValueChanged.
        cut.FindAll("button")[0].Click();

        // The parent's normal post-callback re-render still supplies Value=null — its
        // unbound default, unchanged from what this component saw before the click.
        cut.Render(p => p
            .Add(g => g.Type, L.ToggleGroup.ToggleGroupType.Single)
            .Add(g => g.Value, (string?)null)
            .Add(g => g.ValueChanged, (string? _) => { }));

        // Not a veto — the local toggle must stay selected.
        Assert.Equal("true", cut.FindAll("button")[0].GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Single_Controlled_Unchanged_From_Selected_Baseline_Keeps_Local_Toggle()
    {
        // Same observer-only contract, starting from a non-null baseline: the parent
        // binds Value="a" once (its own local state) and ValueChanged purely to
        // observe, without ever re-binding Value after the callback fires.
        var cut = _ctx.Render<L.ToggleGroup>(p => p
            .Add(g => g.Type, L.ToggleGroup.ToggleGroupType.Single)
            .Add(g => g.Value, "a")
            .Add(g => g.ValueChanged, (string? _) => { })  // observes only, never echoes
            .Add(g => g.ChildContent, TwoItems()));

        Assert.Equal("true", cut.FindAll("button")[0].GetAttribute("aria-pressed"));

        // User clicks "a" to deselect — pushes null via ValueChanged.
        cut.FindAll("button")[0].Click();

        // The parent's re-render still supplies Value="a" — unchanged from the
        // baseline this component saw before the click, not an echo of the push.
        cut.Render(p => p
            .Add(g => g.Type, L.ToggleGroup.ToggleGroupType.Single)
            .Add(g => g.Value, "a")
            .Add(g => g.ValueChanged, (string? _) => { }));

        // Not a veto — the local deselect must stick.
        Assert.All(cut.FindAll("button"), b => Assert.Equal("false", b.GetAttribute("aria-pressed")));
    }

    [Fact]
    public void Single_Controlled_Veto_With_Distinct_Value_Rolls_Back_To_That_Value()
    {
        // A GENUINE, distinguishable veto: the parent's callback explicitly asserts a
        // value different from BOTH what the user pushed ("a") AND the pre-interaction
        // baseline (null) — this must still win and roll the UI back.
        IRenderedComponent<L.ToggleGroup>? cut = null;
        var callback = EventCallback.Factory.Create<string?>(_ctx, (string? _) =>
        {
            cut!.Render(p => p
                .Add(g => g.Type, L.ToggleGroup.ToggleGroupType.Single)
                .Add(g => g.Value, "b")
                .Add(g => g.ValueChanged, EventCallback.Factory.Create<string?>(_ctx, (string? _2) => { })));
        });

        cut = _ctx.Render<L.ToggleGroup>(p => p
            .Add(g => g.Type, L.ToggleGroup.ToggleGroupType.Single)
            .Add(g => g.Value, (string?)null)
            .Add(g => g.ValueChanged, callback)
            .Add(g => g.ChildContent, TwoItems()));

        cut.FindAll("button")[0].Click(); // pushes "a"

        Assert.Equal("false", cut.FindAll("button")[0].GetAttribute("aria-pressed")); // "a" rejected
        Assert.Equal("true",  cut.FindAll("button")[1].GetAttribute("aria-pressed")); // "b" wins
    }

    [Fact]
    public void Single_Controlled_Accepted_Toggle_Shows_New_Selection()
    {
        // Guard against over-correction: when the parent ACCEPTS the toggle and
        // re-renders with the new Value we pushed, the new selection must show.
        string? boundValue = null;

        var cut = _ctx.Render<L.ToggleGroup>(p => p
            .Add(g => g.Type, L.ToggleGroup.ToggleGroupType.Single)
            .Add(g => g.Value, boundValue)
            .Add(g => g.ValueChanged, (string? v) => boundValue = v)
            .Add(g => g.ChildContent, TwoItems()));

        // Click "a" — the callback stores "a" in boundValue.
        cut.FindAll("button")[0].Click();

        // Parent re-renders with the accepted value ("a").
        cut.Render(p => p
            .Add(g => g.Type, L.ToggleGroup.ToggleGroupType.Single)
            .Add(g => g.Value, boundValue)       // = "a"
            .Add(g => g.ValueChanged, (string? v) => boundValue = v));

        Assert.Equal("true",  cut.FindAll("button")[0].GetAttribute("aria-pressed"));
        Assert.Equal("false", cut.FindAll("button")[1].GetAttribute("aria-pressed"));
    }

    // ------------------------------------------------------------------
    // Multiple mode
    // ------------------------------------------------------------------

    [Fact]
    public void Multiple_Controlled_Unchanged_From_Null_Baseline_Keeps_Local_Toggle()
    {
        // Callback-only consumer: SelectedValuesChanged is bound, SelectedValues is
        // never bound back and stays at its unbound default (null).
        var cut = _ctx.Render<L.ToggleGroup>(p => p
            .Add(g => g.Type, L.ToggleGroup.ToggleGroupType.Multiple)
            .Add(g => g.SelectedValues, (IEnumerable<string>?)null)
            .Add(g => g.SelectedValuesChanged, (IEnumerable<string> _) => { })  // observes only
            .Add(g => g.ChildContent, TwoItems()));

        // Click "a" — pushes ["a"] via SelectedValuesChanged.
        cut.FindAll("button")[0].Click();

        // The parent's re-render still supplies the same null reference it held
        // before the click — not an echo of the push, just the unbound default.
        cut.Render(p => p
            .Add(g => g.Type, L.ToggleGroup.ToggleGroupType.Multiple)
            .Add(g => g.SelectedValues, (IEnumerable<string>?)null)
            .Add(g => g.SelectedValuesChanged, (IEnumerable<string> _) => { }));

        // Not a veto — the local toggle must stay selected.
        Assert.Equal("true", cut.FindAll("button")[0].GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Multiple_Controlled_Unchanged_From_Selected_Baseline_Keeps_Local_Toggle()
    {
        // Callback-only consumer starting from a non-null baseline: SelectedValues is
        // bound once to the parent's own list, SelectedValuesChanged purely observes
        // and never re-supplies SelectedValues after the callback fires.
        var originalList = new List<string> { "a" };

        var cut = _ctx.Render<L.ToggleGroup>(p => p
            .Add(g => g.Type, L.ToggleGroup.ToggleGroupType.Multiple)
            .Add(g => g.SelectedValues, originalList)
            .Add(g => g.SelectedValuesChanged, (IEnumerable<string> _) => { })  // observes only
            .Add(g => g.ChildContent, TwoItems()));

        Assert.Equal("true", cut.FindAll("button")[0].GetAttribute("aria-pressed"));

        // User deselects "a" — pushes [] via SelectedValuesChanged.
        cut.FindAll("button")[0].Click();

        // The parent's re-render still supplies the SAME originalList reference it
        // held before the click — unchanged from the pre-interaction baseline.
        cut.Render(p => p
            .Add(g => g.Type, L.ToggleGroup.ToggleGroupType.Multiple)
            .Add(g => g.SelectedValues, originalList)
            .Add(g => g.SelectedValuesChanged, (IEnumerable<string> _) => { }));

        // Not a veto — the local deselect must stick.
        Assert.All(cut.FindAll("button"), b => Assert.Equal("false", b.GetAttribute("aria-pressed")));
    }

    [Fact]
    public void Multiple_Controlled_Veto_With_Distinct_Selection_Rolls_Back_To_That_Selection()
    {
        // A GENUINE, distinguishable veto: the parent's callback explicitly asserts a
        // NEW list reference/content different from BOTH what was pushed (["a"]) AND
        // the pre-interaction baseline (null) — this must still win and roll back.
        IRenderedComponent<L.ToggleGroup>? cut = null;
        var callback = EventCallback.Factory.Create<IEnumerable<string>>(_ctx, (IEnumerable<string> _) =>
        {
            cut!.Render(p => p
                .Add(g => g.Type, L.ToggleGroup.ToggleGroupType.Multiple)
                .Add(g => g.SelectedValues, new List<string> { "b" })
                .Add(g => g.SelectedValuesChanged, EventCallback.Factory.Create<IEnumerable<string>>(_ctx, (_2) => { })));
        });

        cut = _ctx.Render<L.ToggleGroup>(p => p
            .Add(g => g.Type, L.ToggleGroup.ToggleGroupType.Multiple)
            .Add(g => g.SelectedValues, (IEnumerable<string>?)null)
            .Add(g => g.SelectedValuesChanged, callback)
            .Add(g => g.ChildContent, TwoItems()));

        cut.FindAll("button")[0].Click(); // pushes ["a"]

        Assert.Equal("false", cut.FindAll("button")[0].GetAttribute("aria-pressed")); // "a" rejected
        Assert.Equal("true",  cut.FindAll("button")[1].GetAttribute("aria-pressed")); // "b" wins
    }
}
