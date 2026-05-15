using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Services;
using Lumeo.Services.Localization;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;

namespace Lumeo.Tests.Components.Checkbox;

/// <summary>
/// Verifies that HapticFeedback=true triggers HapticsService.Light() (Vibrate(10))
/// when the checkbox is toggled, and that HapticFeedback=false (default) does not.
///
/// Pattern (b): A TrackingInteropService implements IComponentInteropService and
/// counts Vibrate calls without needing to subclass the sealed HapticsService.
/// </summary>
public class CheckboxHapticFeedbackTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _trackingInterop = new();

    public CheckboxHapticFeedbackTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        var module = _ctx.JSInterop.SetupModule("./_content/Lumeo/js/components.js");
        module.Mode = JSRuntimeMode.Loose;

        _ctx.Services.AddSingleton<IComponentInteropService>(_trackingInterop);
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
    public void Checkbox_HapticFeedback_True_Calls_Vibrate_On_Toggle()
    {
        var cut = _ctx.Render<Lumeo.Checkbox>(p => p
            .Add(c => c.HapticFeedback, true));

        cut.Find("button").Click();

        Assert.Equal(1, _trackingInterop.VibrateCallCount);
        Assert.All(_trackingInterop.VibrateArgs, ms => Assert.Equal(10, ms));
    }

    [Fact]
    public void Checkbox_HapticFeedback_False_Does_Not_Call_Vibrate_On_Toggle()
    {
        var cut = _ctx.Render<Lumeo.Checkbox>(p => p
            .Add(c => c.HapticFeedback, false));

        cut.Find("button").Click();

        Assert.Equal(0, _trackingInterop.VibrateCallCount);
    }
}
