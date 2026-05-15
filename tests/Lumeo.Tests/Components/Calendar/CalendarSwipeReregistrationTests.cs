using Bunit;
using Lumeo.Services;
using Lumeo.Services.Localization;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Calendar;

/// <summary>
/// Regression tests for Bug B: after navigating away from the Days view (to
/// Months or Years) and then back, the horizontal swipe handler must be
/// re-registered on the day grid element.
///
/// Strategy: render Calendar, assert RegisterHorizontalSwipe called once
/// (Days view initial render). Click the month/year header to switch to Months
/// view — UnregisterHorizontalSwipe must fire. Click a month to return to Days
/// view — RegisterHorizontalSwipe must fire a second time.
/// </summary>
public class CalendarSwipeReregistrationTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _trackingInterop = new();

    public CalendarSwipeReregistrationTests()
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
    public void Calendar_SwipeReregisters_AfterLeavingAndReturningToDaysView()
    {
        // Render in default Days view — RegisterHorizontalSwipe fires once.
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Calendar>(0);
            builder.AddAttribute(1, "SwipeEnabled", true);
            builder.CloseComponent();
        });

        // Wait for OnAfterRenderAsync to complete before asserting.
        cut.WaitForAssertion(() => Assert.Equal(1, _trackingInterop.RegisterHorizontalSwipeCallCount));

        // Click the combined month+year header button (index 1: prev, header, next)
        // to switch to Months view. The day grid is removed — UnregisterHorizontalSwipe fires.
        var buttons = cut.FindAll("button[type='button']");
        buttons[1].Click(); // → Months view

        cut.WaitForAssertion(() => Assert.Equal(1, _trackingInterop.UnregisterHorizontalSwipeCallCount));
        // No new registration yet — we are in Months view.
        Assert.Equal(1, _trackingInterop.RegisterHorizontalSwipeCallCount);

        // Click the first month button to return to Days view.
        // The day grid is recreated — RegisterHorizontalSwipe must fire a second time.
        var monthButtons = cut.FindAll("button[type='button']")
            .Skip(3) // skip prev, year-header, next
            .First();
        monthButtons.Click(); // → Days view

        cut.WaitForAssertion(() => Assert.Equal(2, _trackingInterop.RegisterHorizontalSwipeCallCount));
    }

    [Fact]
    public void Calendar_SwipeNotRegistered_WhenSwipeEnabledFalse()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Calendar>(0);
            builder.AddAttribute(1, "SwipeEnabled", false);
            builder.CloseComponent();
        });

        // Give OnAfterRenderAsync a chance to complete; count should remain 0.
        cut.WaitForAssertion(() => Assert.Equal(0, _trackingInterop.RegisterHorizontalSwipeCallCount));
    }
}
