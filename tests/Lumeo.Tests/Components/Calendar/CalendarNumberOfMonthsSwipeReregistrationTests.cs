using Bunit;
using Lumeo.Services;
using Lumeo.Services.Localization;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Calendar;

/// <summary>
/// Regression test for the NumberOfMonths 1→2→1 round-trip bug: switching the
/// calendar to multi-month and back permanently killed swipe (and the
/// arrow/Page key page-scroll suppression) navigation.
///
/// Mechanism: the id-bearing day grid element only exists for a single-month
/// Days view (the multi-month panels share one component and omit the id). The
/// swipe + prevent-default-keys (un)registration in OnAfterRenderAsync used to
/// bail out early with `if (NumberOfMonths != 1) return;`, so the transition to
/// 2 months never tore the handlers down and the transition back to 1 month
/// never re-registered them — leaving swipe / key nav dead forever.
///
/// Strategy mirrors CalendarSwipeReregistrationTests: a TrackingInteropService
/// records each RegisterHorizontalSwipe / UnregisterHorizontalSwipe call.
/// Render at NumberOfMonths=1 (register #1) → re-render at 2 (must unregister)
/// → re-render back at 1 (must register #2). Pre-fix the count would stay at 1.
/// </summary>
public class CalendarNumberOfMonthsSwipeReregistrationTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _trackingInterop = new();

    public CalendarNumberOfMonthsSwipeReregistrationTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        var module = _ctx.JSInterop.SetupModule("./_content/Lumeo/js/components.js");
        module.Mode = JSRuntimeMode.Loose;

        _ctx.Services.AddSingleton<IComponentInteropService>(_trackingInterop);
        _ctx.Services.AddScoped<ComponentInteropService>();
        _ctx.Services.AddScoped<ToastService>();
        _ctx.Services.AddScoped<IToastService>(sp => sp.GetRequiredService<ToastService>());
        _ctx.Services.AddScoped<OverlayService>();
        _ctx.Services.AddScoped<IOverlayService>(sp => sp.GetRequiredService<OverlayService>());
        _ctx.Services.AddScoped<ThemeService>();
        _ctx.Services.AddScoped<IThemeService>(sp => sp.GetRequiredService<ThemeService>());
        _ctx.Services.AddScoped<KeyboardShortcutService>();
        _ctx.Services.AddScoped<IKeyboardShortcutService>(sp => sp.GetRequiredService<KeyboardShortcutService>());
        _ctx.Services.AddScoped<IDataGridExportService, Lumeo.Services.DataGridExportService>();
        _ctx.Services.AddScoped<HapticsService>();
        _ctx.Services.AddSingleton<IOptions<LumeoLocalizationOptions>>(_ =>
        {
            var options = new LumeoLocalizationOptions();
            LumeoDefaultStrings.ApplyDefaults(options);
            return Options.Create(options);
        });
        _ctx.Services.AddScoped<ILumeoLocalizer, LumeoLocalizer>();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Calendar_SwipeReregisters_AfterNumberOfMonths_1_2_1_RoundTrip()
    {
        // Render single-month (the id-bearing grid exists) — swipe registers once.
        var cut = _ctx.Render<L.Calendar>(p =>
        {
            p.Add(c => c.SwipeEnabled, true);
            p.Add(c => c.NumberOfMonths, 1);
        });
        cut.WaitForAssertion(() => Assert.Equal(1, _trackingInterop.RegisterHorizontalSwipeCallCount));

        // Go multi-month: the single-grid id disappears, so the orphaned swipe
        // handler must be torn down (no new registration in this state).
        cut.Render(p => p.Add(c => c.NumberOfMonths, 2));
        cut.WaitForAssertion(() => Assert.Equal(1, _trackingInterop.UnregisterHorizontalSwipeCallCount));
        Assert.Equal(1, _trackingInterop.RegisterHorizontalSwipeCallCount);

        // Back to single-month: the id-bearing grid returns, so swipe must
        // re-register. Pre-fix the early-return left _swipeAttached stuck and
        // this second registration never happened.
        cut.Render(p => p.Add(c => c.NumberOfMonths, 1));
        cut.WaitForAssertion(() => Assert.Equal(2, _trackingInterop.RegisterHorizontalSwipeCallCount));
    }
}
