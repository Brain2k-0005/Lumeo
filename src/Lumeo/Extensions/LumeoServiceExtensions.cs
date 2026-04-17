using Lumeo.Services;
using Lumeo.Services.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Lumeo;

public static class LumeoServiceExtensions
{
    /// <summary>Registers Lumeo services with built-in English and German component text.</summary>
    public static IServiceCollection AddLumeo(this IServiceCollection services)
        => AddLumeo(services, configureLocalization: null);

    /// <summary>
    /// Registers Lumeo services and lets you customize the built-in component text.
    /// Default English and German translations are applied before your callback runs,
    /// so you can override single keys or add whole cultures (e.g. "fr").
    /// </summary>
    /// <example>
    /// <code>
    /// builder.Services.AddLumeo(opts =>
    /// {
    ///     opts.Add("de", "DataGrid.NoData", "Keine Datensätze");
    ///     opts.AddMany("fr", new Dictionary&lt;string, string&gt;
    ///     {
    ///         ["DataGrid.NoData"] = "Aucune donnée",
    ///         ["Pagination.Previous"] = "Précédent",
    ///     });
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddLumeo(
        this IServiceCollection services,
        Action<LumeoLocalizationOptions>? configureLocalization)
    {
        services.AddScoped<ComponentInteropService>();
        services.AddScoped<ToastService>();
        services.AddScoped<OverlayService>();
        services.AddScoped<ThemeService>();
        services.AddScoped<KeyboardShortcutService>();

        services.AddSingleton<IOptions<LumeoLocalizationOptions>>(_ =>
        {
            var options = new LumeoLocalizationOptions();
            LumeoDefaultStrings.ApplyDefaults(options);
            configureLocalization?.Invoke(options);
            return Options.Create(options);
        });
        services.AddScoped<ILumeoLocalizer, LumeoLocalizer>();

        return services;
    }
}
