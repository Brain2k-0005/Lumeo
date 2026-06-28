using System.Reflection;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Lumeo.Services;
using Microsoft.Extensions.DependencyInjection;
using L = Lumeo;

namespace Lumeo.Tests.Components.ThemeToggle;

/// <summary>
/// battle-wave3 regression coverage for <see cref="L.ThemeToggle"/>.
///
/// #22 (medium, lifecycle) — "First-render init can subscribe + StateHasChanged on
/// an already-disposed component (leak via shared ThemeService event)". The
/// OnAfterRenderAsync(firstRender) tail awaits ThemeService.InitializeAsync and only
/// then runs `Theme.OnThemeChanged += OnThemeChanged; StateHasChanged();`. If the
/// component is disposed while that init is still in flight, the tail used to run
/// anyway — re-subscribing the dead component to the long-lived, shared ThemeService
/// event (a leak) and rendering after teardown. The fix adds a `_disposed` latch set
/// in Dispose() and an `if (_disposed) return;` guard before the subscribe/render tail.
///
/// #63 (low, state-on-data-change) — "Optimistic binary `_isDark = !_isDark` shows the
/// wrong icon for a 3-way (System/Light/Dark) cycle until the async correction lands".
/// ToggleTheme used to flip `_isDark` synchronously before awaiting ToggleModeAsync.
/// But the mode cycle is three-way, so a System->Dark step (which keeps the resolved
/// state dark) was momentarily painted light by the naive negation. The fix drops the
/// optimistic flip and lets OnThemeChanged be the single source of truth.
///
/// The fixture runs JSInterop in Loose mode (mirrors ThemeToggleBehaviorTests). Each
/// test plans the read-side themeManager.* calls it needs; a planned-but-uncompleted
/// invocation is used as a gate to freeze the async chain at a chosen point (mirrors
/// AffixDisposeLifecycleTests / PdfViewer*Tests).
/// </summary>
public class ThemeToggleBattleWave3Tests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ThemeToggleBattleWave3Tests()
    {
        _ctx.AddLumeoServices();
        // Always-completed reads that InitializeAsync performs after getMode. getMode,
        // isDark and setMode are planned per-test so each test can gate them.
        _ctx.JSInterop.Setup<string>("themeManager.getScheme").SetResult("orange");
        _ctx.JSInterop.Setup<string>("themeManager.getDirection").SetResult("ltr");
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // Reflectively count subscribers on ThemeService's field-like event so we can
    // assert the disposed component left NO dangling handler on the shared service.
    private static int ThemeChangedSubscriberCount(ThemeService svc)
    {
        var field = typeof(ThemeService)
            .GetField("OnThemeChanged", BindingFlags.NonPublic | BindingFlags.Instance);
        var del = (Delegate?)field?.GetValue(svc);
        return del?.GetInvocationList().Length ?? 0;
    }

    [Fact]
    public void Dispose_During_InFlight_Init_Leaves_No_Subscription_On_Shared_ThemeService()
    {
        var theme = _ctx.Services.GetRequiredService<ThemeService>();

        // Plan getMode but DON'T complete it: InitializeAsync (called from
        // OnAfterRenderAsync on first render) parks on this read, so the
        // subscribe/StateHasChanged tail never runs synchronously.
        var getMode = _ctx.JSInterop.Setup<string>("themeManager.getMode");
        _ctx.JSInterop.Setup<bool>("themeManager.isDark").SetResult(false);

        var cut = _ctx.Render<L.ThemeToggle>();

        // Init is parked before the subscribe tail — nothing wired to the event yet.
        Assert.Equal(0, ThemeChangedSubscriberCount(theme));

        // Dispose WHILE init is in flight.
        cut.Instance.Dispose();

        // Now let the parked init resolve. The pre-fix tail re-subscribed the dead
        // component (`Theme.OnThemeChanged += OnThemeChanged`); the fixed tail bails on
        // the `_disposed` guard.
        getMode.SetResult("system");
        // Flush the resumed continuation onto the renderer dispatcher.
        cut.InvokeAsync(() => { }).GetAwaiter().GetResult();

        // The shared, long-lived ThemeService event must carry no handler from the
        // disposed toggle. Pre-fix this is 1 (leak); with the guard it stays 0.
        Assert.Equal(0, ThemeChangedSubscriberCount(theme));
    }

    [Fact]
    public void Toggle_Does_Not_Optimistically_Paint_Wrong_Icon_For_ThreeWay_Cycle()
    {
        // System mode that resolves to dark (OS dark): init reads isDark=true, so the
        // toggle starts pressed (Sun icon).
        _ctx.JSInterop.Setup<string>("themeManager.getMode").SetResult("system");
        _ctx.JSInterop.Setup<bool>("themeManager.isDark").SetResult(true);
        // Plan setMode WITHOUT completing it so the async correction (IsDark re-read +
        // OnThemeChanged) cannot land — we observe only what the click paints up front.
        _ctx.JSInterop.SetupVoid("themeManager.setMode");

        var cut = _ctx.Render<L.ThemeToggle>();
        Assert.Equal("true", cut.Find("button").GetAttribute("aria-pressed"));

        // Click cycles System -> Dark; the resolved state stays dark. The pre-fix
        // optimistic `_isDark = !_isDark` flips the icon to light ("false") while the
        // setMode interop is in flight; the fix keeps it dark.
        cut.Find("button").Click();

        Assert.Equal("true", cut.Find("button").GetAttribute("aria-pressed"));
        // Pressed (dark) means the Sun affordance is shown, never the wrong-mode icon.
        Assert.NotNull(cut.Find("button").QuerySelector("svg"));
    }
}
