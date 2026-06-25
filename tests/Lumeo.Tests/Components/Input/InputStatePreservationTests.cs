using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;

namespace Lumeo.Tests.Components.Input;

/// <summary>
/// Regression tests for triage #39 (state-on-data-change): the uncontrolled
/// editing value must NOT live in the [Parameter] Value. An unrelated parent
/// re-render that re-supplies the same (or a stale) Value must not revert the
/// user's typed text. The live value is held in a private backing field and the
/// parameter is adopted only when the PARENT actually changes it.
/// </summary>
public class InputStatePreservationTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public InputStatePreservationTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void TypedText_Survives_Unrelated_Parent_ReRender_With_Same_Value()
    {
        // Uncontrolled usage: a parent supplies an initial Value but does NOT
        // bind ValueChanged (so the typed text is never written back into the
        // parameter). The user types, then the parent re-renders for an
        // unrelated reason, re-supplying the SAME original Value parameter.
        var cut = _ctx.Render<Lumeo.Input>(p => p
            .Add(b => b.Value, "original"));

        cut.Find("input").Input("user typed this");

        // Unrelated parent re-render that echoes the original parameter value.
        // Pre-fix this re-supplied Value clobbered the rendered text back to
        // "original"; with the backing-field fix the typed text must survive.
        cut.Render(p => p.Add(b => b.Value, "original"));

        Assert.Equal("user typed this", cut.Find("input").GetAttribute("value"));
    }

    [Fact]
    public void ExternalValueChange_From_Parent_Is_Adopted()
    {
        // The flip side: a GENUINE consumer-driven change to the Value parameter
        // (the parent supplies a different value than last time) MUST still be
        // adopted, so controlled usage and external resets keep working.
        var cut = _ctx.Render<Lumeo.Input>(p => p
            .Add(b => b.Value, "first"));

        cut.Find("input").Input("typed over first");

        // Parent now supplies a genuinely different value.
        cut.Render(p => p.Add(b => b.Value, "second"));

        Assert.Equal("second", cut.Find("input").GetAttribute("value"));
    }

    [Fact]
    public void ClearableButton_Visibility_Tracks_Typed_Value_After_Stale_ReRender()
    {
        // Clearable starts empty (no X button). After typing, the X must appear
        // and SURVIVE an unrelated re-render that re-supplies the empty Value,
        // because the clear button is gated on the live value, not the parameter.
        var cut = _ctx.Render<Lumeo.Input>(p => p
            .Add(b => b.Clearable, true)
            .Add(b => b.Value, ""));

        Assert.Empty(cut.FindAll("button"));

        cut.Find("input").Input("abc");
        Assert.NotEmpty(cut.FindAll("button"));

        // Stale uncontrolled re-render re-supplying the empty parameter.
        cut.Render(p => p
            .Add(b => b.Clearable, true)
            .Add(b => b.Value, ""));

        Assert.Equal("abc", cut.Find("input").GetAttribute("value"));
        Assert.NotEmpty(cut.FindAll("button"));
    }
}
