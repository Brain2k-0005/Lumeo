using Bunit;
using Lumeo.Tests.Helpers;
using Xunit;

namespace Lumeo.Tests.Components.Textarea;

/// <summary>
/// Lifecycle regression coverage for #166 — "AutoResize latches on firstRender".
///
/// The original OnAfterRenderAsync gated the auto-resize SETUP behind
/// `firstRender`, so:
///   * toggling AutoResize false->true AFTER the first render never registered
///     the JS measure (the field silently stopped auto-growing), and
///   * toggling AutoResize true->false never tore the measure down (the JS input
///     listener leaked and kept resizing an opted-out field).
///
/// The fix reconciles every render: register when AutoResize is on and not yet
/// set up, unregister when it is off and still set up. These tests assert the
/// MECHANISM via the recorded setupAutoResize / unregisterAutoResize JSInterop
/// invocations, mirroring TextareaAutoResizeRemeasureTests.
/// </summary>
public class TextareaAutoResizeToggleLifecycleTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TextareaAutoResizeToggleLifecycleTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private int SetupCount() =>
        _ctx.JSInterop.Invocations.Count(i => i.Identifier == "setupAutoResize");

    private int UnregisterCount() =>
        _ctx.JSInterop.Invocations.Count(i => i.Identifier == "unregisterAutoResize");

    // #166 — enabling AutoResize AFTER the first render must register the measure.
    [Fact]
    public void Enabling_AutoResize_After_First_Render_Registers()
    {
        // Initial render with AutoResize OFF: nothing should be wired up.
        var cut = _ctx.Render<Lumeo.Textarea>(p => p
            .Add(t => t.AutoResize, false)
            .Add(t => t.Value, "one line"));

        Assert.Equal(0, SetupCount());

        // Consumer toggles AutoResize on at runtime.
        cut.Render(p => p
            .Add(t => t.AutoResize, true)
            .Add(t => t.Value, "one line"));

        // Without the fix the firstRender latch swallows this and the measure is
        // never registered; with it, setupAutoResize fires exactly once.
        cut.WaitForAssertion(() => Assert.Equal(1, SetupCount()));
    }

    // #166 — disabling AutoResize after it was set up must tear the measure down.
    [Fact]
    public void Disabling_AutoResize_After_Setup_Unregisters()
    {
        // Initial render with AutoResize ON: the measure is registered on first render.
        var cut = _ctx.Render<Lumeo.Textarea>(p => p
            .Add(t => t.AutoResize, true)
            .Add(t => t.Value, "one line"));

        Assert.Equal(1, SetupCount());
        Assert.Equal(0, UnregisterCount());

        // Consumer toggles AutoResize off at runtime.
        cut.Render(p => p
            .Add(t => t.AutoResize, false)
            .Add(t => t.Value, "one line"));

        // Without the fix the JS input listener leaks (no teardown on toggle-off);
        // with it, unregisterAutoResize fires exactly once.
        cut.WaitForAssertion(() => Assert.Equal(1, UnregisterCount()));
    }

    // Guard: a never-enabled textarea must never touch the auto-resize interop,
    // so the toggle reconciliation does not register against an opted-out field.
    [Fact]
    public void AutoResize_Off_Never_Registers()
    {
        var cut = _ctx.Render<Lumeo.Textarea>(p => p
            .Add(t => t.AutoResize, false)
            .Add(t => t.Value, "stable"));

        cut.Render(p => p
            .Add(t => t.AutoResize, false)
            .Add(t => t.Value, "stable changed"));

        Assert.Equal(0, SetupCount());
        Assert.Equal(0, UnregisterCount());
    }
}
