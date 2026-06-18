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
}
