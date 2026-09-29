using System.Reflection;
using System.Runtime.CompilerServices;
using Bunit;
using Lumeo.Services;
using Lumeo.Services.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;

namespace Lumeo.Tests.Helpers;

public static class TestContextExtensions
{
    /// <summary>
    /// Raise the WaitForAssertion ceiling from bUnit's 1 s default, once for the
    /// whole test assembly (the property is a static global). Many overlay
    /// components play an exit animation and only unmount on the follow-up render,
    /// and focus moves through async JS interop. Those renders/callbacks are fast
    /// locally but can land just past 1 s on a heavily parallel CI runner, which
    /// flaked a *different* timing test almost every run. WaitForAssertion returns
    /// the moment the assertion passes, so a higher ceiling only buys headroom under
    /// load — it does not slow tests that pass promptly.
    /// </summary>
    [ModuleInitializer]
    internal static void RaiseDefaultWaitTimeout()
        => BunitContext.DefaultWaitTimeout = TimeSpan.FromSeconds(10);

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
        ctx.Services.AddScoped<HapticsService>();
        // 2.1.3 — OverlayProvider now injects IResponsiveService for the responsive
        // mobile overrides on OverlayOptions. The interop is loose-mode so the
        // service's interop calls return defaults; the service itself is a no-op
        // until OnViewportChange is invoked from tests.
        ctx.Services.AddScoped<ResponsiveService>();
        ctx.Services.AddScoped<IResponsiveService>(sp => sp.GetRequiredService<ResponsiveService>());

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

    /// <summary>
    /// Registers (or re-fetches) the bUnit JS module mock for the EXACT versioned
    /// import path <see cref="ComponentInteropService.GetModuleAsync"/> actually
    /// requests (<c>./_content/Lumeo/js/components.js?v=&lt;AssemblyInformationalVersion&gt;</c>).
    ///
    /// <see cref="AddLumeoServices"/> registers the bare (unversioned) path, which is
    /// enough for every test that just needs Loose-mode auto-completion — but does NOT
    /// intercept the real "import" call bUnit's fake <c>IJSRuntime</c> resolves it to
    /// (a distinct mock keyed by the exact string), so configuring a specific call's
    /// result/exception on that bare-path handle silently never fires. Call this
    /// instead whenever a test needs to plan a specific invocation (e.g.
    /// <c>.SetException(...)</c> on <c>attachOverlayExitEnd</c>, or a deferred
    /// <c>.SetVoidResult()</c> to model a JS call parked mid-await).
    /// </summary>
    public static BunitJSModuleInterop SetupComponentsModule(this BunitContext ctx)
    {
        var v = typeof(ComponentInteropService).Assembly
            .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? typeof(ComponentInteropService).Assembly.GetName().Version?.ToString()
            ?? "0";
        var module = ctx.JSInterop.SetupModule($"./_content/Lumeo/js/components.js?v={v}");
        module.Mode = JSRuntimeMode.Loose;
        return module;
    }
}
