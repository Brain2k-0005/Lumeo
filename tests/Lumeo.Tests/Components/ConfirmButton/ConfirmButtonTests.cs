using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.ConfirmButton;

/// <summary>
/// #231: ConfirmButton had no re-entrancy guard, so a fast double-click opened a
/// second confirm dialog on top of the first and fired OnConfirm twice. The
/// guard ignores clicks while a dialog is already in flight and resets once it
/// resolves.
/// </summary>
public class ConfirmButtonTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly OverlayService _overlay = new();

    public ConfirmButtonTests()
    {
        _ctx.AddLumeoServices();
        // Swap in an OverlayService we hold a reference to so we can observe the
        // OnShow stream and resolve the dialog's TaskCompletionSource by hand.
        _ctx.Services.AddScoped<OverlayService>(_ => _overlay);
        _ctx.Services.AddScoped<IOverlayService>(_ => _overlay);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Double_Click_Opens_Only_One_Dialog()
    {
        var shown = new List<OverlayInstance>();
        _overlay.OnShow += shown.Add;

        var cut = _ctx.Render<Lumeo.ConfirmButton>();
        var button = cut.Find("button");

        // Two clicks before the first dialog resolves — the guard must swallow
        // the second so only one dialog is requested.
        button.Click();
        button.Click();

        Assert.Single(shown);
    }

    [Fact]
    public void OnConfirm_Fires_Once_For_Rapid_Double_Click()
    {
        var confirmCount = 0;
        OverlayInstance? instance = null;
        _overlay.OnShow += i => instance = i;

        var cut = _ctx.Render<Lumeo.ConfirmButton>(p => p
            .Add(c => c.OnConfirm, () => confirmCount++));

        var button = cut.Find("button");
        button.Click();
        button.Click();

        // Resolve the single dialog as "confirmed".
        Assert.NotNull(instance);
        cut.InvokeAsync(() => instance!.Tcs!.SetResult(OverlayResult.Ok(null)));

        Assert.Equal(1, confirmCount);
    }

    [Fact]
    public void Guard_Resets_So_A_Later_Click_Opens_A_New_Dialog()
    {
        var shown = new List<OverlayInstance>();
        _overlay.OnShow += shown.Add;

        var cut = _ctx.Render<Lumeo.ConfirmButton>();
        var button = cut.Find("button");

        button.Click();
        Assert.Single(shown);

        // Resolve the first dialog, then click again — a fresh dialog should open.
        cut.InvokeAsync(() => shown[0].Tcs!.SetResult(OverlayResult.CancelResult()));
        button.Click();

        Assert.Equal(2, shown.Count);
    }

    /// <summary>
    /// #29 (lifecycle): the confirm dialog is awaited across a yield, during which
    /// the parent can remove the ConfirmButton from the tree. Resolving the
    /// orphaned dialog afterwards must NOT fire the destructive OnConfirm against
    /// the torn-down component. Without the _disposed guard, OnConfirm would still
    /// run once the dialog resolves "confirmed".
    /// </summary>
    [Fact]
    public void Disposing_Mid_Dialog_Suppresses_OnConfirm_When_Dialog_Later_Resolves()
    {
        var confirmCount = 0;
        OverlayInstance? instance = null;
        _overlay.OnShow += i => instance = i;

        // Host the ConfirmButton in a conditional root so we can unmount (and thus
        // dispose) it while its dialog is still open.
        var host = _ctx.Render<ConditionalRoot>(p => p
            .Add(c => c.Show, true)
            .AddChildContent<Lumeo.ConfirmButton>(cb => cb
                .Add(b => b.OnConfirm, () => confirmCount++)));

        host.Find("button").Click();
        Assert.NotNull(instance);

        // Unmount the ConfirmButton — its Dispose() runs, cancelling the dialog and
        // latching the teardown guard.
        host.Render(p => p.Add(c => c.Show, false));

        // Simulate the (now orphaned) dialog resolving "confirmed" after disposal.
        // TrySet is defensive in case the overlay teardown already completed it.
        host.InvokeAsync(() => instance!.Tcs!.TrySetResult(OverlayResult.Ok(null)));

        Assert.Equal(0, confirmCount);
    }

    /// <summary>
    /// #29: unmounting a ConfirmButton with an open dialog must not throw — Dispose
    /// tears the overlay down cleanly and never double-resolves.
    /// </summary>
    [Fact]
    public void Disposing_Mid_Dialog_Does_Not_Throw()
    {
        OverlayInstance? instance = null;
        _overlay.OnShow += i => instance = i;

        var host = _ctx.Render<ConditionalRoot>(p => p
            .Add(c => c.Show, true)
            .AddChildContent<Lumeo.ConfirmButton>(cb => cb
                .Add(b => b.OnConfirm, () => { })));

        host.Find("button").Click();
        Assert.NotNull(instance);

        var ex = Record.Exception(() =>
        {
            host.Render(p => p.Add(c => c.Show, false));
            host.InvokeAsync(() => instance!.Tcs!.TrySetResult(OverlayResult.Ok(null)));
        });

        Assert.Null(ex);
    }
}
