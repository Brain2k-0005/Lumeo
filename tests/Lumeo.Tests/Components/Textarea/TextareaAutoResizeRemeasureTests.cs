using Bunit;
using Lumeo.Tests.Helpers;
using Xunit;

namespace Lumeo.Tests.Components.Textarea;

/// <summary>
/// Regression coverage for two state-on-data-change bugs in Textarea's
/// auto-resize wiring, both rooted in setupAutoResize running ONCE on first
/// render:
///
///   #67 — a Value set programmatically fires no DOM 'input' event, so the
///         textarea keeps its stale height (the JS resize() only runs on input).
///   #167 — a runtime MaxRows change is ignored, because the row cap (maxHeight)
///          is captured once at setup time and never refreshed.
///
/// The fix re-runs setupAutoResize from OnAfterRenderAsync (after the first
/// render, while auto-resize is active) when Value or MaxRows changed. Re-running
/// re-measures immediately AND updates the captured maxHeight; the JS de-dupes the
/// prior input listener so re-calling never stacks handlers.
///
/// These tests assert the MECHANISM via the recorded setupAutoResize JSInterop
/// invocations (arg order: [0]=elementId, [1]=maxRows). Mirrors
/// BackToTopThresholdReRegistrationTests.
/// </summary>
public class TextareaAutoResizeRemeasureTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TextareaAutoResizeRemeasureTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private int SetupCount() =>
        _ctx.JSInterop.Invocations.Count(i => i.Identifier == "setupAutoResize");

    // #67 — programmatic Value change must trigger a re-measure.
    [Fact]
    public void Programmatic_Value_Change_Remeasures_AutoResize()
    {
        var cut = _ctx.Render<Lumeo.Textarea>(p => p
            .Add(t => t.AutoResize, true)
            .Add(t => t.Value, "one line"));

        // First render performs the initial setup/measure.
        Assert.Equal(1, SetupCount());

        // Parent pushes a new Value (no DOM 'input' event fires for this).
        cut.Render(p => p
            .Add(t => t.AutoResize, true)
            .Add(t => t.Value, "line one\nline two\nline three"));

        // Without the fix the height stays stale (no second measure); with it,
        // setupAutoResize is re-invoked to re-measure against the new content.
        cut.WaitForAssertion(() => Assert.Equal(2, SetupCount()));
    }

    // #167 — runtime MaxRows change must re-apply the cap.
    [Fact]
    public void MaxRows_Change_Reapplies_AutoResize_Cap()
    {
        var cut = _ctx.Render<Lumeo.Textarea>(p => p
            .Add(t => t.AutoResize, true)
            .Add(t => t.MaxRows, 3));

        // Initial setup uses the original row cap.
        Assert.Contains(
            _ctx.JSInterop.Invocations,
            i => i.Identifier == "setupAutoResize" && Equals(i.Arguments[1], 3));

        // Parent re-renders with a larger row cap.
        cut.Render(p => p
            .Add(t => t.AutoResize, true)
            .Add(t => t.MaxRows, 8));

        // Without the fix the cap was captured once on firstRender and the new
        // MaxRows is silently ignored; with it, setupAutoResize re-runs with 8.
        cut.WaitForAssertion(() => Assert.Contains(
            _ctx.JSInterop.Invocations,
            i => i.Identifier == "setupAutoResize" && Equals(i.Arguments[1], 8)));
    }

    // Guard: an unrelated re-render (Value and MaxRows unchanged) must NOT churn
    // the JS measure — otherwise every parent render re-measures pointlessly.
    [Fact]
    public void Unchanged_Re_Render_Does_Not_Remeasure()
    {
        var cut = _ctx.Render<Lumeo.Textarea>(p => p
            .Add(t => t.AutoResize, true)
            .Add(t => t.Value, "stable")
            .Add(t => t.MaxRows, 5));

        var afterFirstRender = SetupCount();

        // Re-render with the same Value / MaxRows (e.g. an unrelated parent update).
        cut.Render(p => p
            .Add(t => t.AutoResize, true)
            .Add(t => t.Value, "stable")
            .Add(t => t.MaxRows, 5));

        Assert.Equal(afterFirstRender, SetupCount());
    }
}
