using Bunit;
using Lumeo.Services;
using Lumeo.Services.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Lumeo.Tests.Helpers;

public static class TestContextExtensions
{
    /// <summary>
    /// Registers all Lumeo services with bUnit's mock JSInterop in loose mode.
    /// Call this before rendering any Lumeo component that depends on services.
    /// </summary>
    public static BunitContext AddLumeoServices(this BunitContext ctx)
    {
        // Enable loose mode so any unplanned JS call returns a default value
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        // Set up the module handler for the components.js module import
        // The module also inherits loose mode, so all calls return defaults
        var module = ctx.JSInterop.SetupModule("./_content/Lumeo/js/components.js");
        module.Mode = JSRuntimeMode.Loose;

        // Register all Lumeo services — they'll use bUnit's fake IJSRuntime
        ctx.Services.AddScoped<ComponentInteropService>();
        // Some components (e.g. Button) inject the interface type — bind it
        // to the same concrete instance so tests keep working.
        ctx.Services.AddScoped<IComponentInteropService>(sp => sp.GetRequiredService<ComponentInteropService>());
        ctx.Services.AddScoped<ToastService>();
        ctx.Services.AddScoped<IToastService>(sp => sp.GetRequiredService<ToastService>());
        ctx.Services.AddScoped<OverlayService>();
        ctx.Services.AddScoped<IOverlayService>(sp => sp.GetRequiredService<OverlayService>());
        ctx.Services.AddScoped<ThemeService>();
        ctx.Services.AddScoped<IThemeService>(sp => sp.GetRequiredService<ThemeService>());
        ctx.Services.AddScoped<KeyboardShortcutService>();
        ctx.Services.AddScoped<IKeyboardShortcutService>(sp => sp.GetRequiredService<KeyboardShortcutService>());
        ctx.Services.AddScoped<IDataGridExportService, Lumeo.Services.DataGridExportService>();

        // Localization — apply defaults so components can resolve strings in tests
        ctx.Services.AddSingleton<IOptions<LumeoLocalizationOptions>>(_ =>
        {
            var options = new LumeoLocalizationOptions();
            LumeoDefaultStrings.ApplyDefaults(options);
            return Options.Create(options);
        });
        ctx.Services.AddScoped<ILumeoLocalizer, LumeoLocalizer>();

        return ctx;
    }
}
