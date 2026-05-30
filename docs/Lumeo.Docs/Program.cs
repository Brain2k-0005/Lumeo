using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.WebAssembly.Services;
using Microsoft.Extensions.DependencyInjection;
using Lumeo;
using Lumeo.Docs;
using Lumeo.Docs.Services;
using Lumeo.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddLumeo();
builder.Services.AddSingleton<IconService>();
builder.Services.AddSingleton<PatternFilterService>();
// Scoped, not Singleton — both depend on HttpClient which is Scoped in Blazor WASM.
// In WASM there's only one scope per app lifetime, so caching semantics are identical
// to a Singleton, but DI lifetime validation requires the consumer match the dependency.
builder.Services.AddScoped<NavConfigService>();
builder.Services.AddScoped<RegistryService>();

var host = builder.Build();

// Lazy-load the DataGrid Excel/PDF export backend on first export instead of at first paint.
// The DataGrid calls this hook before exporting; the assemblies are marked
// BlazorWebAssemblyLazyLoad in this project's .csproj.
var lazyLoader = host.Services.GetRequiredService<LazyAssemblyLoader>();
DataGridExportLoader.LoadAssembliesAsync = names => lazyLoader.LoadAssembliesAsync(names);

await host.RunAsync();
