using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Lumeo;
using Lumeo.Docs;
using Lumeo.Docs.Services;

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

await builder.Build().RunAsync();
