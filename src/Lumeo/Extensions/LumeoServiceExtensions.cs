using Lumeo.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Lumeo;

public static class LumeoServiceExtensions
{
    public static IServiceCollection AddLumeo(this IServiceCollection services)
    {
        services.AddScoped<ComponentInteropService>();
        services.AddScoped<ToastService>();
        services.AddScoped<ThemeService>();
        return services;
    }
}
