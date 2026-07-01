using Bunit;
using Lumeo.Services;
using Lumeo.Services.Localization;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using Xunit;

namespace Lumeo.Tests.Components.BottomNav;

/// <summary>
/// Regression test for the runtime-PressEffect ripple bug (#189): the ripple
/// pointerdown listener was attached only on firstRender, so an item that
/// switched TO <see cref="Lumeo.Button.ButtonPressEffect.Ripple"/> after the
/// first render never got the handler wired, and an item that switched AWAY
/// from Ripple kept a stale handler (it was never detached — not even on
/// dispose).
///
/// Mechanism: a TrackingInteropService records each RippleAttachAsync /
/// RippleDetachAsync call. Render at PressEffect.None (no attach) → re-render
/// at Ripple (must attach once) → re-render back at None (must detach once).
/// Pre-fix neither the late attach nor the detach happened, so the counts
/// stayed at 0. Mirrors CalendarNumberOfMonthsSwipeReregistrationTests.
/// </summary>
public class BottomNavItemRippleReconcileTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _trackingInterop = new();

    public BottomNavItemRippleReconcileTests()
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
    public void Ripple_Attaches_When_Switched_On_AfterFirstRender_And_Detaches_When_Switched_Off()
    {
        // Render with no press effect — nothing to wire yet.
        var cut = _ctx.Render<Lumeo.BottomNavItem>(p => p
            .Add(i => i.Label, "Home")
            .Add(i => i.PressEffect, Lumeo.Button.ButtonPressEffect.None));
        Assert.Equal(0, _trackingInterop.RippleAttachCallCount);

        // Flip TO Ripple after the first render: the handler must now attach.
        // Pre-fix this was firstRender-only, so the attach never happened.
        cut.Render(p => p.Add(i => i.PressEffect, Lumeo.Button.ButtonPressEffect.Ripple));
        cut.WaitForAssertion(() => Assert.Equal(1, _trackingInterop.RippleAttachCallCount));
        Assert.Equal(0, _trackingInterop.RippleDetachCallCount);

        // Flip AWAY from Ripple: the now-stale handler must be torn down.
        // Pre-fix the handler was never detached, leaking the listener.
        cut.Render(p => p.Add(i => i.PressEffect, Lumeo.Button.ButtonPressEffect.None));
        cut.WaitForAssertion(() => Assert.Equal(1, _trackingInterop.RippleDetachCallCount));
        Assert.Equal(1, _trackingInterop.RippleAttachCallCount);
    }
}
