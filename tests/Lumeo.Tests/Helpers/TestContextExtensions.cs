using Bunit;
using Lumeo.Services;
using Microsoft.Extensions.DependencyInjection;

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
        ctx.Services.AddScoped<ToastService>();
        ctx.Services.AddScoped<ThemeService>();
        ctx.Services.AddScoped<KeyboardShortcutService>();

        return ctx;
    }
}
