using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.ConfirmButton;

/// <summary>
/// Wave 4 composition audit — ConfirmButton wraps a native Lumeo &lt;Button&gt;
/// that opens an already-keyboard-tested AlertDialog via IOverlayService.
/// Enter/Space activating the trigger is free via the browser's native button
/// semantics (button.Click() below exercises the exact HandleClick a
/// synthesized keydown would run — see the ScrollspyKeyboardTests precedent for
/// this convention). The dialog's own Escape/focus-trap handling is
/// AlertDialog's job and is covered by its own suite; this file's own
/// incremental surface is the trigger-to-dialog wiring: activation opens the
/// dialog, and a cancelled dialog must NOT fire the destructive OnConfirm.
/// </summary>
public class ConfirmButtonKeyboardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly OverlayService _overlay = new();

    public ConfirmButtonKeyboardTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddScoped<OverlayService>(_ => _overlay);
        _ctx.Services.AddScoped<IOverlayService>(_ => _overlay);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Activating_The_Trigger_Opens_The_Confirm_Dialog()
    {
        OverlayInstance? instance = null;
        _overlay.OnShow += i => instance = i;

        var cut = _ctx.Render<Lumeo.ConfirmButton>();
        cut.Find("button").Click();

        Assert.NotNull(instance);
    }

    [Fact]
    public void Escape_Cancelling_The_Dialog_Does_Not_Invoke_OnConfirm()
    {
        // Escape inside AlertDialog resolves its Task as Cancelled (covered by
        // AlertDialog's own Escape suite) — verify ConfirmButton's reaction to
        // that outcome: OnConfirm must never fire, only OnCancel.
        var confirmed = false;
        var cancelled = false;
        OverlayInstance? instance = null;
        _overlay.OnShow += i => instance = i;

        var cut = _ctx.Render<Lumeo.ConfirmButton>(p => p
            .Add(c => c.OnConfirm, () => confirmed = true)
            .Add(c => c.OnCancel, () => cancelled = true));

        cut.Find("button").Click();
        Assert.NotNull(instance);

        // Simulate the dialog resolving via Escape (AlertDialog's Escape handler
        // resolves the pending Task as Cancelled — the same outcome this asserts).
        cut.InvokeAsync(() => instance!.Tcs!.SetResult(OverlayResult.CancelResult()));

        Assert.False(confirmed);
        Assert.True(cancelled);
    }
}
