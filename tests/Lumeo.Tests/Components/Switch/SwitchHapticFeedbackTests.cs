using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Services;
using Lumeo.Services.Localization;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;

namespace Lumeo.Tests.Components.Switch;

/// <summary>
/// Verifies that HapticFeedback=true triggers HapticsService.Light() (Vibrate(10))
/// on toggle, and that HapticFeedback=false (default) does not.
///
/// Pattern (b): A TrackingInteropService implements IComponentInteropService and
/// counts Vibrate calls. HapticsService is constructed with this tracking interop,
/// so we can assert without needing to subclass the sealed HapticsService.
/// </summary>
public class SwitchHapticFeedbackTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _trackingInterop = new();

    public SwitchHapticFeedbackTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        var module = _ctx.JSInterop.SetupModule("./_content/Lumeo/js/components.js");
        module.Mode = JSRuntimeMode.Loose;

        // Register the tracking interop as the IComponentInteropService so HapticsService
        // picks it up and we can assert Vibrate(10) was called.
        _ctx.Services.AddSingleton<IComponentInteropService>(_trackingInterop);
        // ComponentInteropService still needed for some component internals (uses IJSRuntime directly)
        _ctx.Services.AddScoped<ComponentInteropService>();
        _ctx.Services.AddScoped<HapticsService>();
        _ctx.Services.AddScoped<ToastService>();
        _ctx.Services.AddScoped<IToastService>(sp => sp.GetRequiredService<ToastService>());
        _ctx.Services.AddScoped<OverlayService>();
        _ctx.Services.AddScoped<IOverlayService>(sp => sp.GetRequiredService<OverlayService>());
        _ctx.Services.AddScoped<ThemeService>();
        _ctx.Services.AddScoped<IThemeService>(sp => sp.GetRequiredService<ThemeService>());
        _ctx.Services.AddScoped<KeyboardShortcutService>();
        _ctx.Services.AddScoped<IKeyboardShortcutService>(sp => sp.GetRequiredService<KeyboardShortcutService>());
        _ctx.Services.AddScoped<IDataGridExportService, Lumeo.Services.DataGridExportService>();
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
    public void Switch_HapticFeedback_True_Calls_Vibrate_On_Toggle()
    {
        var cut = _ctx.Render<Lumeo.Switch>(p => p
            .Add(s => s.HapticFeedback, true));

        cut.Find("button").Click();

        Assert.Equal(1, _trackingInterop.VibrateCallCount);
        Assert.All(_trackingInterop.VibrateArgs, ms => Assert.Equal(10, ms));
    }

    [Fact]
    public void Switch_HapticFeedback_False_Does_Not_Call_Vibrate_On_Toggle()
    {
        var cut = _ctx.Render<Lumeo.Switch>(p => p
            .Add(s => s.HapticFeedback, false));

        cut.Find("button").Click();

        Assert.Equal(0, _trackingInterop.VibrateCallCount);
    }
}
