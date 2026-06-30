using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.ToggleGroup;

/// <summary>
/// Regression: when a controlled parent vetoes a toggle (re-renders with the same
/// bound Value / SelectedValues it held before the user's action), the component
/// must roll the UI back to the parent-owned state instead of keeping the
/// in-flight rejected selection.
///
/// Bug: the old OnParametersSet compared incoming Value against _lastValue (the
/// last parameter the parent SUPPLIED). When the parent re-renders after a veto it
/// supplies the SAME value → the comparison saw "no change" and skipped the
/// resync, leaving the rejected selection visible.
///
/// Fix: track _lastPushedValue/_lastPushedSelectedValues (what WE emitted via the
/// callback). A controlled re-render that differs from what we pushed is
/// authoritative (normalization / rejection) and must win; a re-render that echoes
/// what we pushed is a benign round-trip and must be ignored (preserves in-flight
/// state).
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
    public void Single_Controlled_Veto_From_Null_Rolls_Back_To_Nothing_Selected()
    {
        // Controlled parent holds Value = null and never updates it (veto everything).
        var cut = _ctx.Render<L.ToggleGroup>(p => p
            .Add(g => g.Type, L.ToggleGroup.ToggleGroupType.Single)
            .Add(g => g.Value, (string?)null)
            .Add(g => g.ValueChanged, (string? _) => { })  // no-op: veto
            .Add(g => g.ChildContent, TwoItems()));

        Assert.All(cut.FindAll("button"), b => Assert.Equal("false", b.GetAttribute("aria-pressed")));

        // Click "a" — ToggleItem selects it and pushes "a" via ValueChanged.
        cut.FindAll("button")[0].Click();

        // Simulate the parent re-rendering with the same Value=null (it ignored our push).
        cut.Render(p => p
            .Add(g => g.Type, L.ToggleGroup.ToggleGroupType.Single)
            .Add(g => g.Value, (string?)null)
            .Add(g => g.ValueChanged, (string? _) => { }));

        // UI must roll back — nothing should show as pressed.
        Assert.All(cut.FindAll("button"), b => Assert.Equal("false", b.GetAttribute("aria-pressed")));
    }

    [Fact]
    public void Single_Controlled_Veto_From_Selected_Rolls_Back_To_Original_Selection()
    {
        // Controlled parent holds Value = "a" and vetoes any deselect attempt.
        var cut = _ctx.Render<L.ToggleGroup>(p => p
            .Add(g => g.Type, L.ToggleGroup.ToggleGroupType.Single)
            .Add(g => g.Value, "a")
            .Add(g => g.ValueChanged, (string? _) => { })  // no-op: veto
            .Add(g => g.ChildContent, TwoItems()));

        Assert.Equal("true", cut.FindAll("button")[0].GetAttribute("aria-pressed"));

        // User clicks "a" to deselect — pushes null via ValueChanged.
        cut.FindAll("button")[0].Click();

        // Parent re-renders with same Value="a" (veto — it kept "a" selected).
        cut.Render(p => p
            .Add(g => g.Type, L.ToggleGroup.ToggleGroupType.Single)
            .Add(g => g.Value, "a")
            .Add(g => g.ValueChanged, (string? _) => { }));

        // "a" must snap back to pressed.
        Assert.Equal("true",  cut.FindAll("button")[0].GetAttribute("aria-pressed"));
        Assert.Equal("false", cut.FindAll("button")[1].GetAttribute("aria-pressed"));
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
    public void Multiple_Controlled_Veto_From_Null_Rolls_Back_To_Nothing_Selected()
    {
        // Controlled parent holds SelectedValues = null and vetoes all changes.
        var cut = _ctx.Render<L.ToggleGroup>(p => p
            .Add(g => g.Type, L.ToggleGroup.ToggleGroupType.Multiple)
            .Add(g => g.SelectedValues, (IEnumerable<string>?)null)
            .Add(g => g.SelectedValuesChanged, (IEnumerable<string> _) => { })
            .Add(g => g.ChildContent, TwoItems()));

        // Click "a" — pushes ["a"] via SelectedValuesChanged.
        cut.FindAll("button")[0].Click();

        // Simulate parent re-rendering with the same null (veto).
        cut.Render(p => p
            .Add(g => g.Type, L.ToggleGroup.ToggleGroupType.Multiple)
            .Add(g => g.SelectedValues, (IEnumerable<string>?)null)
            .Add(g => g.SelectedValuesChanged, (IEnumerable<string> _) => { }));

        Assert.All(cut.FindAll("button"), b => Assert.Equal("false", b.GetAttribute("aria-pressed")));
    }

    [Fact]
    public void Multiple_Controlled_Veto_From_Selected_Rolls_Back_To_Original_Selection()
    {
        // Controlled parent holds SelectedValues = ["a"] and vetoes any change.
        // Using the same list reference simulates a parent that keeps its old binding.
        var originalList = new List<string> { "a" };

        var cut = _ctx.Render<L.ToggleGroup>(p => p
            .Add(g => g.Type, L.ToggleGroup.ToggleGroupType.Multiple)
            .Add(g => g.SelectedValues, originalList)
            .Add(g => g.SelectedValuesChanged, (IEnumerable<string> _) => { })
            .Add(g => g.ChildContent, TwoItems()));

        Assert.Equal("true", cut.FindAll("button")[0].GetAttribute("aria-pressed"));

        // User deselects "a" — pushes [] via SelectedValuesChanged.
        cut.FindAll("button")[0].Click();

        // Parent re-renders with the same originalList reference (veto).
        cut.Render(p => p
            .Add(g => g.Type, L.ToggleGroup.ToggleGroupType.Multiple)
            .Add(g => g.SelectedValues, originalList)
            .Add(g => g.SelectedValuesChanged, (IEnumerable<string> _) => { }));

        Assert.Equal("true",  cut.FindAll("button")[0].GetAttribute("aria-pressed"));
        Assert.Equal("false", cut.FindAll("button")[1].GetAttribute("aria-pressed"));
    }
}
